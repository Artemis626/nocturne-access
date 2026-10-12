#!/usr/bin/perl
# Offline prototype of the mod's FieldScript dry run (0.0.71): follows an event procedure's control flow with assumed
# story flags (all off, or FLAGS="1269 1274" on), evaluating only the flag test, and stops at the first other command.
# Outcome: action (a prompt command), story (another command first), inactive (END first) or unknown (a value the dry
# run cannot know). Command numbers as seen in the scripts (0x7 flag test, 0x5E / 0x21D prompts, 0x0 message): the mod
# itself resolves them by name from ScrCommandTable.
# Usage: perl tools/unity/simulate.pl <file.bf> [procedure regex]
use strict; use warnings;
my ($file, $only) = @ARGV;
my %on = map { $_ => 1 } split ' ', ($ENV{FLAGS} // '');
open my $fh, '<:raw', $file or die "$file: $!";
local $/; my $d = <$fh>; close $fh;
my $ntab = unpack 'V', substr($d, 16, 4);
my %tab;
for my $t (0 .. $ntab - 1) { my ($type, $isz, $cnt, $addr) = unpack 'VVVV', substr($d, 32 + 16 * $t, 16); $tab{$type} = [$isz, $cnt, $addr]; }
my (@procs, @labels);
for my $pair ([0, \@procs], [1, \@labels]) { my ($isz, $cnt, $addr) = @{ $tab{$pair->[0]} }; for my $i (0 .. $cnt - 1) { push @{ $pair->[1] }, [unpack('Z24', substr($d, $addr + $i * $isz, 24)), unpack('V', substr($d, $addr + $i * $isz + 24, 4))] } }
my ($csz, $ccount, $caddr) = @{ $tab{2} };
my %PROMPT = (0x5E => 1, 0x21D => 1);
my %param = (0x7 => 1, 0x5E => 1, 0x21D => 1, 0x0 => 1);
my %count;
for my $p (@procs) {
    my ($name, $start) = @$p;
    next unless $name =~ /^\d\d\d_\d\d(eve|npc)_\d\d$/;
    next if defined $only && $name !~ /$only/;
    my ($out, $why) = run($start);
    $count{$out}++;
    printf "%-16s %-8s %s\n", $name, $out, $why;
}
print STDERR join(', ', map { "$_ $count{$_}" } sort keys %count), "\n";

# Outcome order: an action anywhere wins, then a story command, then the end; a branch on a value the dry run cannot
# know (a query command's result, read by PUSHREG right after it) is followed both ways.
my %RANK = (action => 3, story => 2, inactive => 1, unknown => 0);
my $budget;
sub run {
    my ($pc) = @_;
    $budget = 20000;
    my %visited;
    return explore($pc, [], undef, [], \%visited, 0);
}

sub explore {
    my ($pc, $st0, $reg, $calls0, $visited, $depth) = @_;
    my @st = @$st0;
    my @calls = @$calls0;
    my %seen;
    while ($budget-- > 0) {
        return ('unknown', "ran off the code at $pc") if $pc < 0 || $pc >= $ccount;
        my ($op, $arg) = unpack 'vs', substr($d, $caddr + 4 * $pc, 4);
        my $u = $arg & 0xFFFF;
        if ($op == 7) { $pc++; next }                                            # PROC
        if ($op == 29) { push @st, $arg; $pc++; next }                           # PUSHIS
        if ($op == 0) { push @st, unpack('l<', substr($d, $caddr + 4 * ($pc + 1), 4)); $pc += 2; next }   # PUSHI
        if ($op == 1) { push @st, undef; $pc += 2; next }                         # PUSHF
        if ($op == 4) { push @st, $reg; $pc++; next }                             # PUSHREG
        if ($op == 34) { push @st, undef; $pc++; next }                           # PUSHSTR
        if ($op == 2 || $op == 3 || $op == 30 || $op == 31) { push @st, undef; $pc++; next }   # variables: unknown
        if ($op == 5 || $op == 6 || $op == 32 || $op == 33) { pop @st; $pc++; next }
        if ($op >= 14 && $op <= 27) {                                             # arithmetic and comparisons
            if ($op == 18 || $op == 19) { my $a = pop @st; push @st, !defined $a ? undef : $op == 18 ? -$a : ($a ? 0 : 1); $pc++; next }
            my $b = pop @st; my $a = pop @st;
            if (!defined $a || !defined $b) { push @st, undef }
            elsif ($op == 22) { push @st, $a == $b ? 1 : 0 } elsif ($op == 23) { push @st, $a != $b ? 1 : 0 }
            elsif ($op == 20) { push @st, ($a || $b) ? 1 : 0 } elsif ($op == 21) { push @st, ($a && $b) ? 1 : 0 }
            else { push @st, undef }   # ADD.. and S/L/SE/LE: order unverified, unknown
            $pc++; next;
        }
        if ($op == 28) {                                                          # IF: jump when 0
            my $v = pop @st;
            if (!defined $v) {
                return ('unknown', "branch limit at $pc") if $depth > 12 || $visited->{$pc}++;
                my @a = explore($pc + 1, \@st, $reg, \@calls, $visited, $depth + 1);
                my @b = explore($labels[$u][1], \@st, $reg, \@calls, $visited, $depth + 1);
                return $RANK{$a[0]} >= $RANK{$b[0]} ? @a : @b;
            }
            $pc = $v ? $pc + 1 : $labels[$u][1]; next;
        }
        if ($op == 13) { $pc = $labels[$u][1]; return ('unknown', 'loop') if $seen{"g$pc"}++ > 50; next }   # GOTO
        if ($op == 10) { $pc = $procs[$u][1]; next }                              # JUMP
        if ($op == 11) { push @calls, $pc + 1; $pc = $procs[$u][1]; next }        # CALL
        if ($op == 9) { return ('inactive', "END at $pc") unless @calls; $pc = pop @calls; next }   # END
        if ($op == 8) {
            if ($u == 0x7) { my $f = pop @st; $reg = defined $f ? ($on{$f} ? 1 : 0) : undef; $pc++; next }
            if ($u == 0x8 || $u == 0x9) { my $f = pop @st; $on{$f} = $u == 0x8 ? 1 : 0 if defined $f; $pc++; next }   # flag set / clear: silent
            return ('inactive', "idle (frame wait 0xD at $pc)") if $u == 0xD;   # a polling loop with nothing to do now
            if ($PROMPT{$u}) { my $n = pop @st; return ('action', sprintf 'prompt %s via 0x%X at %d', $n // '?', $u, $pc) }
            my ($nop) = unpack 'v', substr($d, $caddr + 4 * ($pc + 1), 2);
            if ($nop == 4) { pop @st; $reg = undef; $pc++; next }               # a query: its result is read next
            return ('story', sprintf 'COMM 0x%X at %d', $u, $pc);
        }
        return ('unknown', "op $op at $pc");
    }
    return ('unknown', 'too long');
}
