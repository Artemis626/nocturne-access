# Simulates an area of switching walls offline (27 Nov 2026, the Kagutsuchi Tower's upper floors, field 36 areas 12 to 14):
# flat tiles (event planes) whose field-script procedures turn held objects' collision on and off (FLD_ATARI_ON / OFF,
# COMM 0x107 / 0x108) and story flags (BIT_ON / OFF), choosing a branch by the side the player comes from
# (fldCommand_DIR_HIT_CNTR, COMM 0x5F: 0 when the player stands on the side the plane's normal points to).
#
# The walls come from the area file (tools/unity/f2dump.pl: the static block first, then one block per atari_hoji_NN in
# order, checked against the game's dumps of areas 13 and 14); the tiles' planes from tools/unity/f2vol.pl; the procedures from
# the disassembly (tools/unity/flowscript.pl), run by a small interpreter (PUSH*, COMM with the arities of
# research/gamedata/commands.txt, EQ, IF, GOTO, END).
#
# The player is a point on a 30-unit grid, clear of active walls by 46 (WalkGrid's clearance), walking on the area's floor at
# y 0. A tile fires when the player walks through its plane (cells within ZONE of it, default 20; a tile standing on a closed
# wall cannot fire: 27 Nov 2026, 01eve_21); walks avoid every other tile's zone,
# as the mod's way on prefers walks that cross no story event.
#
# Modes:
#   BFS (default): the shortest chain of tiles to GOAL (x z): prints it.
#   POLICY="name name ...": the way on's rule (the first listed point walkable crossing no other tile), followed from the start
#   state until the goal or a loop; prints each choice.
# Args: f2 flow prefix start-x start-z goal-x goal-z [hoji ids off at the start]. Env: FLAGS="1650" (story flags on), ZONE,
# GOALR (default 150), TRACE=1.
use strict; use warnings;
my ($f2, $flow, $prefix, $sx, $sz, $gx, $gz, @off) = @ARGV;
die "args: f2 flow prefix sx sz gx gz [off...]\n" unless defined $gz;
my $dir = $0; $dir =~ s{[\\/][^\\/]+$}{};
my $C = 30; my $Clear = 46; my $GoalR = $ENV{GOALR} // 150;
my $Zone = $ENV{ZONE} // 20;   # a tile fires only when walked through (0.0.208; 75 was wrong: Home stopped 60 short of 01eve_21)

# ---- Walls ----
my (@walls, %blockOrder, @floors);
open my $fd, '-|', "perl \"$dir/f2dump.pl\" \"$f2\" 0 0 0 0" or die;
my $nb = -1; my %bi;
while (<$fd>) {
  next unless /#\d+ mode2D \d+ edges \d+ n (\S+) (\S+) (\S+) .*? block (\d+) .*?attr (0x[0-9a-f]+) .*\| pts (.*)$/;
  my ($nx, $ny, $nz, $blk, $attr, $pts) = ($1, $2, $3, $4, hex $5, $6);
  $bi{$blk} //= ++$nb;
  my @p = map { [split ' '] } split /\s*\|\s*/, $pts;
  if (abs($ny) >= 0.5) { push @floors, [$attr, @p] if $attr == 0 || $attr == 1; next }
  next if abs($ny) >= 0.2 || ($attr & 0xFB40);
  my ($ylo, $yhi) = (1e9, -1e9); $ylo = $_->[1] < $ylo ? $_->[1] : $ylo for @p; $yhi = $_->[1] > $yhi ? $_->[1] : $yhi for @p;
  next unless $ylo <= -35 && $yhi >= -35 + 0;   # the game tests walls 35 above the floor at y 0
  # the wall's horizontal segment: the two points farthest apart in x z
  my ($a, $b, $best) = (0, 0, -1);
  for my $i (0 .. $#p) { for my $j ($i + 1 .. $#p) { my $d = ($p[$i][0] - $p[$j][0]) ** 2 + ($p[$i][2] - $p[$j][2]) ** 2; ($a, $b, $best) = ($i, $j, $d) if $d > $best } }
  push @walls, { blk => $bi{$blk}, x0 => $p[$a][0], z0 => $p[$a][2], x1 => $p[$b][0], z1 => $p[$b][2] };
}
close $fd;
# block 0 static, block k = atari_hoji_k for k up to the number of atari names in the file
my $atari = 0; { local $/; open my $fh, '<:raw', $f2 or die; my $d = <$fh>; $atari++ while $d =~ /atari_hoji_\d\d\0/g; }
my $static = 0; my $hojiWalls = 0; my $other = 0;
# HOJISKIP=N: N blocks between the static one and atari_hoji_01 (area 12, f036_013: the lift's 0x80 floor comes first)
my $skip = $ENV{HOJISKIP} // 0;
for my $w (@walls) { my $h = $w->{blk} - $skip;
  if ($w->{blk} == 0) { $w->{hoji} = 0; $static++ } elsif ($w->{blk} > $skip && $h <= $atari) { $w->{hoji} = $h; $hojiWalls++ } else { $w->{hoji} = -1; $other++ } }
@walls = grep { $_->{hoji} >= 0 } @walls;
printf "walls: %d static, %d on %d held objects (%d in other blocks left out)\n", $static, $hojiWalls, $atari, $other;

# ---- Grid ----
my ($X0, $X1, $Z0, $Z1) = (1e9, -1e9, 1e9, -1e9);
for my $w (@walls) { for ([$w->{x0}, $w->{z0}], [$w->{x1}, $w->{z1}]) { $X0 = $_->[0] if $_->[0] < $X0; $X1 = $_->[0] if $_->[0] > $X1; $Z0 = $_->[1] if $_->[1] < $Z0; $Z1 = $_->[1] if $_->[1] > $Z1 } }
my $NI = int(($X1 - $X0) / $C) + 1; my $NJ = int(($Z1 - $Z0) / $C) + 1;
sub cx { $X0 + ($_[0] + 0.5) * $C } sub cz { $Z0 + ($_[0] + 0.5) * $C }
sub segd { my ($px, $pz, $x0, $z0, $x1, $z1) = @_; my ($dx, $dz) = ($x1 - $x0, $z1 - $z0); my $l = $dx * $dx + $dz * $dz;
  my $t = $l ? (($px - $x0) * $dx + ($pz - $z0) * $dz) / $l : 0; $t = 0 if $t < 0; $t = 1 if $t > 1;
  return sqrt(($px - $x0 - $t * $dx) ** 2 + ($pz - $z0 - $t * $dz) ** 2) }
sub onfloor { my ($x, $z) = @_; for my $f (@floors) { my ($attr, @p) = @$f; my ($a0, $a1, $b0, $b1) = (1e9, -1e9, 1e9, -1e9);
    for (@p) { $a0 = $_->[0] if $_->[0] < $a0; $a1 = $_->[0] if $_->[0] > $a1; $b0 = $_->[2] if $_->[2] < $b0; $b1 = $_->[2] if $_->[2] > $b1 }
    next unless abs($p[0][1]) <= 60; return 1 if $x >= $a0 && $x <= $a1 && $z >= $b0 && $z <= $b1 } 0 }
# per cell: blocked by static walls, or the set of hojis whose walls are near
my (@static, @near, @floor);
for my $i (0 .. $NI - 1) { for my $j (0 .. $NJ - 1) { my $k = $i * $NJ + $j; my ($x, $z) = (cx($i), cz($j));
  $floor[$k] = onfloor($x, $z);
  for my $w (@walls) { next if segd($x, $z, $w->{x0}, $w->{z0}, $w->{x1}, $w->{z1}) >= $Clear;
    if ($w->{hoji} == 0) { $static[$k] = 1 } else { $near[$k]{$w->{hoji}} = 1 } } } }

# ---- Tiles ----
my %tile;
open my $fv, '-|', "perl \"$dir/f2vol.pl\" \"$f2\"" or die;
while (<$fv>) {
  next unless /^(01eve_\d\d)\s+(-?[\d.]+)\s+(-?[\d.]+) yaw\s+(-?[\d.]+) box t1 w\s+(\d+) d\s+\d+ ends (-?[\d.]+) (-?[\d.]+) (-?[\d.]+) (-?[\d.]+)/;
  my ($name, $x, $z, $yawd, $x0, $z0, $x1, $z1) = ($1, $2, $3, $4, $6, $7, $8, $9);
  next if $tile{$name};
  my $yaw = $yawd * 3.14159265358979 / 180;
  $tile{$name} = { name => $name, x => $x, z => $z, nx => -sin($yaw), nz => cos($yaw), x0 => $x0, z0 => $z0, x1 => $x1, z1 => $z1 };
}
# a volume with no procedure is idle (the mod lists no story event there): it neither fires nor stands in the way
unless ($ENV{KEEPIDLE}) { my %has; open my $fl, '<', $flow or die; while (<$fl>) { $has{$1} = 1 if /^procedure \Q$prefix\E_(\S+)/ } close $fl;
  delete $tile{$_} for grep { !$has{$_} } keys %tile }
close $fv;
for my $t (values %tile) { my @z; for my $i (0 .. $NI - 1) { for my $j (0 .. $NJ - 1) {
    push @z, $i * $NJ + $j if segd(cx($i), cz($j), $t->{x0}, $t->{z0}, $t->{x1}, $t->{z1}) < $Zone } } $t->{zone} = { map { $_ => 1 } @z } }
my %inZone; for my $t (values %tile) { $inZone{$_}{$t->{name}} = 1 for keys %{$t->{zone}} }

# ---- Procedures ----
my %arity; open my $fc, '<', "$dir/../../research/gamedata/commands.txt" or die "commands.txt: $!";
while (<$fc>) { $arity{lc $1} = $2 if /^(0x[0-9A-Fa-f]+) \S*\/(\d+)/ } close $fc;
my %proc; { open my $ff, '<', $flow or die; my $cur;
  while (<$ff>) { if (/^procedure (\S+)/) { $cur = $1; $proc{$cur} = { code => [], label => {} }; next } next unless $cur;
    if (/^\s*label (\S+):/) { $proc{$cur}{label}{$1} = scalar @{$proc{$cur}{code}}; next }
    push @{$proc{$cur}{code}}, [$1, $2] if /^\s*\d+: (\S+)\s*(.*)$/ } }
# runs prefix_name with dir and flags; returns the new off set and flags
sub run { my ($name, $dirv, $offh, $flagh) = @_; my %off = %$offh; my %fl = %$flagh;
  my $p = $proc{"${prefix}_$name"} or return (\%off, \%fl, 'no procedure');
  my @st; my $r = 0; my $pc = 0; my $n = 0; my @did;
  while ($pc < @{$p->{code}} && $n++ < 10000) { my ($op, $arg) = @{$p->{code}[$pc++]};
    if ($op =~ /^PUSHI/) { push @st, $arg + 0 } elsif ($op eq 'PUSHSTR') { $arg =~ /"(.*)"/; push @st, $1 }
    elsif ($op eq 'PUSHREG') { push @st, $r } elsif ($op eq 'EQ') { my $b = pop @st; my $a = pop @st; push @st, (($a // 0) eq ($b // 0)) ? 1 : 0 }
    elsif ($op eq 'IF') { my $c = pop @st; $pc = $p->{label}{$arg} unless $c }
    elsif ($op eq 'GOTO') { $pc = $p->{label}{$arg} } elsif ($op eq 'END') { last }
    elsif ($op eq 'COMM') { my $c = lc $arg; my $k = $arity{$c} // 0; my @a = splice @st, @st - $k; $r = 0;
      if ($c eq '0x5f') { $r = $dirv } elsif ($c eq '0x7') { $r = $fl{$a[0]} ? 1 : 0 }
      elsif ($c eq '0x8') { $fl{$a[0]} = 1; push @did, "flag $a[0] on" } elsif ($c eq '0x9') { delete $fl{$a[0]}; push @did, "flag $a[0] off" }
      elsif ($c eq '0x107' && $a[0] =~ /atari_hoji_(\d+)/) { delete $off{$1 + 0}; push @did, "wall " . ($1 + 0) . " on" }
      elsif ($c eq '0x108' && $a[0] =~ /atari_hoji_(\d+)/) { $off{$1 + 0} = 1; push @did, "wall " . ($1 + 0) . " off" } }
  }
  return (\%off, \%fl, join(', ', @did) || 'nothing') }

# ---- Flood ----
sub free { my ($k, $off) = @_; return 0 if $static[$k] || !$floor[$k]; if (my $h = $near[$k]) { for (keys %$h) { return 0 unless $off->{$_} } } 1 }
sub flood { my ($start, $off, $allow) = @_; my %seen = ($start => 1); my @q = ($start);
  while (@q) { my $k = shift @q; my ($i, $j) = (int($k / $NJ), $k % $NJ);
    for ([1, 0], [-1, 0], [0, 1], [0, -1]) { my ($a, $b) = ($i + $_->[0], $j + $_->[1]); next if $a < 0 || $b < 0 || $a >= $NI || $b >= $NJ;
      my $n = $a * $NJ + $b; next if $seen{$n} || !free($n, $off);
      if (my $z = $inZone{$n}) { next unless $allow && $z->{$allow} && keys %$z == 1 }
      $seen{$n} = 1; push @q, $n } }
  return \%seen }
sub cellOf { my ($x, $z) = @_; return int(($x - $X0) / $C) * $NJ + int(($z - $Z0) / $C) }
sub key { my ($off, $fl) = @_; join(',', sort { $a <=> $b } keys %$off) . '|' . join(',', sort keys %$fl) }
# the tiles the region touches, with the side: [name, cell, dir]
sub touches { my ($reg, $off) = @_; my %t;
  for my $k (keys %$reg) { my ($i, $j) = (int($k / $NJ), $k % $NJ);
    for ([1, 0], [-1, 0], [0, 1], [0, -1], [0, 0]) { my ($a, $b) = ($i + $_->[0], $j + $_->[1]); next if $a < 0 || $b < 0 || $a >= $NI || $b >= $NJ;
      my $n = $a * $NJ + $b; my $z = $inZone{$n} or next; next unless free($n, $off);
      for my $name (keys %$z) { my $t = $tile{$name}; my $s = (cx($i) - $t->{x}) * $t->{nx} + (cz($j) - $t->{z}) * $t->{nz};
        # DIR_HIT_CNTR is 1 on the side the normal points to: the procedures compare it with 1, and only this reading lets each
        # chain open the way it is walked (area 12's blocks from the north, area 14's 01eve_21 from the east)
        my $d = $s > 0 ? 1 : 0;
        $t{"$name/$d"} //= [$name, $n, $d] } } }
  return values %t }
sub reaches { my ($reg) = @_; for my $k (keys %$reg) { my ($i, $j) = (int($k / $NJ), $k % $NJ); return 1 if sqrt((cx($i) - $gx) ** 2 + (cz($j) - $gz) ** 2) <= $GoalR } 0 }

if ($ENV{DEBUGT}) { my $t = $tile{$ENV{DEBUGT}}; my @z = keys %{$t->{zone}}; my $fr = grep { free($_, {map { $_ => 1 } @off}) } @z; print "tile $ENV{DEBUGT}: zone @{[scalar @z]} cells, $fr free; ends $t->{x0} $t->{z0} $t->{x1} $t->{z1}
" }
my %off0 = map { $_ => 1 } @off; my %fl0 = map { $_ => 1 } split ' ', ($ENV{FLAGS} // '');
my $s0 = cellOf($sx, $sz); die "start cell not free\n" unless free($s0, \%off0);
printf "grid %d x %d from %d %d; %d tiles; start %s, goal %s %s\n", $NI, $NJ, $X0, $Z0, scalar keys %tile, "$sx $sz", $gx, $gz;

if (my $pol = $ENV{POLICY}) {
  my @list = split ' ', $pol; my ($pos, $off, $fl) = ($s0, \%off0, \%fl0); my %been; my $allow;
  for my $step (1 .. 60) {
    my $reg = flood($pos, $off, $allow);
    if (reaches($reg)) { print "step $step: the goal is walkable. Done.\n"; exit 0 }
    my %touch = map { ($_->[0] => $_) } touches($reg, $off);
    my $pick; for my $name (@list) { if ($name eq 'GOAL') { next } if ($touch{$name}) { $pick = $touch{$name}; last }
      # the mod's walk test reaches a tile from within NEAR (default 90) of its plane even through a closed wall (27 Nov 2026:
      # 01eve_21 on hoji 13's wall was "walkable", Home stopped 60 short and the game never fired it): a listed tile so near
      # but not crossable would be chosen first
      my $t = $tile{$name} or next; my $near = $ENV{NEAR} // 90;
      for my $k (keys %$reg) { my ($i, $j) = (int($k / $NJ), $k % $NJ);
        if (segd(cx($i), cz($j), $t->{x0}, $t->{z0}, $t->{x1}, $t->{z1}) < $near) { print "step $step: WARNING $name lies within $near of the region but cannot be crossed: the mod may choose it\n"; last } } }
    unless ($pick) { print "step $step: no listed tile walkable; touchable: ", join(' ', sort keys %touch), "\n"; exit 1 }
    my ($name, $cell, $d) = @$pick; my ($noff, $nfl, $did) = run($name, $d, $off, $fl);
    print "step $step: $name (side ", ($d ? 'normal side' : 'back side'), " its normal): $did\n";
    my $k = key($noff, $nfl) . "|$name"; if ($been{$k}++) { print "loop.\n"; exit 1 }
    ($pos, $off, $fl, $allow) = ($cell, $noff, $nfl, $name);
  }
  exit 1;
}

# TABLE=1 (0.0.210): every state reachable from the start (and from STARTS="x z off,off;x z off" more), each with the tile to
# cross next on a shortest way to the goal. Rows for the mod's Data/TilePuzzles.txt: the off set restricted to the hojis the
# procedures switch, the tile, the side (1: the normal side), its plane's centre and normal. Goal states get no row.
if ($ENV{TABLE}) {
  my @starts = ([$s0, \%off0]);
  for (split /;/, ($ENV{STARTS} // '')) { my ($x, $z, $o) = split ' ', $_, 3; my %o = map { $_ => 1 } split /[, ]/, ($o // '');
    my $c = cellOf($x, $z); die "start $x $z not free\n" unless free($c, \%o); push @starts, [$c, \%o] }
  my (%node, %edges, @goal, %sw); my @q = map { [$_->[0], $_->[1], {%fl0}, undef] } @starts;
  while (@q) { my ($pos, $off, $fl, $allow) = @{shift @q};
    my $reg = flood($pos, $off, $allow); my $min = (sort { $a <=> $b } keys %$reg)[0]; my $k = key($off, $fl) . "|$min";
    next if $node{$k}; $node{$k} = { off => $off, fl => $fl, reg => $reg };
    if (reaches($reg)) { push @goal, $k; next }
    for my $t (touches($reg, $off)) { my ($name, $cell, $d) = @$t; my ($noff, $nfl) = run($name, $d, $off, $fl);
      $sw{$_} = 1 for grep { ($off->{$_} // 0) != ($noff->{$_} // 0) } (keys %$off, keys %$noff);
      my $nreg = flood($cell, $noff, $name); my $nmin = (sort { $a <=> $b } keys %$nreg)[0];
      push @{$edges{$k}}, [$name, $d, key($noff, $nfl) . "|$nmin", $cell]; push @q, [$cell, $noff, $nfl, $name] } }
  my %dist = map { $_ => 0 } @goal; my $changed = 1;
  while ($changed) { $changed = 0; for my $k (keys %edges) { for my $e (@{$edges{$k}}) { next unless defined $dist{$e->[2]};
        my $d = $dist{$e->[2]} + 1; if (!defined $dist{$k} || $d < $dist{$k}) { $dist{$k} = $d; $changed = 1 } } } }
  my @swl = sort { $a <=> $b } keys %sw;
  printf "# %d states, %d reach the goal, %d dead ends\nhojis %s\n", scalar keys %node, scalar grep({ defined $dist{$_} } keys %node),
    scalar grep({ !defined $dist{$_} } keys %node), join(' ', @swl);
  my %row;
  for my $k (sort keys %edges) { next unless defined $dist{$k} && $dist{$k} > 0;
    my ($best) = sort { $dist{$a->[2]} <=> $dist{$b->[2]} || $a->[0] cmp $b->[0] } grep { defined $dist{$_->[2]} } @{$edges{$k}};
    my $o = join(',', grep { $node{$k}{off}{$_} } @swl) || '-'; my $t = $tile{$best->[0]};
    my $fl = join(',', sort keys %{$node{$k}{fl}});
    # a standing point: the region's cell nearest the tile's centre (the mod checks the player is in the same walkable part)
    my ($sc) = sort { (cx(int($a / $NJ)) - $t->{x}) ** 2 + (cz($a % $NJ) - $t->{z}) ** 2 <=> (cx(int($b / $NJ)) - $t->{x}) ** 2 + (cz($b % $NJ) - $t->{z}) ** 2 } keys %{$node{$k}{reg}};
    $row{sprintf "%s %s %s %d %.0f %.0f %.3f %.3f %.0f %.0f", $o, $fl ne '' ? $fl : '-', $best->[0], $best->[1], $t->{x}, $t->{z}, $t->{nx}, $t->{nz}, cx(int($sc / $NJ)), cz($sc % $NJ)} = $dist{$k} }
  print "$_  # $row{$_} to go\n" for sort { $row{$b} <=> $row{$a} || $a cmp $b } keys %row;
  # Replay of the mod's rule (TilePuzzle.Next, 0.0.211) along the best way from each start, in the worst case where every row
  # of a wall state lies in the player's walkable part: rows whose plane side the player is on first, then the nearest plane.
  # A step where it would pick another tile is printed as "# CHECK".
  my %byKey; my %bestOf;
  for my $k (keys %edges) { next unless defined $dist{$k} && $dist{$k} > 0;
    my ($best) = sort { $dist{$a->[2]} <=> $dist{$b->[2]} || $a->[0] cmp $b->[0] } grep { defined $dist{$_->[2]} } @{$edges{$k}};
    $bestOf{$k} = $best; push @{$byKey{key($node{$k}{off}, $node{$k}{fl})}}, $best }
  for my $s (@starts) { my ($cell, $off) = @$s; my $reg = flood($cell, $off, undef); my $min = (sort { $a <=> $b } keys %$reg)[0];
    my $k = key($off, \%fl0) . "|$min"; my ($px, $pz) = (cx(int($cell / $NJ)), cz($cell % $NJ)); my @way;
    unless (defined $dist{$k}) { printf "# replay from %.0f %.0f: no way to the goal (the mod names no tile there if the part differs)\n", $px, $pz; next }
    while ($dist{$k} > 0) { my $best = $bestOf{$k}; my %u; my @c = grep { !$u{"$_->[0]/$_->[1]"}++ } @{$byKey{key($node{$k}{off}, $node{$k}{fl})}};
      my ($pick) = sort { my ($ta, $tb) = ($tile{$a->[0]}, $tile{$b->[0]});
          my $sa = ((($px - $ta->{x}) * $ta->{nx} + ($pz - $ta->{z}) * $ta->{nz}) > 0 ? 1 : 0) == $a->[1] ? 0 : 1;
          my $sb = ((($px - $tb->{x}) * $tb->{nx} + ($pz - $tb->{z}) * $tb->{nz}) > 0 ? 1 : 0) == $b->[1] ? 0 : 1;
          $sa <=> $sb || (($px - $ta->{x}) ** 2 + ($pz - $ta->{z}) ** 2) <=> (($px - $tb->{x}) ** 2 + ($pz - $tb->{z}) ** 2) } @c;
      print "# CHECK at $px $pz, walls off ", join(',', sort { $a <=> $b } keys %{$node{$k}{off}}), ": the rule picks $pick->[0], the way is $best->[0]\n" if $pick->[0] ne $best->[0];
      push @way, $best->[0]; my $t = $tile{$best->[0]}; my $cc = $best->[3]; my $sg = $best->[1] ? -1 : 1;
      ($px, $pz) = (cx(int($cc / $NJ)) + $t->{nx} * 40 * $sg, cz($cc % $NJ) + $t->{nz} * 40 * $sg); $k = $best->[2] }
    printf "# replay from %.0f %.0f: %s\n", cx(int($cell / $NJ)), cz($cell % $NJ), join(' ', @way) }
  exit 0;
}

# BFS over (walls, flags, region)
my %seen; my @q = [$s0, \%off0, \%fl0, undef, []];
while (@q) { my ($pos, $off, $fl, $allow, $path) = @{shift @q};
  my $reg = flood($pos, $off, $allow); print "  debug: region ", scalar(keys %$reg), " cells, touches ", join(" ", map { "$_->[0]/$_->[2]" } touches($reg, $off)), "
" if $ENV{DEBUGR};
  my $min = (sort { $a <=> $b } keys %$reg)[0]; my $k = key($off, $fl) . "|$min"; next if $seen{$k}++;
  if (reaches($reg)) { print "solution, ", scalar @$path, " tiles:\n"; print "  $_\n" for @$path; exit 0 }
  print "  state ", key($off, $fl), " region of ", scalar keys %$reg, " cells after: @{[map { (split ' ', $_)[0] } @$path]}\n" if $ENV{TRACE};
  for my $t (touches($reg, $off)) { my ($name, $cell, $d) = @$t; my ($noff, $nfl, $did) = run($name, $d, $off, $fl);
    push @q, [$cell, $noff, $nfl, $name, [@$path, "$name (" . ($d ? 'normal side' : 'back side') . "): $did"]] }
}
print "no solution\n"; exit 1;
