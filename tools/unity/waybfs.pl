# The way through a field without visiting it: a breadth-first search over (area, arrival point) nodes. Each node's area file
# (fNNN_AAA.f2) is flooded from the arrival point with tools/navregions.pl (the game's walking rules); every door volume reached
# leads on along the door table (wap.txt from tools/unity/wap.pl); extra links (script doors such as the Third Kalpa's stat doors)
# come from EXTRA lines. Prints the shortest chain of volumes from the start to the goal volume.
# Args: wap.txt dir-of-f2 field start-area start-point goal-area goal-volume [extra-file]
# Extra file lines: "area volume -> area point [label]" (a volume that warps the player by script; label e.g. "Strength 15").
# Env: SKIP="a:vol a:vol" leaves volumes out (e.g. other stats' doors).
use strict; use warnings; use FindBin;
my ($wap, $dir, $field, $sa, $sp, $ga, $gv, $extra) = @ARGV;
my $tmp = ($ENV{TMPDIR} // $ENV{TEMP} // '.') . "/waybfs_$$.txt";
my %skip = map { $_ => 1 } split ' ', ($ENV{SKIP} // '');
my %link;   # "area:vol" -> [ [area, point, label] ... ]
open my $w, '<', $wap or die "$wap: $!"; my @r = <$w>; close $w;
for my $i (0 .. $#r) {
  my ($fa, $nm) = $r[$i] =~ /from area\+1 (\d+) "([^"]+)"/ or next;
  my ($flag) = $r[$i] =~ /flag (\d+) from/;
  my ($tf, $ta, $tp, $after) = $r[$i] =~ / to field (\d+) area\+1 (\d+) u10 \d+ at "([^"]*)".* after (\d+)/ or next;
  if ($after == 1 && ($r[$i + 1] // '') =~ /movetype 10 .*from area\+1 \d+ "" .* to field (\d+) area\+1 (\d+) u10 \d+ at "([^"]*)"/) { ($tf, $ta, $tp) = ($1, $2, $3) }
  my $a = $fa - 1; my $da = $ta == 0 ? $a : $ta - 1;
  my $lab = $flag ? "flag $flag" : '';
  $da = "field$tf" if $tf && $tf != $field;
  push @{ $link{"$a:$nm"} }, [$da, $tp, $lab];
}
if ($extra) { open my $e, '<', $extra or die; while (<$e>) { s/#.*//; my ($a, $v, $da, $dp, $lab) = /^\s*(\d+)\s+(\S+)\s*->\s*(\d+)\s+(\S+)\s*(.*?)\s*$/ or next; unshift @{ $link{"$a:$v"} }, [$da, $dp, $lab] } }
my %flood;
sub reach { my ($a, $p) = @_; return @{ $flood{"$a:$p"} } if $flood{"$a:$p"};
  my $f = sprintf "%s/f%03d_%03d.f2", $dir, $field, $a + 1; my @out;
  print STDERR "flood a$a $p\n" if $ENV{TRACE};
  if (-f $f) { my ($x, $y, $z); for (`perl "$FindBin::Bin/f2pos.pl" "$f"`) { my @c = split; if ($c[0] eq $p) { ($x, $y, $z) = @c[1 .. 3]; last } }
    if (defined $x) { system(qq{perl "$FindBin::Bin/f2dump.pl" "$f" $field $a $x $z $y > "$tmp"});
      my $on = 0; for (`perl "$FindBin::Bin/../navregions.pl" "$tmp" 2>&1`) { $on = 1 if /one-way flood/; next unless $on; push @out, $1 if /^\s+(\S+)\s+point\s+reached at y: -?\d/ } } }
  $flood{"$a:$p"} = \@out; return @out }
my %seen = ("$sa:$sp" => 1); my @q = ([$sa, $sp, []]);
while (my $n = shift @q) { my ($a, $p, $path) = @$n;
  for my $v (reach($a, $p)) { next if $skip{"$a:$v"};
    if ($a eq $ga && $v eq $gv) { print join("\n", @$path, "a$a $v (goal)"), "\n"; unlink $tmp; exit 0 }
    for my $l (@{ $link{"$a:$v"} // [] }) { my ($da, $dp, $lab) = @$l; next if $da =~ /^field/ || $seen{"$da:$dp"}++;
      push @q, [$da, $dp, [@$path, sprintf("a%s %s -> a%s %s%s", $a, $v, $da, $dp, $lab ? " ($lab)" : '')]] } } }
unlink $tmp; print "no way from a$sa $sp to a$ga $gv\n"; exit 1;
