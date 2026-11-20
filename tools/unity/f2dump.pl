# Writes a navigation-dump-like file from an area file (fNNN_AAA.f2) so tools/navregions.pl can flood it offline:
# collision blocks (nverts, nfaces, 0, vertex ptr, face ptr, next; vertices x y z w floats, faces 36 bytes: attr, ?, ?, 4 indices,
# ?, ?), points of interest = every named point in the file. Args: f2 field area player-x player-z [player-y].
use strict; use warnings;
my ($file, $field, $area, $px, $pz, $py) = @ARGV; $py //= 0;
local $/; open my $f, '<:raw', $file or die; my $d = <$f>; close $f;
my $L = length $d; my $id = 0; my @lines;
for (my $b = 0; $b + 24 <= $L; $b += 4) {
  my ($nv, $nf, $z, $vp, $fp, $nx) = unpack 'l<6', substr($d, $b, 24);
  next unless $z == 0 && $nv > 0 && $nf > 0 && $nv < 100000 && $vp == $b + 0x20 && $fp == $vp + 16 * $nv && $nx >= $fp + 36 * $nf && $nx < $fp + 36 * $nf + 16 && $nx <= $L;
  my @v = map { [unpack 'f<3', substr($d, $vp + 16 * $_, 12)] } 0 .. $nv - 1;
  for my $k (0 .. $nf - 1) {
    my @r = unpack 'l<9', substr($d, $fp + 36 * $k, 36);
    my @ix = @r[3 .. 6]; my @p = map { $v[$_] } grep { $_ >= 0 && $_ < $nv } @ix;
    my %u; @p = grep { !$u{join ',', @$_}++ } @p; next if @p < 3;
    my @e1 = map { $p[1][$_] - $p[0][$_] } 0 .. 2; my @e2 = map { $p[2][$_] - $p[0][$_] } 0 .. 2;
    my @n = ($e1[1] * $e2[2] - $e1[2] * $e2[1], $e1[2] * $e2[0] - $e1[0] * $e2[2], $e1[0] * $e2[1] - $e1[1] * $e2[0]);
    my $len = sqrt($n[0] ** 2 + $n[1] ** 2 + $n[2] ** 2) || 1; @n = map { $_ / $len } @n;
    push @lines, sprintf "  #%d mode2D 0 edges %d n %.3f %.3f %.3f 0 num 0 min [0 0] max [0 0] block %d hit 0 res 0 hoji 0 npc -1 elv 0 0 0 0 | attr 0x%x myuka 0 | pts %s",
      $id++, scalar @p, @n, $b, $r[0], join(' | ', map { sprintf '%g %g %g 1', @$_ } @p);
    $b = $nx - 4 if 0;
  }
}
# BASEPLANE=1 (20 Nov 2026, the Fifth Kalpa, field 45): the game adds a flat floor at y 0 over the bounding box of the area's
# polygons (live dump 20 Nov 13:35:45, area 8: plane #0 mode2D 1, min [4400 -2200] max [9400 2200] = the f2 polygons' bounds);
# those areas' files hold walls and trigger sheets only, so without it the flood finds no floor.
if ($ENV{BASEPLANE} && @lines) {
  my ($x0, $x1, $z0, $z1);
  for (@lines) { while (/(-?[\d.e+-]+) (-?[\d.e+-]+) (-?[\d.e+-]+) 1(?: \||$)/g) { my ($x, $z) = ($1, $3);
    $x0 = $x if !defined $x0 || $x < $x0; $x1 = $x if !defined $x1 || $x > $x1; $z0 = $z if !defined $z0 || $z < $z0; $z1 = $z if !defined $z1 || $z > $z1 } }
  unshift @lines, sprintf "  #%d mode2D 1 edges 4 n 0 -1 0 0 num 0 min [%g %g] max [%g %g] block 0 hit 1 res 65537 hoji 0 npc -1 elv 0 0 0 0 | attr 0x0 myuka 0 | pts %g 0 %g 1 | %g 0 %g 1 | %g 0 %g 1 | %g 0 %g 1",
    $id++, $x0, $z0, $x1, $z1, $x0, $z1, $x0, $z0, $x1, $z0, $x1, $z1;
}
print "Synthetic dump from $file\n== Player\nfieldID $field areaID $area blockID 0\nplayer $px $py $pz; old\n== Collision\n";
print "$_\n" for @lines;
print "== Points of interest and walk grid\n";
my %seen; my $g = 0;
while ($d =~ /((?:\d\d\d_)?\d\d(?:d|pos|eve|npc)_\d\d)\0/g) { my ($n, $at) = ($1, $-[1]); next if $seen{$n}++ || $at + 28 > $L;
  my ($x, $y, $z) = unpack 'f<3', substr($d, $at + 16, 12);
  next if abs($x) > 1e6 || abs($z) > 1e6;   # a script procedure's name ("001_01eve_01" in f045), not a point
  printf "  gEvnRng %d point \"%s\" at %.0f %.0f d 0\n", $g++, $n, $x, $z; }
