# Usage: perl annot.pl symmap.txt < objdump-output : names call/jmp targets that are method starts.
use strict; use warnings;
no warnings 'portable';
my %sym; open my $m, '<', shift or die; while (<$m>) { chomp; my ($r, $n) = split /\t/; $sym{hex($r) + 0x180000000} = $n; }
while (my $l = <STDIN>) {
  $l =~ s/<[^>]*>//g;
  if ($l =~ /(call|jmp)q?\s+0x([0-9a-f]+)/) { my $a = hex($2); $l =~ s/\s*$/    ; -> $sym{$a}\n/ if $sym{$a}; }
  print $l;
}
