# Usage: perl annot-ti.pl typeinfo.txt < asm : appends the likely class of each TypeInfo global load.
use strict; use warnings;
my %ti; open my $t, '<', shift or die; while (<$t>) { my ($g, $c) = split /\t/; ($c) = split /,/, $c; $c =~ s/ \d+$//; $ti{lc $g} = $c; }
while (my $l = <STDIN>) { if ($l =~ /#\s*(0x[0-9a-f]+)/ && $ti{lc $1}) { chomp $l; $l .= "   [$ti{lc $1}]\n"; } print $l; }
