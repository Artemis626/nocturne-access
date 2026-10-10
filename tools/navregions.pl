# Offline copy of WalkGrid on every level (docs/PATHFINDING.md section 2.2), from a navigation dump: builds the grid with
# the same rules (Attr 0 floors, up to LAYERS heights per cell merged within 20, walls blocking within 65 of a vertical
# polygon overlapping 30..180 above a floor, moves between heights within 45, 8 ways, no corner cutting), labels the
# connected regions and prints them with their size and extent, the region of each point of interest and warp landing,
# and a map per region level.
# Usage: perl tools/navregions.pl <dump> [maxcells per side, default 400] [layers, default 3] [player x z]
# The window is placed as WalkGrid does (around the player when the area is wider than maxcells).
use strict; use warnings; use POSIX qw(floor ceil);
my ($file, $maxCells, $layers, $px, $pz) = @ARGV;
$maxCells ||= 400; $layers ||= 3;
my $C = 30; my ($Clear, $Step, $Body, $MaxStep, $Merge, $NearR) = (65, 30, 180, 45, 20, 300);
# NAVSTEP="step maxstep": the body's lowest blocking height and the largest height change of a move (0.0.63: 55 55).
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
        my ($lo, $hi) = (1e9, -1e9);
        for (@p) { $lo = $_->[1] if $_->[1] < $lo; $hi = $_->[1] if $_->[1] > $hi; }
        my ($a, $b, $best) = ($p[0], $p[0], -1);
        for my $i (0 .. $#p) { for my $j ($i + 1 .. $#p) { my $d = ($p[$i][0] - $p[$j][0]) ** 2 + ($p[$i][2] - $p[$j][2]) ** 2; if ($d > $best) { $best = $d; $a = $p[$i]; $b = $p[$j]; } } }
        push @walls, [$a->[0], $a->[2], $b->[0], $b->[2], $lo, $hi];
    }
}
sub window { my ($lo, $hi, $p, $span) = @_; return ($lo, $hi) if $hi - $lo <= $span; my $s = $p - $span / 2; $s = $lo if $s < $lo; $s = $hi - $span if $s > $hi - $span; return ($s, $s + $span); }
my $span = ($maxCells - 1) * $C;
($x0, $x1) = window($x0, $x1, $px, $span);
($z0, $z1) = window($z0, $z1, $pz, $span);
my $gx = floor($x0 / $C) * $C + $C / 2; my $gz = floor($z0 / $C) * $C + $C / 2;
my $W = ceil(($x1 - $gx) / $C) + 1; $W = $maxCells if $W > $maxCells;
my $H = ceil(($z1 - $gz) / $C) + 1; $H = $maxCells if $H > $maxCells;
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
        for my $l (0 .. $n - 1) { next CELL if abs($hgt[$c * $layers + $l] - $y) <= $Merge; }
        if ($n >= $layers) { $lost++; $dropped[$c] = 1; next; }
        $hgt[$c * $layers + $n] = $y; $st[$c * $layers + $n] = 1; $cnt[$c] = $n + 1;
    } }
}
print "floor heights dropped by the layer cap: $lost cells\n";
for my $w (@walls) {
    my ($ax, $az, $bx, $bz, $lo, $hi) = @$w;
    my ($i0, $i1, $j0, $j1) = cells(($ax < $bx ? $ax : $bx) - $Clear, ($az < $bz ? $az : $bz) - $Clear, ($ax > $bx ? $ax : $bx) + $Clear, ($az > $bz ? $az : $bz) + $Clear);
    for my $j ($j0 .. $j1) { for my $i ($i0 .. $i1) {
        my $c = $j * $W + $i; my $n = $cnt[$c] // 0; next unless $n;
        my $d = sd($gx + $i * $C, $gz + $j * $C, $ax, $az, $bx, $bz); next if $d >= $Clear;
        for my $l (0 .. $n - 1) { my $y = $hgt[$c * $layers + $l]; next if $lo >= $y - $Step || $hi <= $y - $Body; $st[$c * $layers + $l] = 2; }
    } }
}
my @Dx = (0, 1, 1, 1, 0, -1, -1, -1); my @Dz = (1, 1, 0, -1, -1, -1, 0, 1);
sub step { my ($i, $j, $y) = @_; return -1 if $i < 0 || $j < 0 || $i >= $W || $j >= $H; my $c = $j * $W + $i; my ($best, $bd) = (-1, $MaxStep); for my $l (0 .. ($cnt[$c] // 0) - 1) { my $k = $c * $layers + $l; my $d = abs($hgt[$k] - $y); if ($st[$k] == 1 && $d <= $bd) { $bd = $d; $best = $k; } } return $best; }
sub steps { my ($i, $j, $y) = @_; return () if $i < 0 || $j < 0 || $i >= $W || $j >= $H; my $c = $j * $W + $i; return grep { $st[$_] == 1 && abs($hgt[$_] - $y) <= $MaxStep } map { $c * $layers + $_ } 0 .. ($cnt[$c] // 0) - 1; }
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
print "points of interest (regions within 250):\n";
printf "  %-8s %-18s %7s %7s  %s\n", $_->[0], $_->[1], $_->[2], $_->[3], regions_at($_->[2], $_->[3], 250) for @pois;
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
        my ($x, $z) = ($p->[2], $p->[3]); my ($i0, $i1, $j0, $j1) = cells($x - 250, $z - 250, $x + 250, $z + 250); my %ys;
        for my $j ($j0 .. $j1) { for my $i ($i0 .. $i1) { my $c = $j * $W + $i; for my $l (0 .. ($cnt[$c] // 0) - 1) { my $k = $c * $layers + $l; $ys{ sprintf '%.0f', $hgt[$k] } = 1 if $reach[$k]; } } }
        printf "  %-8s %-18s reached at y: %s\n", $p->[0], $p->[1], join(' ', sort { $a <=> $b } keys %ys) || 'none';
    }
}
