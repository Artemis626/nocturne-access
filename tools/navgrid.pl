# Offline check of the walkability grid (docs/PATHFINDING.md section 2.2): rebuilds WalkGrid from a navigation dump
# (UserData/NocturneAccess/NavDumps/navdump_*.txt, polygon lines "#N mode2D ... | pts ...") and prints the map, north at the top.
# Usage: perl tools/navgrid.pl <dump> <playerX> <playerY> <playerZ> ["x,z,char" ...]   e.g. "-5400,-6360,P"
# Same rules as WalkGrid: real floors (Attr 0) within 60 of the player Y, cells of 30, blocked within 65 of vertical
# polygons overlapping 5..180 above the player (Y points down). "." walkable, "#" blocked, " " no floor.
use strict; use warnings;
my ($file,$px,$py,$pz)=@ARGV; my %poly;
open F,"<",$file or die;
while(<F>){ next unless /#(\d+) mode2D \d+ edges \d+ n (\S+) (\S+) (\S+) .*attr (0x[0-9a-f]+) .*\| pts (.*)$/; my($id,$nx,$ny,$nz,$attr,$pts)=($1,$2,$3,$4,hex($5),$6); next if $poly{$id};
  my @p = map { [split ' '] } split /\s*\|\s*/, $pts; $poly{$id}={ny=>$ny,attr=>$attr,p=>\@p}; }
my (@floors,@walls); my ($x0,$z0,$x1,$z1)=(1e9,1e9,-1e9,-1e9);
for my $q (values %poly){ my @p=@{$q->{p}}; next if @p<2;
  if(abs($q->{ny})>=0.5){ next if @p<3 || $q->{attr} || abs($p[0][1]-$py)>60; push @floors,$q; for(@p){ $x0=$_->[0] if $_->[0]<$x0; $x1=$_->[0] if $_->[0]>$x1; $z0=$_->[2] if $_->[2]<$z0; $z1=$_->[2] if $_->[2]>$z1; } }
  elsif(abs($q->{ny})<0.2){ my ($lo,$hi)=(1e9,-1e9); for(@p){$lo=$_->[1] if $_->[1]<$lo; $hi=$_->[1] if $_->[1]>$hi;} next if $hi < $py-180 || $lo > $py-5;
    my ($a,$b,$best)=($p[0],$p[0],-1); for my $i(0..$#p){for my $j($i+1..$#p){my $d=($p[$i][0]-$p[$j][0])**2+($p[$i][2]-$p[$j][2])**2; if($d>$best){$best=$d;$a=$p[$i];$b=$p[$j]}}} push @walls,[$a->[0],$a->[2],$b->[0],$b->[2]]; } }
my $C=30; my $gx=int(($x0)/$C-0.5)*$C+$C/2; $gx=(POSIX::floor($x0/$C))*$C+$C/2; my $gz=(POSIX::floor($z0/$C))*$C+$C/2;
use POSIX; my $W=int(($x1-$gx)/$C)+2; my $H=int(($z1-$gz)/$C)+2;
sub inside{ my($pts,$x,$z)=@_; my $in=0; my @p=@$pts; for(my($i,$j)=(0,$#p);$i<@p;$j=$i++){ my($zi,$zj)=($p[$i][2],$p[$j][2]); if(($zi>$z)!=($zj>$z)){ my $xc=($p[$j][0]-$p[$i][0])*($z-$zi)/($zj-$zi)+$p[$i][0]; $in=!$in if $x<$xc; } } return $in; }
sub sd{ my($px,$pz,$ax,$az,$bx,$bz)=@_; my($dx,$dz)=($bx-$ax,$bz-$az); my $l=$dx*$dx+$dz*$dz; my $t=$l<=0?0:(($px-$ax)*$dx+($pz-$az)*$dz)/$l; $t=0 if $t<0; $t=1 if $t>1; return sqrt(($ax+$t*$dx-$px)**2+($az+$t*$dz-$pz)**2); }
my @g;
for my $j(0..$H-1){ for my $i(0..$W-1){ my($x,$z)=($gx+$i*$C,$gz+$j*$C); my $c=0; for my $f(@floors){ if(inside($f->{p},$x,$z)){$c=1;last} } if($c){ for my $w(@walls){ if(sd($x,$z,@$w)<65){$c=2;last} } } $g[$j][$i]=$c; } }
printf "floors %d walls %d grid %dx%d from %d %d\n", scalar @floors, scalar @walls, $W,$H,$gx,$gz;
my @marks=@ARGV[4..$#ARGV];
for(my $j=$H-1;$j>=0;$j--){ my $row=join '', map { $g[$j][$_]==1?'.':$g[$j][$_]==2?'#':' ' } 0..$W-1;
  for my $m(@marks){ my($mx,$mz,$ch)=split /,/,$m; my $i=int(($mx-$gx)/$C+0.5); my $jj=int(($mz-$gz)/$C+0.5); substr($row,$i,1)=$ch if $jj==$j && $i>=0 && $i<$W; }
  $row =~ s/\s+$//; print "$row\n" if $row ne ''; }
