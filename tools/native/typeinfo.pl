# Usage: llvm-objdump -d ... GameAssembly.dll | perl typeinfo.pl symmap.txt > typeinfo.txt
# Names the IL2CPP class pointers (TypeInfo globals) that methods load before touching static fields.
# A load `movq X(%rip), %reg` followed by the class-init test `testb $0x2, 0x12f(%reg)` marks X as a
# TypeInfo global. Each global is listed with the classes whose methods use it, most frequent first;
# a class's own methods usually dominate, which names it (check the static field offsets before use).
use strict; use warnings;
no warnings 'portable';
my @starts; my %name;
open my $m, '<', shift or die;
while (<$m>) { chomp; my ($r, $n, $len) = split /\t/; my $a = hex($r) + 0x180000000; next if $a == 0x180000000; push @starts, $a; $name{$a} = $n; }
@starts = sort { $a <=> $b } @starts;
my ($i, $cur) = (0, '');
my %last;     # register -> global last loaded into it
my %uses;     # global -> class -> count of methods
my %seen;     # "global|method" already counted
while (my $l = <STDIN>) {
  next unless $l =~ /^\s*([0-9a-f]+):\s+(\S+)\s+(.*)$/;
  my ($addr, $op, $rest) = (hex($1), $2, $3);
  while ($i < @starts && $starts[$i] <= $addr) { $cur = $name{$starts[$i]}; $i++; %last = (); }
  if ($op eq 'movq' && $rest =~ /\(%rip\),\s*%(\w+)\s+#\s*0x([0-9a-f]+)/) { $last{$1} = $2; next; }
  if ($op eq 'testb' && $rest =~ /^\$0x2,\s*0x12f\(%(\w+)\)/ && $last{$1}) {
    my $g = $last{$1};
    my $cls = $cur; $cls =~ s/\.[^.(]+\(.*$//;
    next if $seen{"$g|$cur"}++;
    $uses{$g}{$cls}++;
  }
}
for my $g (sort keys %uses) {
  my $c = $uses{$g};
  my @top = (sort { $c->{$b} <=> $c->{$a} || $a cmp $b } keys %$c)[0 .. 4];
  print "0x$g\t", join(', ', map { "$_ $c->{$_}" } grep { defined } @top), "\n";
}
