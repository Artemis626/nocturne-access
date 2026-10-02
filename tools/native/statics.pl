# Usage: perl statics.pl research/native/acs Class   : static field offsets (from TypeInfo+0xB8) of a
# Cpp2IL dummy class, in declaration order with natural alignment. Struct sizes come from their own
# dummies (instance FieldOffset attributes include the 0x10 object header). Read-only aid for the asm.
use strict; use warnings;
use File::Find;
my ($root, $cls) = @ARGV;
my %file;
find(sub { $file{$1} //= $File::Find::name if /^(.+)\.cs$/; }, $root);
my %prim = (int => [4,4], uint => [4,4], float => [4,4], bool => [1,1], byte => [1,1], sbyte => [1,1],
  short => [2,2], ushort => [2,2], char => [2,2], long => [8,8], ulong => [8,8], double => [8,8],
  Vector4 => [16,4], Vector3 => [12,4], Vector2 => [8,4], Quaternion => [16,4], Color => [16,4],
  Matrix4x4 => [64,4], Vector2Int => [8,4], Vector3Int => [12,4]);
my %cache;
sub slurp { my $n = shift; my $f = $file{$n} or return undef; open my $h, '<', $f or return undef; local $/; my $s = <$h>; $s }
sub layout {   # [size, align] of a type as a field
  my $t = shift;
  return [8,8] if $t =~ /\[\]$|^string$|^object$|</;
  return $prim{$t} if $prim{$t};
  return $cache{$t} if $cache{$t};
  my $src = slurp($t);
  return $cache{$t} = [8,8] unless $src && $src =~ /\bstruct\s+\Q$t\E\b/;
  my $isenum = $src =~ /\benum\s+\Q$t\E\b/; return $cache{$t} = [4,4] if $isenum;
  my ($size, $align) = (0, 1); my $off;
  for my $l (split /\n/, $src) {
    if ($l =~ /FieldOffset\(Offset = "0x([0-9A-Fa-f]+)"\)/) { $off = hex($1) - 0x10; next; }
    if (defined $off && $l =~ /^\s*(?:public|private|internal|protected)\s+(?:readonly\s+)?([\w.<>\[\], ]+?)\s+(\w+)\s*;/ && $l !~ /\bstatic\b/) {
      my $fl = layout($1); $size = $off + $fl->[0] if $off + $fl->[0] > $size; $align = $fl->[1] if $fl->[1] > $align; undef $off;
    }
  }
  $size = ($size + $align - 1) & ~($align - 1);
  return $cache{$t} = [$size || 1, $align];
}
my $src = slurp($cls) or die "no dummy for $cls\n";
my $off = 0;
for my $l (split /\n/, $src) {
  next unless $l =~ /^\s*(?:public|private|internal|protected)\s+static\s+(?:readonly\s+)?([\w.<>\[\], ]+?)\s+(\w+)\s*;\s*$/;
  my ($t, $n) = ($1, $2);
  my ($s, $a) = @{ layout($t) };
  $off = ($off + $a - 1) & ~($a - 1);
  printf "0x%02X\t%s %s%s\n", $off, $t, $n, ($s != 8 && !$prim{$t} ? "   (struct, $s bytes)" : '');
  $off += $s;
}
