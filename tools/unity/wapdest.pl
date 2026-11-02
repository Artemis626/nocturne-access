# Per area of a field (fNNN files; field 34 when written): each door volume, its position, and where the game really puts the player (after_flag 1: the next
# unnamed movetype 10 row). Args: wap.txt dir-of-f2 areas...
use strict; use warnings; use FindBin;
my ($wap, $dir, @areas) = @ARGV;
my %pos;   # area -> name -> [x,z]
for my $a (0 .. 40) { my $f = sprintf "%s/f%03d_%03d.f2", $dir, $ENV{FIELD} // 34, $a + 1; next unless -f $f;
  for (`perl "$FindBin::Bin/f2pos.pl" "$f"`) { my ($n, $x, $y, $z) = split; $pos{$a}{$n} //= [$x, $z]; } }
my @r; open my $w, '<', $wap or die; while (<$w>) { chomp; push @r, $_ } close $w;
my %first;
for my $i (0 .. $#r) {
  my ($mt, $flag, $fa, $nm) = $r[$i] =~ /movetype (\d+) flagmode \d+ flag (\d+) from area\+1 (\d+) "([^"]*)"/ or next;
  next if $nm eq '';
  my $a = $fa - 1; next unless grep { $_ == $a } @areas;
  my ($wt, $tf, $ta, $tp, $after) = $r[$i] =~ /warptype (\d+) .* to field (\d+) area\+1 (\d+) u10 \d+ at "([^"]*)".* after (\d+)/;
  my $how = '';
  if ($after == 1 && $r[$i + 1] =~ /movetype 10 .*from area\+1 \d+ "" .* to field (\d+) area\+1 (\d+) u10 \d+ at "([^"]*)"/) { ($tf, $ta, $tp) = ($1, $2, $3); $how = ' (after-warp)'; }
  my $da = $ta == 0 ? $a : $ta - 1; $da = "f$tf" if $tf && $tf != 34;
  my $p = $pos{$a}{$nm} // ['?', '?']; my $q = $pos{$da}{$tp} // ['?', '?'];
  printf "a%-2d %-7s mt%-2s %s at %5s %5s -> a%-3s %-8s at %5s %5s%s\n", $a, $nm, $mt, ($flag ? "flag $flag" : ''), @$p, $da, $tp, @$q, $how;
}
