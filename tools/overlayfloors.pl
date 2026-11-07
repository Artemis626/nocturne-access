#!/usr/bin/perl
# Floor-like polygons that carry a marker attribute (0x800, 0x8000, ...), checked against the ground floors (Attr 0 or 0x80)
# of a navigation dump: for each, the share of its area that no ground floor covers within BAND (default 30) of its height.
# 0.0.161: Mifunashiro's Third Stratum (field 35 area 7) joins its upper and lower halves only by a ramp of Attr 0x800 (#185);
# the walking map takes no marked polygon as floor, so the lower half was "not reachable" until the player walked down.
# Usage: perl tools/overlayfloors.pl <navdump> [band]
use strict; use warnings;
my ($file, $band) = @ARGV; $band //= 30; my $S = 30;
open my $fh, '<', $file or die "$file: $!";
my (@ground, @marked);
while (<$fh>) {
    next unless /^  #(\d+) .* n (\S+) (\S+) (\S+) .*attr (0x[0-9a-f]+).*\| pts (.*)$/;
    my ($id, $ny, $attr, $pts) = ($1, $3, hex($5), $6);
    next if abs($ny) < 0.5;
    my @p = map { [ (split ' ')[0 .. 2] ] } grep { /\S/ } split /\|/, $pts;
    next if @p < 3;
    my $poly = { id => $id, attr => $attr, p => \@p };
    if ($attr == 0 || $attr == 0x80) { push @ground, $poly } else { push @marked, $poly }
}
sub inside { my ($q, $x, $z) = @_; my @p = @{ $q->{p} }; my $in = 0;
    for (my $i = 0, my $j = $#p; $i < @p; $j = $i++) { my ($xi, $zi, $xj, $zj) = ($p[$i][0], $p[$i][2], $p[$j][0], $p[$j][2]);
        $in = !$in if (($zi > $z) != ($zj > $z)) && ($x < ($xj - $xi) * ($z - $zi) / (($zj - $zi) || 1e-9) + $xi); }
    return $in; }
sub height { my ($q, $x, $z) = @_; my @p = @{ $q->{p} }; my ($a, $b, $c) = @p[0 .. 2];
    my ($ux, $uy, $uz) = ($b->[0] - $a->[0], $b->[1] - $a->[1], $b->[2] - $a->[2]); my ($vx, $vy, $vz) = ($c->[0] - $a->[0], $c->[1] - $a->[1], $c->[2] - $a->[2]);
    my ($nx, $ny, $nz) = ($uy * $vz - $uz * $vy, $uz * $vx - $ux * $vz, $ux * $vy - $uy * $vx); return $a->[1] if abs($ny) < 1e-9;
    return $a->[1] - ($nx * ($x - $a->[0]) + $nz * ($z - $a->[2])) / $ny; }
printf "%d ground floors, %d marked floors (band %d)\n", scalar @ground, scalar @marked, $band;
for my $q (@marked) {
    my @xs = map { $_->[0] } @{ $q->{p} }; my @zs = map { $_->[2] } @{ $q->{p} }; my @ys = map { $_->[1] } @{ $q->{p} };
    my ($x0, $x1) = (sort { $a <=> $b } @xs)[0, -1]; my ($z0, $z1) = (sort { $a <=> $b } @zs)[0, -1]; my ($y0, $y1) = (sort { $a <=> $b } @ys)[0, -1];
    my ($n, $bare, $nofloor) = (0, 0, 0);
    for (my $x = $x0 + $S / 2; $x < $x1; $x += $S) { for (my $z = $z0 + $S / 2; $z < $z1; $z += $S) {
        next unless inside($q, $x, $z); $n++; my $h = height($q, $x, $z);
        my @under = grep { inside($_, $x, $z) } @ground;
        $bare++ unless grep { abs(height($_, $x, $z) - $h) <= $band } @under;
        $nofloor++ unless @under; } }
    next unless $n;
    printf "#%-4d attr 0x%04x  x %6.0f..%6.0f z %6.0f..%6.0f y %5.0f..%5.0f  %5d samples, %3.0f%% with no ground floor at its height, %3.0f%% with no ground floor at all\n",
        $q->{id}, $q->{attr}, $x0, $x1, $z0, $z1, $y0, $y1, $n, 100 * $bare / $n, 100 * $nofloor / $n;
}
