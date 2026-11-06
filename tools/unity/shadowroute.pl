# Shadow-free routes through a field whose "shadow" volumes must never be crossed (the Amala Temple's Red Temple, field 34 areas 11
# to 17; 6 Nov 2026, 0.0.157). Read from the game's own files only:
#   - each area's walls and floors from its area file (tools/unity/f2dump.pl), flooded by tools/navregions.pl;
#   - the shadows: event volumes whose procedure sets the alarm flag (FLAG, default 1703: "PUSHIS 1703; COMM 0x8" in the flow
#     listing), their shapes from the area file (tools/unity/f2vol.pl: a flat plane is a line, a box a rectangle), given to
#     navregions.pl as blocked strips (NAVBLOCK);
#   - the doors from the door table (wap.txt from tools/unity/wap.pl): a door volume joins the regions around it to the region of
#     its landing point (01pos_NN of that area);
#   - DEVICE rows: "area volume to-area point" (warp devices whose script calls CALL_FIELD2, read from the flow listing).
# Prints, per area, the regions and where every door leads, then the route from START to GOAL as the doors and devices to take.
# Args: dir-with-f2-wap-flow field "areas" start-area start-point goal-area goal-volume [device rows separated by ;]
# Example: perl tools/unity/shadowroute.pl research/gamedata/f034 34 "11 12 13 14 15 16 17" 11 01pos_01 15 01eve_51 \
#            "12 01eve_51 15 01pos_11;15 01eve_50 12 01pos_12"
use strict; use warnings; use FindBin;
my ($dir, $field, $areas, $startA, $startP, $goalA, $goalV, $devs) = @ARGV;
my $flag = $ENV{FLAG} // 1703;
my $tmp = $ENV{TMPDIR} // $ENV{TEMP} // '/tmp';
my $T = "$FindBin::Bin";
my @areas = split ' ', $areas;

# Procedures that set the flag.
my %setter; { open my $fl, '<', "$dir/f" . sprintf('%03d', $field) . '.flow.txt' or die "flow: $!"; my ($p, $prev) = ('', '');
  while (<$fl>) { $p = $1 if /^procedure (\S+)/; $setter{$p} = 1 if $prev =~ /PUSHIS $flag\s*$/ && /COMM 0x8\b/; $prev = $_; } }

# Door table rows.
my @rows; { open my $w, '<', "$dir/wap.txt" or die "wap.txt: $!"; while (<$w>) {
  my ($mt, $fa, $nm) = /movetype (\d+) flagmode \d+ flag \d+ from area\+1 (\d+) "([^"]*)"/ or next; next if $nm eq '';
  my ($tf, $ta, $tp) = /to field (\d+) area\+1 (\d+) u10 \d+ at "([^"]*)"/ or next;
  push @rows, { a => $fa - 1, n => $nm, mt => $mt, tf => $tf, ta => ($ta == 0 ? $fa - 1 : $ta - 1), tp => $tp }; } }

my (%region, %doorRegs, %real, %shapes);
# A point lies in a shadow: inside its box, or within 30 of its line (the player's step).
sub inShadow { my ($a, $px, $pz) = @_; for my $s (@{ $shapes{$a} // [] }) { my ($n, $x, $z, $r, $hw, $hd) = @$s; my ($dx, $dz) = ($px - $x, $pz - $z); my ($u, $v) = ($dx * cos($r) + $dz * sin($r), -$dx * sin($r) + $dz * cos($r)); return $n if abs($u) <= $hw + 30 && abs($v) <= $hd + 30; } return '' }
my %pos;   # region{area}{point} = [regions]; real{area}{region}
for my $a (@areas) {
    my $f2 = sprintf '%s/f%03d_%03d.f2', $dir, $field, $a + 1;
    # Shadows of this area: the area file names their procedure ("013_01eve_01") or it is "{a+1}_{volume}".
    my @vols = `perl "$T/f2vol.pl" "$f2"`;
    my %fileProc; { local $/; open my $fh, '<:raw', $f2 or die; my $d = <$fh>; $fileProc{substr($1, 4)} = $1 while $d =~ /(\d\d\d_\d\deve_\d\d)\0/g; }
    my @block; my @shadowNames;
    for (@vols) {
        my ($n, $x, $z, $yaw, $t, $w, $dp) = /^(\S+)\s+(\S+)\s+(\S+)\s+yaw\s+(\S+)\s+box t(\d) w\s+(\S+) d\s+(\S+)/ or next;
        my $proc = $setter{sprintf('%03d_%s', $a + 1, $n)} ? sprintf('%03d_%s', $a + 1, $n) : ($fileProc{$n} // '');
        next unless $setter{$proc};
        push @shadowNames, $n;
        push @{ $shapes{$a} }, [$n, $x, $z, $yaw * 3.14159265 / 180, $w / 2, $dp / 2];
        my $r = $yaw * 3.14159265 / 180; my ($c, $s) = (cos $r, sin $r);
        my $half = $w / 2; my $hd = $dp / 2;
        for (my $u = -$half; $u <= $half; $u += 20) {
            for (my $v = -$hd; $v <= $hd; $v += 20) {
                my ($px, $pz) = ($x + $c * $u - $s * $v, $z + $s * $u + $c * $v);
                push @block, sprintf '%.0f %.0f %.0f %.0f', $px - 20, $pz - 20, $px + 20, $pz + 20;
            }
        }
    }
    my $dump = "$tmp/shadowroute_$a.txt";
    system(qq{perl "$T/f2dump.pl" "$f2" $field $a 0 0 > "$dump" 2>/dev/null});
    # Two states: "off" (the alarm off, shadows block) and "on" (the alarm on: every shadow's script ends at once, so nothing blocks).
    # Doors: the regions within 250 (a door stands in its wall); landing points, devices, tiles: within 60, where the player stands.
    for my $state (['off', join ';', @block], ['on', '']) {
        local $ENV{NAVBLOCK} = $state->[1];
        for my $pass ([250, qr/d_/], [60, qr/pos_|eve_/]) {
            local $ENV{NAVREGR} = $pass->[0];
            my @out = `perl "$T/../navregions.pl" "$dump" 2>&1`;
            my $in = 0;
            for (@out) {
                $in = 1 if /^points of interest/; $in = 0 if /^warp landings/;
                next unless $in && /^\s+(\S+)\s+point\s+(\S+)\s+(\S+)\s+(.*)$/;
                my ($p, $px, $pz, $rs) = ($1, $2, $3, $4);
                next if $p =~ /^\d\d\d_/ || $p !~ $pass->[1];
                $pos{$a}{$p} = [$px, $pz];
                $region{$a}{$state->[0]}{$p} = [ $rs =~ /R(\d+)/g ];
            }
        }
        # A region is real when a landing point lies in it (the rest is floor outside the rooms).
        for my $p (grep { /pos_/ } keys %{ $region{$a}{$state->[0]} }) { $real{$a}{$state->[0]}{$_} = 1 for @{ $region{$a}{$state->[0]}{$p} }; }
    }
    printf "area %d: shadows %s; %d strips\n", $a, join(' ', @shadowNames) || 'none', scalar @block;
}

# Graph: node "area/state/region", edges with a cost: a door or device 1, getting caught (the alarm on) 10, a reset tile 1.
my (%edges, %seenEdge, %said);
sub add { my ($from, $to, $how, $cost) = @_; return if $seenEdge{"$from>$to>$how"}++; push @{ $edges{$from} }, [$to, $how, $cost]; }
my %areaSet = map { $_ => 1 } @areas;
# Where a move lands: with the alarm off, a landing inside a shadow sets it off there.
sub landing { my ($a, $p, $state) = @_; my @n;
    if ($state eq 'off' && $pos{$a}{$p} && (my $sh = inShadow($a, @{ $pos{$a}{$p} }))) { return map { ["$a/on/$_", " (lands in shadow $sh: alarm)"] } @{ $region{$a}{on}{$p} // [] }; }
    return map { ["$a/$state/$_", ''] } @{ $region{$a}{$state}{$p} // [] }; }
for my $r (@rows) {
    next unless $areaSet{$r->{a}} && ($r->{tf} == 0 || $r->{tf} == $field) && $areaSet{$r->{ta}};
    for my $st ('off', 'on') {
        my $from = $region{$r->{a}}{$st}{$r->{n}} or next;
        for my $fr (grep { $real{$r->{a}}{$st}{$_} } @$from) { for my $to (landing($r->{ta}, $r->{tp}, $st)) { add("$r->{a}/$st/$fr", $to->[0], "$r->{a}:$r->{n}$to->[1]", 1); } }
    }
}
# Devices work only with the alarm off.
for my $row (split /;/, ($devs // '')) {
    my ($a, $v, $ta, $tp) = split ' ', $row; next unless defined $tp;
    for my $fr (@{ $region{$a}{off}{$v} // [] }) { for my $to (landing($ta, $tp, 'off')) { add("$a/off/$fr", $to->[0], "$a:$v (device)$to->[1]", 1); } }
}
# Getting caught: from any spot of an area with shadows, the same spot with the alarm on (through the points both floods share).
for my $a (@areas) { next unless @{ $shapes{$a} // [] };
    for my $p (keys %{ $pos{$a} }) { for my $ro (@{ $region{$a}{off}{$p} // [] }) { for my $rn (@{ $region{$a}{on}{$p} // [] }) { add("$a/off/$ro", "$a/on/$rn", "$a: get caught by a shadow (alarm on)", 10); } } } }
# Reset tiles: "area tile" rows in RESET (the alarm off where the tile is).
for my $row (split /;/, ($ENV{RESET} // '')) { my ($a, $t) = split ' ', $row; next unless defined $t;
    for my $rn (@{ $region{$a}{on}{$t} // [] }) { for my $ro (@{ $region{$a}{off}{$t} // [] }) { add("$a/on/$rn", "$a/off/$ro", "$a:$t (reset tile, alarm off)", 1); } } }

# SHOWAREA=N: every point of area N with its regions in both states, and every move out of its regions.
if (defined(my $sa = $ENV{SHOWAREA})) {
    for my $p (sort keys %{ $pos{$sa} // {} }) { printf "  %-9s %6.0f %6.0f  off R%-8s on R%s\n", $p, @{ $pos{$sa}{$p} }, join(',', @{ $region{$sa}{off}{$p} // [] }), join(',', @{ $region{$sa}{on}{$p} // [] }); }
    for my $f (sort keys %edges) { next unless $f =~ m{^$sa/}; printf "  %-10s -> %s\n", $f, join(', ', map { "$_->[1] to $_->[0]" } @{ $edges{$f} }); }
}

# Cheapest route from the start (alarm off) to the goal device (alarm off).
my @start = map { "$startA/off/$_" } @{ $region{$startA}{off}{$startP} // [] };
my %goal = map { ("$goalA/off/$_" => 1) } @{ $region{$goalA}{off}{$goalV} // [] };
my (%dist, %prev); my @open;
for (@start) { $dist{$_} = 0; $prev{$_} = ['', '']; push @open, $_; }
my $hit;
while (@open) {
    @open = sort { $dist{$a} <=> $dist{$b} } @open; my $n = shift @open;
    if ($goal{$n}) { $hit = $n; last; }
    for my $e (@{ $edges{$n} // [] }) { my $d = $dist{$n} + $e->[2];
        next if exists $dist{$e->[0]} && $dist{$e->[0]} <= $d;
        $dist{$e->[0]} = $d; $prev{$e->[0]} = [$n, $e->[1]]; push @open, $e->[0]; }
}
print "start @start, goal ", join(' ', sort keys %goal), "\n";
if (!$hit) { print "NO route\n"; exit 1; }
my @path; for (my $n = $hit; $prev{$n}[0] ne ''; $n = $prev{$n}[0]) { unshift @path, "$prev{$n}[1] (to $n)"; }
print "route (cost $dist{$hit}): ", join("\n       ", @path), "\n";
