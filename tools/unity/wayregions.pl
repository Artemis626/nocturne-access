#!/usr/bin/perl
# Way-on lines by the parts of each area (22 Nov 2026, Yurakucho Tunnel: waygraph.pl took every area as one room, and area 13's
# ladder from area 3 lands in a sealed pocket, so the way on named a ladder in another part, "beyond doors"). Floods each area
# file offline (tools/unity/f2dump.pl, tools/navregions.pl), gives every door volume and landing point its walkable part
# (region), builds the graph of parts from the door table (wap.txt from tools/unity/wap.pl: a volume's part to its landing's part,
# same-area doors included), measures each part's distance in doors to the goal volume, and prints one Data/WayOn.txt line per
# area: for every part, nearest to the goal first, the volumes leaving it toward the goal. In game the way on takes the first
# listed point the player can reach, walking or through same-area doors, so each part finds its own exit.
# An area whose file floods no part around a point (missing collision) falls back to one part for that point: "*".
# Args: wap.txt dir-with-fNNN_AAA.f2 field goal-area goal-volume
# Env: SHOW=1 prints parts and distances as comments; BASEPLANE=1 passed to f2dump; KEEP=dir keeps the floods.
use strict; use warnings;
use File::Temp qw(tempdir);
my ($wap, $dir, $field, $ga, $gv) = @ARGV;
die "usage: wayregions.pl wap.txt dir field goal-area goal-volume\n" unless defined $gv;
my $tmp = $ENV{KEEP} // tempdir(CLEANUP => 1);
my $tools = $0; $tools =~ s{[\\/][^\\/]+$}{}; my $nav = "$tools/../navregions.pl";
my $fff = sprintf "f%03d", $field;

# 1. Doors.
my (@rows, %areas);
open my $f, '<', $wap or die "$wap: $!";
while (<$f>) {
  my ($mt, $fa, $vol, $tf, $ta, $at) = /movetype (\d+) .*from area\+1 (\d+) "([^"]*)".*to field (\d+) area\+1 (\d+) u10 \S+ at "([^"]*)"/ or next;
  next if $vol eq '';
  my $a = $fa - 1; my $d = $tf == 0 ? ($ta == 0 ? $a : $ta - 1) : -1;
  push @rows, { a => $a, vol => $vol, d => $d, at => $at, tf => $tf, mt => $mt };
  $areas{$a} = 1; $areas{$d} = 1 if $d >= 0;
}

# 1b. Slides and other scripted moves (22 Nov 2026: the tunnel's prompt-less 01eve volumes, "You slide down too?"): a volume's
# procedure NNN_name (NNN = area + 1) that calls CALL_FIELD2 (COMM 0x98: PUSHSTR point, PUSHIS area + 1) moves the player there.
my $flow = sprintf "%s/%s.flow.txt", $dir, $fff;
if (open my $fl, '<', $flow) {
  my ($pa, $pn, $str, $int);
  while (<$fl>) {
    if (/^procedure (\d{3})_(\S+)/) { ($pa, $pn) = ($1 - 1, $2); next }
    if (/^procedure /) { $pa = undef; next }
    next unless defined $pa;
    if (/PUSHSTR "([^"]*)"/) { $str = $1 } elsif (/PUSHIS (\d+)/) { $int = $1 }
    elsif (/COMM 0x98\b/ && defined $str && $int) {
      push @rows, { a => $pa, vol => $pn, d => $int - 1, at => $str, tf => 0, mt => 'script' };
      $areas{$int - 1} = 1;
      print "# script move: area $pa $pn to area ", $int - 1, " $str\n" if $ENV{SHOW};
    }
  }
}

# 2. Parts of each area.
my %whole; my %part;   # "a:name" => part id ("a:R5" or "a:*name")
for my $a (sort { $a <=> $b } keys %areas) {
  my $f2 = sprintf "%s/%s_%03d.f2", $dir, $fff, $a + 1;
  next unless -f $f2;
  my $dump = "$tmp/a$a.txt";
  system("perl \"$tools/f2dump.pl\" \"$f2\" $field $a 0 0 > \"$dump\" 2>/dev/null");
  my @out = `NAVREGR=150 perl "$nav" "$dump" 2>&1`;
  my ($x0, $z0); my %ext; my $outer = '';
  for (@out) {
    ($x0, $z0) = ($1, $2) if /grid \d+ x \d+ from (-?\d+) (-?\d+)/;
    $ext{$1} = [$2, $4] if /^\s+R(\d+): \d+ places, x (-?\d+)\.\.(-?\d+), z (-?\d+)/;
  }
  for my $r (keys %ext) { $outer = $r if defined $x0 && abs($ext{$r}[0] - $x0) <= 31 && abs($ext{$r}[1] - $z0) <= 31 }
  my %uf; my $find; $find = sub { my $x = shift; $uf{$x} //= $x; $uf{$x} eq $x ? $x : ($uf{$x} = $find->($uf{$x})) };
  my %regs;
  my $in = 0;
  for (@out) {
    $in = 1 if /^points of interest/; $in = 0 if /^warp landings|^player/;
    next unless $in && /^\s{2}(\S+)\s+point\s+-?\d+\s+-?\d+\s+(.*)$/;
    my ($name, $rest) = ($1, $2);
    my @r = grep { $_ ne $outer } ($rest =~ /R(\d+)\(/g);
    $regs{$name} = [@r];
    for my $i (1 .. $#r) { my ($p, $q) = ($find->($r[0]), $find->($r[$i])); if ($p ne $q) { $uf{$q} = $p; print "# area $a: $name joins R$r[0] and R$r[$i]\n" if $ENV{SHOW} } }
  }
  # Trusted only when every door volume and landing of this area has a part; otherwise the area is one part ("a:*").
  # A script move whose volume is not in the area file is a leftover procedure (area 8's 009_01eve_03 and 06, 22 Nov 2026): dropped.
  @rows = grep { !($_->{mt} eq 'script' && $_->{a} == $a && !exists $regs{$_->{vol}}) } @rows;
  my @needed = (map { $_->{vol} } grep { $_->{a} == $a } @rows), (map { $_->{at} } grep { $_->{d} == $a } @rows);
  my @lost = grep { !$regs{$_} || !@{ $regs{$_} } } @needed;
  if (@lost) { print "# area $a: one part (no floor part for @lost)\n" if $ENV{SHOW}; $whole{$a} = 1; next }
  for my $name (keys %regs) { my @r = @{ $regs{$name} }; $part{"$a:$name"} = "$a:R" . $find->($r[0]) if @r }
}
my $partOf = sub { my ($a, $name) = @_; $whole{$a} || !$part{"$a:$name"} ? "$a:*" : $part{"$a:$name"} };

# 3. Graph of parts and distances to the goal.
my (%into, %edges);
my $goal = $partOf->($ga, $gv);
for my $r (@rows) {
  next if $r->{d} < 0;
  my $s = $partOf->($r->{a}, $r->{vol}); my $t = $partOf->($r->{d}, $r->{at});
  next if $s eq $t;
  push @{ $edges{$s} }, [$r->{vol}, $t]; push @{ $into{$t} }, $s;
}
my %dist = ($goal => 0); my @q = ($goal);
while (defined(my $n = shift @q)) { for my $p (@{ $into{$n} // [] }) { next if exists $dist{$p}; $dist{$p} = $dist{$n} + 1; push @q, $p } }

# 4. One line per area: parts nearest first, each part's exits that bring it nearer (same-area doors too: a part may need one).
my %byArea; for my $p (keys %dist) { my ($a) = $p =~ /^(\d+):/; push @{ $byArea{$a} }, $p }
for my $A (sort { $a <=> $b } keys %byArea) {
  my @names; my @show;
  for my $p (sort { $dist{$a} <=> $dist{$b} || $a cmp $b } @{ $byArea{$A} }) {
    my @c = $p eq $goal ? ($gv) : ();
    for my $e (@{ $edges{$p} // [] }) { push @c, $e->[0] if exists $dist{$e->[1]} && $dist{$e->[1]} < $dist{$p} }
    push @show, "$p d$dist{$p}: " . join(" ", map { my $e = $_; "$e->[0]>$e->[1]" . (exists $dist{$e->[1]} ? "d$dist{$e->[1]}" : "") } @{ $edges{$p} // [] });
    push @names, @c;
  }
  my %seen; @names = grep { !$seen{$_}++ } @names;
  next unless @names;
  print "# $_\n" for $ENV{SHOW} ? @show : ();
  print "$field $A @names\n";
}
my @unreached = grep { !exists $dist{$_} } sort keys %edges;
if ($ENV{SHOW} && @unreached) {
  print "# parts with no way to the goal:\n";
  print "#   $_: " . join(" ", map { "$_->[0]>$_->[1]" . (exists $dist{$_->[1]} ? "d$dist{$_->[1]}" : "") } @{ $edges{$_} }) . "\n" for @unreached;
}
