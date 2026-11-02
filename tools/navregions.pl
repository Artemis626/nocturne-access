# Offline copy of WalkGrid on every level (docs/PATHFINDING.md section 2.2), from a navigation dump: builds the grid with
# the same rules (Attr 0 and 0x80 floors, up to LAYERS heights per cell merged within 20, walls blocking within 65 of a vertical
# polygon overlapping 55..180 above a floor, moves between heights within 55, 8 ways, no corner cutting), labels the
# connected regions and prints them with their size and extent, the region of each point of interest and warp landing,
# and a map per region level.
# Usage: perl tools/navregions.pl <dump> [maxcells, default 700 in dungeons, 400 on the world map] [layers, default 8] [player x z]
# Defaults follow the game's WalkGrid (0.0.74): steps and risers 55, 8 layers (0.0.96), Attr 0x80 floors (NAVATTR80), no faces looking
# down (NAVUP), every layer within a step (NAVMULTI), merged floors keep the higher (NAVMERGEHI), no drop under a continuing
# floor (NAVCONT), seams narrower than a cell bridged (NAVSEAM, 0.0.77). Set any of them to 0 to see the older rules (12 Oct 2026: the stale defaults hid a corridor bug for an hour).
# NAVHEAD=180 marks floors with another floor less than that above them (a diagnostic, not a game rule).
# The window is placed as WalkGrid does (around the player when the area is wider than maxcells).
use strict; use warnings; use POSIX qw(floor ceil);
$ENV{$_} //= 1 for qw(NAVATTR80 NAVUP NAVMULTI NAVMERGEHI NAVCONT);
my ($file, $maxCells, $layers, $px, $pz) = @ARGV;
$layers ||= 8;   # WalkGrid.Layers (8 since 0.0.96)
my $C = 30; my ($Clear, $Step, $Body, $MaxStep, $Merge, $NearR) = ($ENV{NAVCLEAR} // 46, 55, 180, 55, 20, 300);   # WalkGrid since 0.0.78 (clearance 46, was 65); NAVCLEAR tries another
# NAVSTEP="step maxstep": the body's lowest blocking height and the largest height change of a move (0.0.63: 55 55).
# 0.0.105: WalkGrid moves up to 75 in dungeons (the Obelisk's lift floors 70 below the hall), 55 on the world map (field < 10).
{ open my $fh, '<', $file or die "$file: $!"; while (<$fh>) { if (/^fieldID (\d+)/) { $MaxStep = $1 < 10 ? 55 : 75; $maxCells ||= $1 < 10 ? 400 : 700; last } } close $fh; }
# 0.0.138: WalkGrid keeps a dungeon area whole up to 700 x 700 cells (DungeonWholeCells; the world map 400).
$maxCells ||= 400;
($Step, $MaxStep) = split ' ', $ENV{NAVSTEP} if $ENV{NAVSTEP};

my (%poly, @pois, @landings, $player, %hitOff, %npcOff);
# NAVHITOFF="1 2": treat these hojis' hit as off (to try a switch's effect on an older dump).
$hitOff{$_} = 1 for split ' ', ($ENV{NAVHITOFF} // '');
open my $F, '<', $file or die "$file: $!";
my $sect = '';
while (my $l = <$F>) {
    $l =~ s/?
$//;
    $sect = $1 if $l =~ /^== (.*)/;
    if (!defined $player && $l =~ /^player (-?[\d.]+) (-?[\d.]+) (-?[\d.]+);/) { $player = [$1, $2, $3]; }
    # The game's switches (dumps from 0.0.66): polygons whose hoji's hit is off, or whose NPC (npc > 0) is off, are left out.
    if ($sect eq 'Collision' && $l =~ /^switches: hit off for held objects \[([\d ]*)\], NPCs off \[([\d ]*)\]/) {
        $hitOff{$_} = 1 for split ' ', $1;
        $npcOff{$_} = 1 for split ' ', $2;
    }
    if ($sect eq 'Collision' && $l =~ /#(\d+) mode2D \d+ edges \d+ n (\S+) (\S+) (\S+) .*? block (\d+) .*? hoji (-?\d+) npc (-?\d+) .*?attr (0x[0-9a-f]+) .*\| pts (.*)$/) {
        my ($id, $nx, $ny, $nz, $blk, $hoji, $npc, $attr, $pts) = ($1, $2, $3, $4, $5, $6, $7, hex($8), $9);
        next if $poly{$id};
        my @p = map { [split ' '] } split /\s*\|\s*/, $pts;
        $poly{$id} = { id => $id, nx => $nx, ny => $ny, nz => $nz, attr => $attr, hoji => $hoji, npc => $npc, p => \@p };
    }
    if ($sect =~ /^Points of interest/ && $l =~ /^  gEvnRng (\d+) (.*?) "(\S+)" at (-?[\d.]+) (-?[\d.]+) d/) { push @pois, [$3, $2, $4, $5]; }
    if ($sect =~ /^Warps/ && $l =~ /^  (\S+) gEvnRng \d+ movetype (\d+) at (-?[\d.]+) (-?[\d.]+) .*-> (\S+) at (-?[\d.]+) (-?[\d.]+) (-?[\d.]+)/) {
        push @landings, ["$1 lands $5", $6, $7, $8];
    }
}
close $F;
($px, $pz) = ($player->[0], $player->[2]) unless defined $pz;
($player->[0], $player->[2]) = ($px, $pz);   # a given "player x z" also starts the flood there

my (@floors, @walls);
my ($x0, $z0, $x1, $z1) = (1e9, 1e9, -1e9, -1e9);
for my $q (sort { $a->{id} <=> $b->{id} } values %poly) {
    my @p = @{ $q->{p} };
    next if @p < 2;
    next if $hitOff{$q->{hoji}} || ($q->{npc} > 0 && $npcOff{$q->{npc}});
    if (abs($q->{ny}) >= 0.5) {
        # NAVATTR80=1: Attr 0x80 floors count too (0.0.63; the Mantra pyramid, field 24 area 17, has its floors in 0x80).
        next if @p < 3 || ($q->{attr} && !($ENV{NAVATTR80} && $q->{attr} == 0x80));
        next if $ENV{NAVUP} && $q->{ny} > 0;   # NAVUP=1: faces looking down are not floors (0.0.65)
        push @floors, $q;
        for (@p) { $x0 = $_->[0] if $_->[0] < $x0; $x1 = $_->[0] if $_->[0] > $x1; $z0 = $_->[2] if $_->[2] < $z0; $z1 = $_->[2] if $_->[2] > $z1; }
    } elsif (abs($q->{ny}) < 0.2) {
        # Walls with a trigger bit (0x40, 0x100, 0x200, 0x800, 0x1000, 0x2000, 0x4000, 0x8000) never block: fldHit.fldHitChkUnit
        # branches on each to automap names, blocks or the sky (0.0.93, Asakusa area 8's stairs; CollisionPoly.IsTriggerAttr).
        # 0x400 (TomeIdx "stopper" boxes in Ginza) stays a wall. NAVWALLATTR=0 restores the old rule.
        next if ($ENV{NAVWALLATTR} // 1) && ($q->{attr} & 0xFB40);
        my ($lo, $hi) = (1e9, -1e9);
        for (@p) { $lo = $_->[1] if $_->[1] < $lo; $hi = $_->[1] if $_->[1] > $hi; }
        my ($a, $b, $best) = ($p[0], $p[0], -1);
        for my $i (0 .. $#p) { for my $j ($i + 1 .. $#p) { my $d = ($p[$i][0] - $p[$j][0]) ** 2 + ($p[$i][2] - $p[$j][2]) ** 2; if ($d > $best) { $best = $d; $a = $p[$i]; $b = $p[$j]; } } }
        push @walls, [$a->[0], $a->[2], $b->[0], $b->[2], $lo, $hi];
    }
}
sub window { my ($lo, $hi, $p, $span) = @_; return ($lo, $hi) if $hi - $lo <= $span; my $s = $p - $span / 2; $s = $lo if $s < $lo; $s = $hi - $span if $s > $hi - $span; return ($s, $s + $span); }
# As WalkGrid (0.0.60): the whole area when it holds at most maxCells squared cells and no side passes 2000; else a window round the player.
my $span = ($maxCells - 1) * $C; my ($wx, $wz) = (($x1 - $x0) / $C + 2, ($z1 - $z0) / $C + 2); my $wholeArea = $wx * $wz <= $maxCells * $maxCells && $wx <= 2000 && $wz <= 2000;
unless ($wholeArea) { ($x0, $x1) = window($x0, $x1, $px, $span); ($z0, $z1) = window($z0, $z1, $pz, $span); }
my $cap = $wholeArea ? 2000 : $maxCells;
my $gx = floor($x0 / $C) * $C + $C / 2; my $gz = floor($z0 / $C) * $C + $C / 2;
my $W = ceil(($x1 - $gx) / $C) + 1; $W = $cap if $W > $cap;
my $H = ceil(($z1 - $gz) / $C) + 1; $H = $cap if $H > $cap;
printf "%d floors, %d walls; grid %d x %d from %s %s (x to %s), %d layers\n", scalar @floors, scalar @walls, $W, $H, $gx, $gz, $gx + ($W - 1) * $C, $layers;

sub inside { my ($pts, $x, $z) = @_; my $in = 0; my @p = @$pts; for (my ($i, $j) = (0, $#p); $i < @p; $j = $i++) { my ($zi, $zj) = ($p[$i][2], $p[$j][2]); if (($zi > $z) != ($zj > $z)) { my $xc = ($p[$j][0] - $p[$i][0]) * ($z - $zi) / ($zj - $zi) + $p[$i][0]; $in = !$in if $x < $xc; } } return $in; }
sub planey { my ($q, $x, $z) = @_; my $a = $q->{p}[0]; return $a->[1] - ($q->{nx} * ($x - $a->[0]) + $q->{nz} * ($z - $a->[2])) / $q->{ny}; }
sub sd { my ($px, $pz, $ax, $az, $bx, $bz) = @_; my ($dx, $dz) = ($bx - $ax, $bz - $az); my $l = $dx * $dx + $dz * $dz; my $t = $l <= 0 ? 0 : (($px - $ax) * $dx + ($pz - $az) * $dz) / $l; $t = 0 if $t < 0; $t = 1 if $t > 1; return sqrt(($ax + $t * $dx - $px) ** 2 + ($az + $t * $dz - $pz) ** 2); }
sub cells { my ($a0, $b0, $a1, $b1) = @_; my $i0 = floor(($a0 - $gx) / $C); $i0 = 0 if $i0 < 0; my $i1 = ceil(($a1 - $gx) / $C); $i1 = $W - 1 if $i1 > $W - 1; my $j0 = floor(($b0 - $gz) / $C); $j0 = 0 if $j0 < 0; my $j1 = ceil(($b1 - $gz) / $C); $j1 = $H - 1 if $j1 > $H - 1; return ($i0, $i1, $j0, $j1); }

my (@cnt, @hgt, @st, @dropped);
my $lost = 0;
for my $f (@floors) {
    my ($fx0, $fz0, $fx1, $fz1) = (1e9, 1e9, -1e9, -1e9);
    for (@{ $f->{p} }) { $fx0 = $_->[0] if $_->[0] < $fx0; $fx1 = $_->[0] if $_->[0] > $fx1; $fz0 = $_->[2] if $_->[2] < $fz0; $fz1 = $_->[2] if $_->[2] > $fz1; }
    my ($i0, $i1, $j0, $j1) = cells($fx0, $fz0, $fx1, $fz1);
    for my $j ($j0 .. $j1) { CELL: for my $i ($i0 .. $i1) {
        my ($x, $z) = ($gx + $i * $C, $gz + $j * $C);
        next unless inside($f->{p}, $x, $z);
        my $y = planey($f, $x, $z); my $c = $j * $W + $i; my $n = $cnt[$c] // 0;
        # NAVMERGEHI=1 (WalkGrid 0.0.74): floors merged within $Merge keep the higher one (smaller y), where the player stands.
        for my $l (0 .. $n - 1) { my $k = $c * $layers + $l; if (abs($hgt[$k] - $y) <= $Merge) { $hgt[$k] = $y if $ENV{NAVMERGEHI} && $y < $hgt[$k]; next CELL; } }
        if ($n >= $layers) { $lost++; $dropped[$c] = 1; next; }
        $hgt[$c * $layers + $n] = $y; $st[$c * $layers + $n] = 1; $cnt[$c] = $n + 1;
    } }
}
print "floor heights dropped by the layer cap: $lost cells\n";
# NAVSEAM=1 (WalkGrid 0.0.77, default): a cell with no floor at its centre whose two sides, half a cell from the centre north and
# south (or east and west), lie on floors within the merge band takes the higher of the two (Ginza 1F, field 19 area 2: the hall
# floor ends at z -1090, the pocket in front of door 01d_08 starts at -1100, and the row of centres at -1095 cut it off).
my $seamed = 0;
if ($ENV{NAVSEAM} // 1) {
    my $h = $C / 2;
    my %side;   # cell -> axis*2+sign -> [heights]
    for my $f (@floors) {
        my ($fx0, $fz0, $fx1, $fz1) = (1e9, 1e9, -1e9, -1e9);
        for (@{ $f->{p} }) { $fx0 = $_->[0] if $_->[0] < $fx0; $fx1 = $_->[0] if $_->[0] > $fx1; $fz0 = $_->[2] if $_->[2] < $fz0; $fz1 = $_->[2] if $_->[2] > $fz1; }
        my $pr = $ENV{NAVSEAMPROBE} // $C - 1; my ($i0, $i1, $j0, $j1) = cells($fx0 - $pr, $fz0 - $pr, $fx1 + $pr, $fz1 + $pr);
        for my $j ($j0 .. $j1) { for my $i ($i0 .. $i1) {
            my $c = $j * $W + $i; next if $cnt[$c];
            my ($x, $z) = ($gx + $i * $C, $gz + $j * $C);
            my $p = $ENV{NAVSEAMPROBE} // $C - 1;   # how far from the centre each side is probed (WalkGrid 0.0.138: a cell less 1; 0.0.77: half a cell, NAVSEAMPROBE=15)
            my @probe = ([$x, $z - $p], [$x, $z + $p], [$x - $p, $z], [$x + $p, $z]);
            for my $s (0 .. 3) { my ($sx, $sz) = @{ $probe[$s] }; push @{ $side{$c}[$s] }, planey($f, $sx, $sz) if inside($f->{p}, $sx, $sz); }
        } }
    }
    for my $c (keys %side) {
        my $best;
        for my $axis (0, 1) {
            for my $ya (@{ $side{$c}[2 * $axis] // [] }) { for my $yb (@{ $side{$c}[2 * $axis + 1] // [] }) {
                next if abs($ya - $yb) > ($ENV{NAVSEAMBAND} // $Step);
                my $y = $ya < $yb ? $ya : $yb; $best = $y if !defined $best || $y < $best;
            } }
        }
        next unless defined $best;
        $hgt[$c * $layers] = $best; $st[$c * $layers] = 1; $cnt[$c] = 1; $seamed++;
    }
    print "seam cells bridged: $seamed\n";
}
# NAVRAIL=1 (WalkGrid 0.0.81, default): a wall lower than a step still blocks when it reaches the game's test height (35 above
# the floor, fldHitChkUnit: pos.y - 35) and no floor lies at its top (a railing, not a stair riser): Kabukicho Prison's 50-unit
# railings by the "Jump Off" volumes. A floor node within RailReach of the wall at its top height (within RailTop) makes it a riser.
my ($TestLift, $RailReach, $RailTop) = (35, 60, 10);
my $railed = 0;
sub top_floor {
    my ($ax, $az, $bx, $bz, $top) = @_;
    my ($i0, $i1, $j0, $j1) = cells(($ax < $bx ? $ax : $bx) - $RailReach, ($az < $bz ? $az : $bz) - $RailReach, ($ax > $bx ? $ax : $bx) + $RailReach, ($az > $bz ? $az : $bz) + $RailReach);
    for my $j ($j0 .. $j1) { for my $i ($i0 .. $i1) {
        my $c = $j * $W + $i; my $n = $cnt[$c] // 0; next unless $n;
        next if sd($gx + $i * $C, $gz + $j * $C, $ax, $az, $bx, $bz) >= $RailReach;
        for my $l (0 .. $n - 1) { return 1 if abs($hgt[$c * $layers + $l] - $top) <= $RailTop; }
    } }
    return 0;
}
for my $w (@walls) {
    my ($ax, $az, $bx, $bz, $lo, $hi) = @$w;
    my $topFloor;
    my ($i0, $i1, $j0, $j1) = cells(($ax < $bx ? $ax : $bx) - $Clear, ($az < $bz ? $az : $bz) - $Clear, ($ax > $bx ? $ax : $bx) + $Clear, ($az > $bz ? $az : $bz) + $Clear);
    for my $j ($j0 .. $j1) { for my $i ($i0 .. $i1) {
        my $c = $j * $W + $i; my $n = $cnt[$c] // 0; next unless $n;
        my $d = sd($gx + $i * $C, $gz + $j * $C, $ax, $az, $bx, $bz); next if $d >= $Clear;
        for my $l (0 .. $n - 1) {
            my $y = $hgt[$c * $layers + $l];
            next if $hi <= $y - $Body;
            if ($lo >= $y - $Step) {
                # Lower than a step: a railing only if it reaches the test height, its bottom is at or under it, and no floor at its top.
                next unless ($ENV{NAVRAIL} // 1) && $lo < $y - $TestLift && $hi > $y - $TestLift;
                $topFloor //= top_floor($ax, $az, $bx, $bz, $lo);
                next if $topFloor;
                $railed++;
            }
            $st[$c * $layers + $l] = 2;
        }
    } }
}
print "nodes blocked by railings: $railed\n";
# NAVBLOCK="x0 z0 x1 z1;x0 z0 x1 z1": rectangles whose nodes are blocked, as WalkGrid keeps warp volumes (and, 0.0.120, story
# event volumes when another way exists) out of routes; give the volume plus AvoidMargin (45).
if ($ENV{NAVBLOCK}) {
    my $blocked = 0;
    for my $r (split /;/, $ENV{NAVBLOCK}) {
        my ($x0, $z0, $x1, $z1) = split ' ', $r;
        for my $c (0 .. $W * $H - 1) {
            my ($cx, $cz) = ($gx + ($c % $W) * $C, $gz + int($c / $W) * $C);
            next unless $cx >= $x0 && $cx <= $x1 && $cz >= $z0 && $cz <= $z1;
            for my $l (0 .. ($cnt[$c] // 0) - 1) { my $k = $c * $layers + $l; if ($st[$k] == 1) { $st[$k] = 2; $blocked++ } }
        }
    }
    print "nodes blocked by NAVBLOCK: $blocked\n";
}
my @Dx = (0, 1, 1, 1, 0, -1, -1, -1); my @Dz = (1, 1, 0, -1, -1, -1, 0, 1);
# NAVHEAD=180: a floor with another floor of its cell less than this above it (Y points down: a smaller y) is no place to
# stand (WalkGrid 0.0.74, the Nihilo core's spiral ramp under its floor).
if ($ENV{NAVHEAD}) {
    my $headBlocked = 0; my %headPair;
    for my $c (0 .. $W * $H - 1) { my $n = $cnt[$c] // 0; next if $n < 2;
        for my $l (0 .. $n - 1) { my $k = $c * $layers + $l; next unless $st[$k] == 1;
            for my $m (0 .. $n - 1) { next if $m == $l; my $above = $hgt[$k] - $hgt[$c * $layers + $m];
                if ($above > 0 && $above < $ENV{NAVHEAD}) { $st[$k] = 2; $headBlocked++; $headPair{sprintf "%.0f under %.0f", $hgt[$k], $hgt[$c * $layers + $m]}++; last; } } } }
    print "  $_: $headPair{$_}\n" for sort { $headPair{$b} <=> $headPair{$a} } keys %headPair;
    print "no headroom (NAVHEAD $ENV{NAVHEAD}): $headBlocked places\n";
}
sub step { my ($i, $j, $y) = @_; return -1 if $i < 0 || $j < 0 || $i >= $W || $j >= $H; my $c = $j * $W + $i; my ($best, $bd) = (-1, $MaxStep); for my $l (0 .. ($cnt[$c] // 0) - 1) { my $k = $c * $layers + $l; my $d = abs($hgt[$k] - $y); if ($st[$k] == 1 && $d <= $bd) { $bd = $d; $best = $k; } } return $best; }
sub steps { my ($i, $j, $y) = @_; return () if $i < 0 || $j < 0 || $i >= $W || $j >= $H; my $c = $j * $W + $i;
    my @in = grep { abs($hgt[$_] - $y) <= $MaxStep } map { $c * $layers + $_ } 0 .. ($cnt[$c] // 0) - 1;
    # NAVCONT=1 (WalkGrid 0.0.74): where the floor walked on continues into the cell (within 5), no move to a floor under it.
    if ($ENV{NAVCONT}) { my @same = grep { abs($hgt[$_] - $y) <= 5 } @in; if (@same) { my $lim = (sort { $a <=> $b } map { $hgt[$_] } @same)[-1]; @in = grep { $hgt[$_] <= $lim } @in; } }
    return grep { $st[$_] == 1 } @in; }
my @comp; my @info; my $label = 0;
for my $k0 (0 .. $W * $H * $layers - 1) {
    next unless ($st[$k0] // 0) == 1 && !defined $comp[$k0];
    $comp[$k0] = $label; my @q = ($k0); my ($n, $mx0, $mx1, $mz0, $mz1, $ylo, $yhi) = (0, 1e9, -1e9, 1e9, -1e9, 1e9, -1e9);
    while (@q) {
        my $cur = shift @q; $n++; my $c = int($cur / $layers); my ($ci, $cj) = ($c % $W, int($c / $W)); my $y = $hgt[$cur];
        my ($x, $z) = ($gx + $ci * $C, $gz + $cj * $C);
        $mx0 = $x if $x < $mx0; $mx1 = $x if $x > $mx1; $mz0 = $z if $z < $mz0; $mz1 = $z if $z > $mz1; $ylo = $y if $y < $ylo; $yhi = $y if $y > $yhi;
        for my $s (0 .. 7) {
            next if $Dx[$s] && $Dz[$s] && (step($ci + $Dx[$s], $cj, $y) < 0 || step($ci, $cj + $Dz[$s], $y) < 0);
            # NAVMULTI=1: every walkable layer of the neighbour within MaxStep (WalkGrid 0.0.60), not only the closest.
            for my $nk ($ENV{NAVMULTI} ? steps($ci + $Dx[$s], $cj + $Dz[$s], $y) : step($ci + $Dx[$s], $cj + $Dz[$s], $y)) {
                next if $nk < 0 || defined $comp[$nk];
                $comp[$nk] = $label; push @q, $nk;
            }
        }
    }
    $info[$label] = [$n, $mx0, $mx1, $mz0, $mz1, $ylo, $yhi];
    $label++;
}
my @order = sort { $info[$b][0] <=> $info[$a][0] } 0 .. $label - 1;
printf "%d regions; the largest:\n", $label;
for my $r (grep { $info[$_][0] >= 20 } @order) { my $i = $info[$r]; printf "  R%d: %d places, x %d..%d, z %d..%d, y %.0f..%.0f\n", $r, @$i; }

sub regions_at { my ($x, $z, $rad) = @_; my %s; my ($i0, $i1, $j0, $j1) = cells($x - $rad, $z - $rad, $x + $rad, $z + $rad);
    for my $j ($j0 .. $j1) { for my $i ($i0 .. $i1) { my $c = $j * $W + $i; next if (($gx + $i * $C - $x) ** 2 + ($gz + $j * $C - $z) ** 2) > $rad * $rad;
        for my $l (0 .. ($cnt[$c] // 0) - 1) { my $k = $c * $layers + $l; $s{ sprintf "R%d(y %.0f)", $comp[$k], $hgt[$k] } = 1 if defined $comp[$k]; } } }
    return join(' ', sort keys %s) || 'none'; }
print "points of interest (regions within NAVREGR, default 250):\n";
printf "  %-8s %-18s %7s %7s  %s\n", $_->[0], $_->[1], $_->[2], $_->[3], regions_at($_->[2], $_->[3], $ENV{NAVREGR} // 250) for @pois;
print "warp landings (regions within 60):\n";
printf "  %-24s %7s %6s %7s  %s\n", $_->[0], $_->[1], $_->[2], $_->[3], regions_at($_->[1], $_->[3], 60) for @landings;
printf "player %s %s %s: %s\n", @$player, regions_at($player->[0], $player->[2], 60);
# Optional box map: NAVMAP="x0 x1 z0 z1" prints per cell the walkable layers' heights rounded to 100s (w), '#' blocked only.
if ($ENV{NAVMAP}) {
    my ($bx0, $bx1, $bz0, $bz1) = split ' ', $ENV{NAVMAP};
    my ($i0, $i1, $j0, $j1) = cells($bx0, $bz0, $bx1, $bz1);
    print "map x $bx0..$bx1, z $bz0..$bz1: per cell the region letter of each walkable layer (by height), '#' blocked, '.' none\n";
    my %letter; my $next = 0;
    for (my $j = $j1; $j >= $j0; $j--) {
        my $row = sprintf "%6d ", $gz + $j * $C;
        for my $i ($i0 .. $i1) {
            my $c = $j * $W + $i; my $s = '';
            for my $l (0 .. ($cnt[$c] // 0) - 1) { my $k = $c * $layers + $l;
                if ($st[$k] == 1) { my $r = $comp[$k]; $letter{$r} //= chr(65 + $next++ % 26); $s .= $letter{$r}; } else { $s .= '#'; } }
            $row .= sprintf "%-3s", $s eq '' ? '.' : $s;
        }
        print "$row\n";
    }
    print "letters: ", join(', ', map { "$letter{$_}=R$_" } sort { $a <=> $b } keys %letter), "\n";
    for my $w (@walls) { my ($ax, $az, $bx, $bz, $lo, $hi) = @$w; next if ($ax < $bx0 - 100 && $bx < $bx0 - 100) || ($ax > $bx1 + 100 && $bx > $bx1 + 100) || ($az < $bz0 - 100 && $bz < $bz0 - 100) || ($az > $bz1 + 100 && $bz > $bz1 + 100); printf "  wall %s %s - %s %s, y %s..%s\n", $ax, $az, $bx, $bz, $lo, $hi; }
}
if ($ENV{NAVCELL}) {
    for my $pt (split /;/, $ENV{NAVCELL}) {
        my ($x, $z) = split ' ', $pt; my $i = int(($x - $gx) / $C + 0.5); my $j = int(($z - $gz) / $C + 0.5); my $c = $j * $W + $i;
        printf "cell %d %d (centre %d %d):", $x, $z, $gx + $i * $C, $gz + $j * $C;
        for my $l (0 .. ($cnt[$c] // 0) - 1) { my $k = $c * $layers + $l; printf " [y %.1f %s R%s]", $hgt[$k], $st[$k] == 1 ? 'walk' : 'blocked', $comp[$k] // '-'; }
        print "\n";
    }
}
# One-way flood from the player, as WalkGrid.Flood and Plan move (start: the player's cell, the layer nearest the player's y),
# then which points of interest it reaches (a node within 250 at the layer nearest the point's own floor is enough).
{
    my $pi = int(($player->[0] - $gx) / $C + 0.5); my $pj = int(($player->[2] - $gz) / $C + 0.5); my $pc = $pj * $W + $pi;
    my ($start, $bd) = (-1, 1e9);
    for my $l (0 .. ($cnt[$pc] // 0) - 1) { my $k = $pc * $layers + $l; my $d = abs($hgt[$k] - $player->[1]); if ($st[$k] == 1 && $d < $bd) { $bd = $d; $start = $k; } }
    my @reach; my @q = ($start); $reach[$start] = 1 if $start >= 0; @q = () if $start < 0;
    while (@q) {
        my $cur = shift @q; my $c = int($cur / $layers); my ($ci, $cj) = ($c % $W, int($c / $W)); my $y = $hgt[$cur];
        for my $s (0 .. 7) {
            next if $Dx[$s] && $Dz[$s] && (step($ci + $Dx[$s], $cj, $y) < 0 || step($ci, $cj + $Dz[$s], $y) < 0);
            for my $nk ($ENV{NAVMULTI} ? steps($ci + $Dx[$s], $cj + $Dz[$s], $y) : step($ci + $Dx[$s], $cj + $Dz[$s], $y)) {
                next if $nk < 0 || $reach[$nk]; $reach[$nk] = 1; push @q, $nk;
            }
        }
    }
    my $n = grep { $_ } @reach;
    print "one-way flood from the player: $n places\n";
    for my $p (@pois) {
        my ($x, $z) = ($p->[2], $p->[3]); my $r = $ENV{NAVPOIR} // 250; my ($i0, $i1, $j0, $j1) = cells($x - $r, $z - $r, $x + $r, $z + $r); my %ys;
        for my $j ($j0 .. $j1) { for my $i ($i0 .. $i1) { my $c = $j * $W + $i; for my $l (0 .. ($cnt[$c] // 0) - 1) { my $k = $c * $layers + $l; $ys{ sprintf '%.0f', $hgt[$k] } = 1 if $reach[$k]; } } }
        printf "  %-8s %-18s reached at y: %s\n", $p->[0], $p->[1], %ys ? join(' ', sort { $a <=> $b } keys %ys) : 'none';   # not "|| 'none'": a lone y 0 is false in perl
    }
}
