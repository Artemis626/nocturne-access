#!/usr/bin/perl
# Way-on lines from the door table alone (21 Nov 2026, Yurakucho Tunnel: its area files hold only part of the collision the game
# builds, so waybfs.pl's floods fail). Builds the directed graph of areas from wap.txt (tools/unity/wap.pl), measures each area's
# distance in doors to the goal volume, and prints one Data/WayOn.txt line per area: the volumes leaving it, nearest to the goal
# first (ties in table order). Same-area doors and ladders are kept after them when they exist (the live map decides). In game the
# way on takes the first listed point the player can reach, so walls the files lack are the live map's business.
# Args: wap.txt field goal-area goal-volume
# Env: SKIP="a:vol ..." leaves volumes out; SHOW=1 prints the distances.
use strict; use warnings;
my ($wap, $field, $ga, $gv) = @ARGV;
die "usage: waygraph.pl wap.txt field goal-area goal-volume\n" unless defined $gv;
my %skip = map { $_ => 1 } split ' ', ($ENV{SKIP} // '');
my (%out, @order, %flag);
open my $f, '<', $wap or die;
while (<$f>) {
  my ($mt, $fm, $fl, $fa, $vol, $tf, $ta) = /movetype (\d+) flagmode (\d+) flag (\d+) from area\+1 (\d+) "([^"]*)".*to field (\d+) area\+1 (\d+)/ or next;
  next if $vol eq '' || $tf != 0;           # nameless script rows, other fields
  my ($a, $d) = ($fa - 1, $ta - 1); $d = $a if $d < 0;   # area+1 0: the same area (a door to a point here)
  next if $skip{"$a:$vol"};
  push @{ $out{$a} }, [$vol, $d, $mt];
  push @order, $a unless grep { $_ == $a } @order;
  $flag{"$a:$vol"} = "$fm/$fl" if $fl;
}
# Distances to the goal area by reversed edges between areas.
my %into; for my $a (keys %out) { push @{ $into{$_->[1]} }, $a for grep { $_->[1] != $a } @{ $out{$a} } }
my %dist = ($ga => 0); my @q = ($ga);
while (defined(my $n = shift @q)) { for my $p (@{ $into{$n} // [] }) { next if exists $dist{$p}; $dist{$p} = $dist{$n} + 1; push @q, $p } }
for my $A (sort { $a <=> $b } keys %out) {
  next unless exists $dist{$A};
  my @c = $A == $ga ? ([$gv, -1]) : ();
  my $i = 0;
  for my $e (@{ $out{$A} }) { my $d = $e->[1]; next if $d == $A || !exists $dist{$d} || $dist{$d} >= $dist{$A}; push @c, [$e->[0], $dist{$d} + $i++ / 1000] }
  my @s = sort { $a->[1] <=> $b->[1] } @c;
  my %seen; my @names = grep { !$seen{$_}++ } map { $_->[0] } @s;
  next unless @names;
  my $show = join(", ", map { "$_->[0]>a$_->[1]" . ($flag{"$A:$_->[0]"} ? " flag " . $flag{"$A:$_->[0]"} : "") } @{ $out{$A} });
  printf "%s %d %s%s
", $field, $A, join(" ", @names), $ENV{SHOW} ? "   # dist $dist{$A}; $show" : "";
}
