#!/usr/bin/perl
# Decodes a field's door warp table file (FNNN.WAP in bundle fNNN; 28 Oct 2026), the data behind fldWap.gFldWarpBuff.door_buff.
# Layout (checked against the Shift+F6 dumps of field 26, rows 7, 17, 18, 123): a 160-byte header (5 elevator rows of 32),
# then 256 rows of 100 bytes:
#   +0 movetype u8, +1 flagmode u8, +2 flag u16, +4 union01 u16 (area + 1), +6 union02 char[12] (the volume),
#   +0x12 union03 u16, +0x14 union04 u16, +0x16 union05 u16, +0x18 union06 char[12], +0x24 union07 char[12],
#   +0x30 warptype u8, +0x31 warp_attr u8, +0x32 union08 u16 (field, 0 this one), +0x34 union09 u16 (area + 1, 0 this one),
#   +0x36 union10 u16, +0x38 warp_pos char[12], +0x44 warp_cammode u8, +0x45 warp_camtbl u8, +0x46 warp_camname char[12],
#   +0x52 warp_bgm u8, +0x53 warp_foot u8, +0x54 after_flag u8, +0x55 after_scr char[15].
# Prints the rows in the dump's own format. Usage: perl wap.pl F029.WAP [area]   (area as the game counts it, from 0)
use strict;
use warnings;

my ($file, $area) = @ARGV;
die "usage: wap.pl FNNN.WAP [area]\n" unless $file;
open my $fh, '<:raw', $file or die "$file: $!\n";
local $/;
my $data = <$fh>;
close $fh;

sub str { my ($off, $len) = @_; my $s = substr($data, $off, $len); $s =~ s/\0.*//s; return $s; }

my $rows = int((length($data) - 160) / 100);
for my $i (0 .. $rows - 1) {
    my $o = 160 + $i * 100;
    my ($move, $fmode, $flag, $from) = unpack('C C v v', substr($data, $o, 6));
    next if $move == 0 && $from == 0;
    next if defined $area && $from != $area + 1;
    my ($u03, $u04, $u05) = unpack('v v v', substr($data, $o + 0x12, 6));
    my ($wtype, $attr, $tofield, $toarea, $u10) = unpack('C C v v v', substr($data, $o + 0x30, 8));
    my ($cmode, $ctbl) = unpack('C C', substr($data, $o + 0x44, 2));
    my ($bgm, $foot, $after) = unpack('C C C', substr($data, $o + 0x52, 3));
    printf "door %d movetype %d flagmode %d flag %d from area+1 %d \"%s\" | u03 %d u04 %d u05 %d u06 \"%s\" u07 \"%s\" | warptype %d attr %d to field %d area+1 %d u10 %d at \"%s\" | cam %d/%d \"%s\" bgm %d foot %d after %d \"%s\"\n",
        $i, $move, $fmode, $flag, $from, str($o + 6, 12), $u03, $u04, $u05, str($o + 0x18, 12), str($o + 0x24, 12),
        $wtype, $attr, $tofield, $toarea, $u10, str($o + 0x38, 12), $cmode, $ctbl, str($o + 0x46, 12), $bgm, $foot, $after,
        str($o + 0x55, 15);
}
