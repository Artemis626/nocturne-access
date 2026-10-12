#!/usr/bin/perl
# Read-only disassembler for the game's FlowScript files (FLW0, the field scripts fNNN.bf in the fNNN bundles), laid out
# as the game's scr_H structs: scrHeader_t (32 bytes), scrTypeTable_t (Type, ItemSize, ItemCount, ItemAddress; SCR_DATATYPE
# order: procedures, labels, code, messages, strings), scrProcTable_t / scrLabelTable_t (24-byte name, CodeAddress),
# scrCode_t (u16 opcode, u16 operand). Opcode numbers follow the game's dispatch table ScrTraceCode.codefunc, checked
# against the 0.0.70 dump (OPNAMES below is the order to verify there; until then they are marked unverified).
# Usage: perl tools/unity/flowscript.pl <file.bf> [procedure name regex]
use strict; use warnings;
my ($file, $only) = @ARGV;
open my $fh, '<:raw', $file or die "$file: $!";
local $/; my $d = <$fh>; close $fh;
die "not FLW0" unless substr($d, 8, 4) eq 'FLW0';
my $ntab = unpack 'V', substr($d, 16, 4);
my %tab;
for my $t (0 .. $ntab - 1) {
    my ($type, $isz, $cnt, $addr) = unpack 'VVVV', substr($d, 32 + 16 * $t, 16);
    $tab{$type} = [$isz, $cnt, $addr];
}
sub names { my ($t) = @_; my ($isz, $cnt, $addr) = @{ $tab{$t} }; my @r;
    for my $i (0 .. $cnt - 1) { my $e = substr($d, $addr + $i * $isz, $isz); my $n = unpack 'Z24', $e; my $a = unpack 'V', substr($e, 24, 4); push @r, [$n, $a]; } @r }
my @procs = names(0);
my @labels = names(1);
my ($csz, $ccount, $caddr) = @{ $tab{2} };
my @OPNAMES = qw(PUSHI PUSHF PUSHIX PUSHIF PUSHREG POPIX POPFX PROC COMM END JUMP CALL RUN GOTO ADD SUB MUL DIV MINUS NOT OR AND EQ NEQ S L SE LE IF PUSHIS PUSHLIX PUSHLFX POPLIX POPLFX PUSHSTR);
my %labelAt; push @{ $labelAt{$_->[1]} }, $_->[0] for @labels;
my %procAt; $procAt{$_->[1]} = $_->[0] for @procs;
printf "%s: %d procedures, %d labels, %d instructions, messages %d bytes, strings %d bytes\n", $file, scalar @procs, scalar @labels, $ccount,
    $tab{3} ? $tab{3}[1] : 0, $tab{4} ? $tab{4}[1] : 0;
my @order = sort { $a->[1] <=> $b->[1] } @procs;
for my $k (0 .. $#order) {
    my ($name, $start) = @{ $order[$k] };
    my $end = $k < $#order ? $order[$k + 1][1] : $ccount;
    next if defined $only && $name !~ /$only/;
    printf "\nprocedure %s [%d..%d)\n", $name, $start, $end;
    for (my $c = $start; $c < $end; $c++) {
        my ($op, $arg) = unpack 'vs', substr($d, $caddr + 4 * $c, 4);
        my $uarg = $arg & 0xFFFF;
        print "  label $_:\n" for @{ $labelAt{$c} // [] };
        my $on = $OPNAMES[$op] // "op$op";
        my $text = "$on $arg";
        if ($on eq 'PUSHI') { $text = 'PUSHI ' . unpack('l<', substr($d, $caddr + 4 * ($c + 1), 4)); $c++ }
        elsif ($on eq 'PUSHF') { $text = 'PUSHF ' . unpack('f<', substr($d, $caddr + 4 * ($c + 1), 4)); $c++ }
        elsif ($on eq 'COMM') { $text = sprintf 'COMM 0x%X', $uarg }
        elsif ($on eq 'IF' || $on eq 'GOTO') { $text = "$on " . ($labels[$uarg] ? $labels[$uarg][0] : "label$uarg") }
        elsif ($on eq 'CALL' || $on eq 'JUMP') { $text = "$on " . ($procs[$uarg] ? $procs[$uarg][0] : "proc$uarg") }
        elsif ($on eq 'PUSHSTR') { my ($ssz, $scnt, $saddr) = @{ $tab{4} }; $text = "PUSHSTR \"" . unpack('Z*', substr($d, $saddr + $uarg)) . "\"" }
        printf "  %5d: %s\n", $c, $text;
    }
}
