# Reads ilspycmd output of a Cpp2IL dummy assembly (MelonLoader's cpp2il_out) and prints, per method:
#   RVA <TAB> Namespace.Class.Method(signature) <TAB> Length
# RVA and Length come from the [Address(RVA = "0x...", Offset = "0x...", Length = "0x...")] attribute.
use strict; use warnings;
my @out;
for my $file (@ARGV) {
  open my $fh, '<', $file or next;
  my ($ns, @cls, $rva, $len) = ('');
  my $depth = 0; my @clsDepth;
  while (my $l = <$fh>) {
    $ns = $1 if $l =~ /^namespace\s+([\w.]+)/;
    if ($l =~ /\b(?:class|struct|interface|enum)\s+(\w+)/ && $l !~ /\(/) { push @cls, $1; push @clsDepth, $depth; }
    if ($l =~ /\[Address\(RVA = "0x([0-9A-Fa-f]+)".*?Length = "0x([0-9A-Fa-f]+)"/) { $rva = $1; $len = $2; }
    if (defined $rva && $l =~ /^\s*(?:public|private|internal|protected)[^=;]*?\s([\w<>\.]+)\s*\(([^)]*)\)/) {
      my ($m, $args) = ($1, $2); $args =~ s/\s+/ /g;
      push @out, sprintf("%08X\t%s%s.%s(%s)\t%X", hex($rva), ($ns ? "$ns." : ""), join('.', @cls), $m, $args, hex($len));
      undef $rva; undef $len;
    }
    my $o = () = $l =~ /\{/g; my $c = () = $l =~ /\}/g; $depth += $o - $c;
    while (@clsDepth && $depth <= $clsDepth[-1] && $c) { pop @cls; pop @clsDepth; }
  }
}
print "$_\n" for sort @out;
