# Usage: perl consts.pl GameAssembly.dll VA... : prints the float (and int) at each virtual address of the
# .xdata section (constants the code loads with movss X(%rip)). Read-only.
use strict; use warnings; no warnings 'portable';
my $f = shift; open my $h, '<:raw', $f or die;
my ($va, $raw) = (0x1828BE000, 0x28BC800);
for my $a (@ARGV) { my $v = hex($a); seek $h, $v - $va + $raw, 0; read $h, my $b, 4; printf "%s\t%g\t%d\n", $a, unpack('f<', $b), unpack('l<', $b); }
