#!/usr/bin/perl
# Read-only survey of a field's event procedures (FlowScript fNNN.bf): for each procedure, the story flags it tests
# first, the prompt it waits on (a COMM taking a PANELMES number, ETC_EN PANELMES_L0001..L0011) and the first message it
# shows (BMD dialog name and its first line, localized from the eNNN_en bundle's EnnnN_EN table).
# Command numbers are those seen in the scripts (0x7 flag test, 0x8 flag set, 0x5E and 0x21D prompts, 0x0 message):
# unverified until the 0.0.70+ dump names them from ScrCommandTable in game.
# Usage: perl tools/unity/events.pl <file.bf> <localization dump (msgID = text lines)> [procedure regex]
use strict; use warnings;
my ($file, $loc, $only) = @ARGV;
my %L;
if ($loc && open my $lf, '<', $loc) { while (<$lf>) { chomp; $L{$1} = $2 if /^(\S+) = (.*)$/ } }
my @PANEL = ('', 'Talk', 'Open Door', 'Jump Off', 'Enter', 'Ride', 'Climb Ladder', 'Descend Ladder', 'Open', 'Examine', 'Exit', 'Enter');
open my $fh, '<:raw', $file or die "$file: $!";
local $/; my $d = <$fh>; close $fh;
my $ntab = unpack 'V', substr($d, 16, 4);
my %tab;
for my $t (0 .. $ntab - 1) { my ($type, $isz, $cnt, $addr) = unpack 'VVVV', substr($d, 32 + 16 * $t, 16); $tab{$type} = [$isz, $cnt, $addr]; }
my @procs; { my ($isz, $cnt, $addr) = @{ $tab{0} }; for my $i (0 .. $cnt - 1) { push @procs, [unpack('Z24', substr($d, $addr + $i * $isz, 24)), unpack('V', substr($d, $addr + $i * $isz + 24, 4))] } }
my ($csz, $ccount, $caddr) = @{ $tab{2} };
# Dialogs: name and the localization keys of their lines.
my @dlg;
if ($tab{3}) {
    my ($isz, $cnt, $addr) = @{ $tab{3} };
    my $m = substr($d, $addr, $cnt * $isz);
    my $count = unpack 'V', substr($m, 0x18, 4);
    for my $i (0 .. $count - 1) {
        my ($kind, $off) = unpack 'VV', substr($m, 0x20 + 8 * $i, 8);
        my $p = 0x20 + $off;
        my $name = unpack 'Z24', substr($m, $p, 24);
        my $rest = substr($m, $p, 4096);
        my @keys = $rest =~ /<([A-Za-z0-9_]+)>/g;
        push @dlg, [$name, $kind, \@keys];
    }
}
my @order = sort { $a->[1] <=> $b->[1] } @procs;
for my $k (0 .. $#order) {
    my ($name, $start) = @{ $order[$k] };
    my $end = $k < $#order ? $order[$k + 1][1] : $ccount;
    next if defined $only && $name !~ /$only/;
    my (@flags, @sets, @prompts, $msg, $actions);
    my @pushed;
    for (my $c = $start; $c < $end; $c++) {
        my ($op, $arg) = unpack 'vs', substr($d, $caddr + 4 * $c, 4);
        if ($op == 29) { push @pushed, $arg; next }
        if ($op == 0) { push @pushed, unpack('l<', substr($d, $caddr + 4 * ($c + 1), 4)); $c++; next }
        if ($op == 1) { $c++; next }
        if ($op == 8) {
            my $cmd = $arg & 0xFFFF;
            my $last = @pushed ? $pushed[-1] : undef;
            if ($cmd == 0x7) { push @flags, $last if defined $last && !@prompts && !defined $msg }
            elsif ($cmd == 0x8) { push @sets, $last if defined $last }
            elsif ($cmd == 0x5E || $cmd == 0x21D) { push @prompts, sprintf('%s(%s)', $cmd == 0x5E ? 'P' : 'Q', defined $last ? ($PANEL[$last] // $last) : '?') }
            elsif ($cmd == 0x0 && !defined $msg && defined $last && $dlg[$last]) {
                my ($dn, $kind, $keys) = @{ $dlg[$last] };
                my $line = @$keys ? ($L{$keys->[0]} // "<$keys->[0]>") : '';
                $msg = "$dn: $line";
            }
            $actions++ unless $cmd == 0x7;
            @pushed = ();
            next;
        }
        @pushed = () unless $op == 4;
    }
    printf "%-22s gate flags [%s] prompt %s first message %s; sets [%s]\n", $name, join(' ', @flags), @prompts ? join(' ', @prompts) : 'none',
        $msg // 'none', join(' ', @sets);
}
