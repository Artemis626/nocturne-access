# Reads ilspycmd output of a Cpp2IL dummy assembly; prints "RVA<TAB>Class.Method(signature)" per method.
use strict; use warnings;
my @out;
for my $file (@ARGV) {
  open my $fh, '<', $file or next;
  my ($ns, @cls, $rva) = ('');
  my $depth = 0; my @clsDepth;
  while (my $l = <$fh>) {
    $ns = $1 if $l =~ /^namespace\s+([\w.]+)/;
    if ($l =~ /\b(?:class|struct|interface|enum)\s+(\w+)/ && $l !~ /\(/) { push @cls, $1; push @clsDepth, $depth; }
    $rva = $1 if $l =~ /\[Address\(RVA = "0x([0-9A-Fa-f]+)"/;
    if (defined $rva && $l =~ /^\s*(?:public|private|internal|protected)[^=;]*?\s([\w<>\.]+)\s*\(([^)]*)\)/) {
      my ($m, $args) = ($1, $2); $args =~ s/\s+/ /g;
      push @out, sprintf("%08X\t%s%s.%s(%s)", hex($rva), ($ns ? "$ns." : ""), join('.', @cls), $m, $args);
      undef $rva;
    }
    my $o = () = $l =~ /\{/g; my $c = () = $l =~ /\}/g; $depth += $o - $c;
    while (@clsDepth && $depth <= $clsDepth[-1] && $c) { pop @cls; pop @clsDepth; }
  }
}
print "$_\n" for sort @out;
