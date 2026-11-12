#!/usr/bin/perl
# One-way door maze of the Fourth Kalpa as grid cells: each door, its room cell, the area and cell it lands in.
# Usage: perl tools/unity/mazecells.pl research/gamedata/f044 19,20,21,22 [cell] (field 44 file names; docs/FOURTH-KALPA-PLAN.md).
use strict; use warnings;
my ($dir, $areas, $cell) = @ARGV; $cell //= 1800;
my %pts;
for my $a (0 .. 30) { my $f = sprintf "%s/f044_%03d.f2", $dir, $a + 1; next unless -e $f;
    for (`perl tools/unity/f2pos.pl $f`) { my ($n, $x, $y, $z) = split; $pts{$a}{$n} = [$x, $z] if defined $z; } }
my %done; my %want = map { $_ => 1 } split /,/, $areas;
sub c { my ($x, $z) = @_; return sprintf "(%d,%d)", int($x / $cell + ($x < 0 ? -0.5 : 0.5)), int($z / $cell + ($z < 0 ? -0.5 : 0.5)); }
open my $w, '<', "$dir/wap.txt" or die;
while (<$w>) {
    m/movetype (\d+) flagmode \d+ flag (\d+) from area\+1 (\d+) "([^"]+)".* to field (\d+) area\+1 (\d+) .* at "([^"]*)"/ or next;
    my ($mt, $flag, $from, $door, $tf, $to, $at) = ($1, $2, $3 - 1, $4, $5, $6 - 1, $7);
    next unless $want{$from}; next if $done{"$from $door"}++;
    my $dest = $to < 0 ? $from : $to; my $d = $pts{$from}{$door}; my $l = $pts{$dest}{$at};
    next unless $d;
    my $src = $l && $dest == $from ? c(2 * $d->[0] - $l->[0], 2 * $d->[1] - $l->[1]) : c(@$d);
    printf "area %2d %-7s cell %-8s -> area %2d %-9s cell %s%s%s\n", $from, $door, $src, $dest, $at, $l ? c(@$l) : "?", $mt != 1 ? " movetype $mt" : "", $flag ? " flag $flag" : "";
}
