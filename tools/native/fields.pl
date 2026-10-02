# Usage: perl fields.pl Class.cs : prints "offset  declaration" for every instance field of a Cpp2IL dummy class
# (static fields carry no offset there: use statics.pl).
use strict; use warnings;
my $off = '';
while (<>) {
  if (/FieldOffset\(Offset = "(0x[0-9A-Fa-f]+)"\)/) { $off = $1; next; }
  if ($off ne '' && /^\s*(public|private|internal|protected)[^(]*;\s*$/) { s/^\s+//; print "$off\t$_"; $off = ''; }
}
