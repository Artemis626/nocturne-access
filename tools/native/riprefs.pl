#!/usr/bin/perl
# Read-only: every RIP-relative 64-bit load (mov reg, [rip+disp32]: 48/4C 8B modrm with mod 00 and rm 101) in
# GameAssembly.dll's .text that reads one of the given global addresses (for example a class TypeInfo pointer from
# typeinfo.txt), named by symmap.txt; code outside every known method is shown as "?".
# Usage: perl tools/native/riprefs.pl <GameAssembly.dll> <symmap.txt> <va hex> [<va hex> ...]
use strict; use warnings;
my ($dll, $map, @targets) = @ARGV;
my ($tva, $toff, $tsize) = (0x18019B000, 0x19A000, 0x2722800);
my %want = map { hex($_) => $_ } @targets;
my @syms;
open my $m, '<', $map or die "$map: $!";
while (<$m>) { push @syms, [hex($1), $2, hex($3)] if /^([0-9A-Fa-f]+)\t(.*)\t([0-9A-Fa-f]+)$/; }
close $m;
@syms = sort { $a->[0] <=> $b->[0] } @syms;
sub name_of {
    my ($rva) = @_;
    my ($lo, $hi) = (0, $#syms);
    while ($lo < $hi) { my $mid = int(($lo + $hi + 1) / 2); if ($syms[$mid][0] <= $rva) { $lo = $mid } else { $hi = $mid - 1 } }
    my $s = $syms[$lo];
    return $s && $rva < $s->[0] + $s->[2] ? sprintf("%s+0x%X", $s->[1], $rva - $s->[0]) : sprintf("? (after %s)", $s ? $s->[1] : '-');
}
open my $fh, '<:raw', $dll or die "$dll: $!";
seek $fh, $toff, 0;
my $text;
read $fh, $text, $tsize;
close $fh;
while ($text =~ /[\x48\x4C]\x8B[\x05\x0D\x15\x1D\x25\x2D\x35\x3D]/g) {
    my $i = pos($text) - 3;
    my $disp = unpack 'l<', substr($text, $i + 3, 4);
    my $dest = $tva + $i + 7 + $disp;
    next unless exists $want{$dest};
    printf "%08X reads %s: %s\n", $tva + $i - 0x180000000, $want{$dest}, name_of($tva + $i - 0x180000000);
}
