#!/usr/bin/perl
# Read-only: lists the dialogs of the message block (BMD, magic MSG1) inside a FlowScript file (type table 3), with name,
# kind (0 message, 1 selection), line count, speaker and the raw text bytes (printable characters, other bytes as {xx}).
# Usage: perl tools/unity/bmd.pl <file.bf> [first N]
use strict; use warnings;
my ($file, $max) = @ARGV;
open my $fh, '<:raw', $file or die "$file: $!";
local $/; my $d = <$fh>; close $fh;
my $ntab = unpack 'V', substr($d, 16, 4);
my ($base, $size);
for my $t (0 .. $ntab - 1) {
    my ($type, $isz, $cnt, $addr) = unpack 'VVVV', substr($d, 32 + 16 * $t, 16);
    ($base, $size) = ($addr, $cnt * $isz) if $type == 3;
}
die "no message block" unless defined $base;
my $m = substr($d, $base, $size);
die "not MSG1" unless substr($m, 8, 4) eq 'MSG1';
my $count = unpack 'V', substr($m, 0x18, 4);
print "$count dialogs\n";
for my $i (0 .. $count - 1) {
    last if defined $max && $i >= $max;
    my ($kind, $off) = unpack 'VV', substr($m, 0x20 + 8 * $i, 8);
    my $p = 0x20 + $off;
    my $name = unpack 'Z24', substr($m, $p, 24);
    my ($lines, $speaker, @starts, $tsize);
    if ($kind == 0) {
        ($lines, $speaker) = unpack 'vv', substr($m, $p + 24, 4);
        @starts = unpack "V$lines", substr($m, $p + 28, 4 * $lines);
        $tsize = unpack 'V', substr($m, $p + 28 + 4 * $lines, 4);
        $p += 32 + 4 * $lines;
    } else {
        my ($f18, $n, $f1c, $f1e) = unpack 'vvvv', substr($m, $p + 24, 8);
        $lines = $n; $speaker = 0xFFFF;
        @starts = unpack "V$n", substr($m, $p + 32, 4 * $n);
        $tsize = unpack 'V', substr($m, $p + 32 + 4 * $n, 4);
        $p += 36 + 4 * $n;
    }
    my $text = substr($m, $p, $tsize);
    $text =~ s/([^\x20-\x7e])/sprintf '{%02x}', ord $1/ge;
    printf "%3d %s %-28s lines %d speaker %s: %s\n", $i, $kind ? 'SEL' : 'MSG', $name, $lines, $speaker == 0xFFFF ? '-' : $speaker, $text;
}
