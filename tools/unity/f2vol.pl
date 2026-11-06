# Lists the volumes of an area file (fNNN_AAA.f2) with their shape, the way the game builds fldEveHit.gEvnRng / gEvnRngBox:
# name, position (x z), yaw (degrees, from the record's quaternion), box type (1 flat plane, 2 box), width, depth, and for a flat
# plane its two ends. Records: the name, 16 bytes on x y z w, 32 on the quaternion x y z w, 48 on the scale, 64 a u32 1, 68 a
# pointer. Boxes: a table of {u32 type, f32 height, f32 width, f32 depth} each followed by {u32 1, u32 index, 0, next}, in the
# order of the records (checked on field 34 area 12 against dump 2026-11-05 21:15:30: 01eve_02 1800 wide at 43 degrees, ends
# 3718 7068 and 2402 5840). Args: f2 [name regex]. 0.0.157 research tool.
use strict; use warnings;
my ($file, $want) = @ARGV; $want //= '.';
local $/; open my $f, '<:raw', $file or die "$file: $!"; my $d = <$f>; close $f;
my $L = length $d;
my @rec;
while ($d =~ /((?:\d\d\d_)?\d\d(?:d|pos|eve|npc|cam)_\d\d)\0/g) {
    my ($n, $at) = ($1, $-[1]);
    next if $n =~ /^\d\d\d_/;
    next if $at + 72 > $L;
    my ($x, $y, $z) = unpack 'f<3', substr($d, $at + 16, 12);
    my ($qx, $qy, $qz, $qw) = unpack 'f<4', substr($d, $at + 32, 16);
    my ($one) = unpack 'V', substr($d, $at + 64, 4);
    next unless abs($x) < 1e6 && abs($z) < 1e6 && $one == 1;
    push @rec, { n => $n, at => $at, x => $x, y => $y, z => $z, yaw => 2 * atan2($qy, $qw) };
}
# Box table: consecutive 32-byte pairs.
my @box;
# A chain: a box {type 1 or 2, f32 height, f32 width, f32 depth}, then one or more link records {u32, u32 index or -1, 0, next};
# the last link's next is the following box. The longest chain is the volumes' boxes, one per volume in record order.
sub isbox { my ($e) = @_; return 0 if $e + 16 > $L; my ($t, $hb) = unpack q{V2}, substr($d, $e, 8); my ($h, $w, $dp) = unpack q{f<3}, substr($d, $e + 4, 12); return ($t == 1 || $t == 2) && $hb > 0x3f000000 && $h > 0 && $h < 5000 && $w >= 0 && $w < 20000 && $dp >= 0 }
sub islink { my ($e) = @_; return 0 if $e + 16 > $L; my ($a, $i, $z, $nx) = unpack q{V4}, substr($d, $e, 16); return $a <= 8 && ($i == 0xffffffff || $i < 1000) && $z == 0 && $nx > $e && $nx < $L }
my %seen;
for (my $b = 0; $b + 32 <= $L; $b += 4) {
    next if $seen{$b} || !isbox($b) || !islink($b + 16);
    my @chain; my $e = $b;
    while (isbox($e) && islink($e + 16)) {
        $seen{$e} = 1;
        my ($t, $h, $w, $dp) = unpack q{Vf<f<f<}, substr($d, $e, 16);
        push @chain, { t => $t, h => $h, w => $w, dp => $dp, at => $e };
        my $l = $e + 16; my $guard = 0;
        while (islink($l) && $guard++ < 50) { my $nx = (unpack q{V4}, substr($d, $l, 16))[3]; last if isbox($nx); $l = $nx; }
        $e = (unpack q{V4}, substr($d, $l, 16))[3];
    }
    @box = @chain if @chain > @box;
}
my @vol = grep { $_->{n} =~ /d_|eve_|npc_/ } @rec;   # volumes; points (pos) and cameras have no box
printf "# %d volume records, %d boxes\n", scalar @vol, scalar @box;
for my $k (0 .. $#vol) {
    my $v = $vol[$k]; my $bx = $box[$k];
    next unless $v->{n} =~ /$want/;
    my $deg = $v->{yaw} * 180 / 3.14159265;
    my $line = sprintf "%-9s %7.0f %7.0f yaw %7.1f", $v->{n}, $v->{x}, $v->{z}, $deg;
    if ($bx) {
        $line .= sprintf " box t%d w %6.0f d %6.0f", $bx->{t}, $bx->{w}, $bx->{dp};
        if ($bx->{t} == 1 || $bx->{dp} == 0) {
            my ($c, $s) = (cos $v->{yaw}, sin $v->{yaw}); my $hw = $bx->{w} / 2;
            $line .= sprintf " ends %.0f %.0f %.0f %.0f", $v->{x} + $c * $hw, $v->{z} + $s * $hw, $v->{x} - $c * $hw, $v->{z} - $s * $hw;
        }
    }
    print "$line\n";
}
