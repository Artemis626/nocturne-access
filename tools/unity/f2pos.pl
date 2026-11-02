# Lists named points of an area file (fNNN_AAA.f2): the name, then x y z (floats) 16 bytes after the name's start.
use strict; use warnings;
local $/; open my $f, '<:raw', $ARGV[0] or die; my $d = <$f>; close $f;
my %seen;
while ($d =~ /((?:\d\d\d_)?\d\d(?:d|pos|eve|npc|cam)_\d\d)\0/g) {
    my ($n, $at) = ($1, $-[1]);
    next if $seen{"$n $at"}++;
    next if $at + 28 > length $d;
    my ($x, $y, $z) = unpack 'f<3', substr($d, $at + 16, 12);
    printf "%-10s %8.0f %6.0f %8.0f  @%x\n", $n, $x, $y, $z, $at;
}
