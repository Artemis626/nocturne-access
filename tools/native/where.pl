use strict; use warnings; no warnings 'portable';
my %s; open my $m, '<', $ARGV[0]; while (<$m>) { chomp; my ($r, $n) = split /\t/; $s{hex $r} = $n }
my @st = sort { $a <=> $b } keys %s;
open my $h, '<', $ARGV[1];
while (<$h>) { chomp; my ($f, $at, $t) = split /\t/; my $a = hex $at;
  my ($lo, $hi) = (0, $#st); while ($lo < $hi) { my $mid = int(($lo + $hi + 1) / 2); if ($st[$mid] <= $a) { $lo = $mid } else { $hi = $mid - 1 } }
  printf "flag %-5s at %s %-26s in %s\n", $f, $at, ($s{hex $t} // "?$t"), $s{$st[$lo]}; }
