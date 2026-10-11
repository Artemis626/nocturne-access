#!/usr/bin/perl
# Read-only: every direct call (E8 rel32) in GameAssembly.dll's .text to the given method RVAs, named by symmap.txt.
# Usage: perl tools/native/callers.pl <GameAssembly.dll> <symmap.txt> <rva hex> [<rva hex> ...]
# .text: virtual address 0x18019B000, file offset 0x19A000, size 0x2722800 (llvm-readobj --sections; README section 3).
use strict; use warnings;
my ($dll, $map, @targets) = @ARGV;
my ($tva, $toff, $tsize) = (0x19B000, 0x19A000, 0x2722800);   # RVAs (image base 0x180000000 left out)
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
    return $s && $rva < $s->[0] + $s->[2] ? sprintf("%s+0x%X", $s->[1], $rva - $s->[0]) : "?";
}
open my $fh, '<:raw', $dll or die "$dll: $!";
seek $fh, $toff, 0;
my $text;
read $fh, $text, $tsize;
close $fh;
while ($text =~ /\xE8/g) {
    my $i = pos($text) - 1;
    last if $i + 5 > length $text;
    my $rel = unpack 'l<', substr($text, $i + 1, 4);
    my $dest = $tva + $i + 5 + $rel;
    next unless exists $want{$dest};
    my $at = $tva + $i;
    printf "%08X -> %s: %s\n", $at, $want{$dest}, name_of($at);
}
