# Finds "mov ecx, imm32" for the given flags followed within 24 bytes by a call; prints RVA of call and target.
use strict; use warnings; no warnings 'portable';
my ($file, $vma, $foff, $size, @flags) = @ARGV; $vma = hex($vma); $foff = hex($foff); $size = hex($size);
open my $fh, '<:raw', $file or die; seek $fh, $foff, 0; read $fh, my $buf, $size;
for my $f (@flags) {
  my $pat = pack('C', 0xB9) . pack('V', $f);
  my $pos = 0;
  while (($pos = index($buf, $pat, $pos)) >= 0) {
    for my $k (5 .. 28) {
      my $p = $pos + $k; last if $p + 5 > length $buf;
      if (ord(substr($buf, $p, 1)) == 0xE8) {
        my $rel = unpack('l<', substr($buf, $p + 1, 4));
        my $at = $vma + $p; my $tgt = $at + 5 + $rel;
        printf "%d\t%08X\t%08X\n", $f, $at - 0x180000000, $tgt - 0x180000000; last;
      }
    }
    $pos++;
  }
}
