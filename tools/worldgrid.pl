# Offline check of the world map's walking map (WalkGrid world-map walls, 0.0.41; docs/HANDOFF-OVERWORLD.md step 3).
# Usage: perl tools/worldgrid.pl <navdump> <field> <clearance> <lift> <maxstep> <trace file>
#   e.g. perl tools/worldgrid.pl navdump_2026-10-02_20-26-27.txt 2 22 15 45 trace.txt
# The trace file has lines "field x y z": positions the marker really stood on (from [Nav:Trace] log lines, e.g.
#   perl -ne 'if(/fieldID \S+ -> (\d+)/){$f=$1} print "$f $1 $2 $3\n" if /\[Nav:Trace\] pos (\S+) (\S+) (\S+)/ && $f<10' session.log
# Same rules as WalkGrid in world mode: floors Attr 0 with |n.y| >= 0.5 (3 levels per 30-unit cell), walls every other
# polygon of Attr 0 or 0x80; a cell is walkable when its centre or a point 6 or 12 off it lies at least <clearance>
# from every wall's plane (where it projects inside the wall), <lift> above the floor. Prints how many traced positions
# lie on reachable cells and whether each point of interest has a reachable cell near it. WIN="x,z,radius" also draws
# the map around a point (R reachable, . walkable not reached, # blocked).
package poly; use List::Util qw(min max);
our @P;
sub load { my $f=shift; my %seen; open my $h,'<',$f or die; @P=();
  while(<$h>){ next unless /^\s+#(\d+) mode2D (\d+) edges (\d+) n (\S+) (\S+) (\S+) \S+ num (\S+) .*block (\d+) .*attr (0x[0-9a-f]+) .*\| pts (.*)$/; next if $seen{$1}++;
    my @pts = map { [ (split ' ')[0..2] ] } split /\s*\|\s*/, $10; @pts=@pts[0..$3-1];
    push @P, {id=>$1,m=>$2,n=>[$4,$5,$6],num=>$7,block=>$8,attr=>hex($9),p=>\@pts}; } return \@P; }
sub sub3{[$_[0][0]-$_[1][0],$_[0][1]-$_[1][1],$_[0][2]-$_[1][2]]} sub dot{$_[0][0]*$_[1][0]+$_[0][1]*$_[1][1]+$_[0][2]*$_[1][2]}
sub ptri { my($p,$a,$b,$c)=@_; # closest distance point-triangle (Ericson)
  my $ab=sub3($b,$a); my $ac=sub3($c,$a); my $ap=sub3($p,$a); my $d1=dot($ab,$ap); my $d2=dot($ac,$ap);
  return dist($p,$a) if $d1<=0 && $d2<=0; my $bp=sub3($p,$b); my $d3=dot($ab,$bp); my $d4=dot($ac,$bp); return dist($p,$b) if $d3>=0 && $d4<=$d3;
  my $vc=$d1*$d4-$d3*$d2; if($vc<=0&&$d1>=0&&$d3<=0){my $v=$d1/($d1-$d3); return dist($p,[map{$a->[$_]+$v*$ab->[$_]}0..2]);}
  my $cp=sub3($p,$c); my $d5=dot($ab,$cp); my $d6=dot($ac,$cp); return dist($p,$c) if $d6>=0&&$d5<=$d6;
  my $vb=$d5*$d2-$d1*$d6; if($vb<=0&&$d2>=0&&$d6<=0){my $w=$d2/($d2-$d6); return dist($p,[map{$a->[$_]+$w*$ac->[$_]}0..2]);}
  my $va=$d3*$d6-$d5*$d4; if($va<=0&&($d4-$d3)>=0&&($d5-$d6)>=0){my $w=($d4-$d3)/(($d4-$d3)+($d5-$d6)); return dist($p,[map{$b->[$_]+$w*($c->[$_]-$b->[$_])}0..2]);}
  my $den=1/($va+$vb+$vc); my $v=$vb*$den; my $w=$vc*$den; return dist($p,[map{$a->[$_]+$ab->[$_]*$v+$ac->[$_]*$w}0..2]); }
sub dist{ my($a,$b)=@_; sqrt(($a->[0]-$b->[0])**2+($a->[1]-$b->[1])**2+($a->[2]-$b->[2])**2)}
sub pdist { my($p,$q)=@_; my @t=@{$q->{p}}; my $d=ptri($p,@t[0,1,2]); $d=min($d,ptri($p,@t[0,2,3])) if @t>3; $d }
sub inxz { my($q,$x,$z)=@_; my $in=0; my @p=@{$q->{p}}; for(my($i,$j)=(0,$#p);$i<@p;$j=$i++){ my($zi,$zj)=($p[$i][2],$p[$j][2]); if(($zi>$z)!=($zj>$z)){ my $xc=($p[$j][0]-$p[$i][0])*($z-$zi)/($zj-$zi)+$p[$i][0]; $in=!$in if $x<$xc; } } $in }
sub planey { my($q,$x,$z)=@_; my $n=$q->{n}; ($q->{num}-$n->[0]*$x-$n->[2]*$z)/$n->[1] }
package poly;
# The game's style of test: plane distance, counted only when the point projects inside the polygon.
sub projdist { my($p,$q)=@_; my $n=$q->{n}; my $d=dot($n,$p)-$q->{num}; my $pp=[map{$p->[$_]-$d*$n->[$_]}0..2];
  my @t=@{$q->{p}}; return abs($d) if intri($pp,@t[0,1,2]) || (@t>3 && intri($pp,@t[0,2,3])); return 1e9; }
sub intri { my($p,$a,$b,$c)=@_; my $v0=sub3($c,$a); my $v1=sub3($b,$a); my $v2=sub3($p,$a);
  my($d00,$d01,$d02,$d11,$d12)=(dot($v0,$v0),dot($v0,$v1),dot($v0,$v2),dot($v1,$v1),dot($v1,$v2)); my $den=$d00*$d11-$d01*$d01; return 0 if abs($den)<1e-9;
  my $u=($d11*$d02-$d01*$d12)/$den; my $v=($d00*$d12-$d01*$d02)/$den; return $u>=-1e-4 && $v>=-1e-4 && $u+$v<=1+1e-4; }
package main;
# Prototype world-map walking map. Usage: perl wgrid.pl dump field clearance lift maxstep
use List::Util qw(min max); use POSIX qw(floor ceil);
my ($dump,$fld,$CL,$LIFT,$MS)=@ARGV; my $P=poly::load($dump); my $C=30; my $L=3;
my @F = grep { abs($_->{n}[1])>=0.5 && $_->{attr}==0 && @{$_->{p}}>=3 } @$P;
my @W = grep { abs($_->{n}[1])<0.5 && ($_->{attr}==0||$_->{attr}==0x80) } @$P;
my ($x0,$z0,$x1,$z1)=(1e9,1e9,-1e9,-1e9); for my $f(@F){ for(@{$f->{p}}){ $x0=min($x0,$_->[0]);$x1=max($x1,$_->[0]);$z0=min($z0,$_->[2]);$z1=max($z1,$_->[2]); } }
my $gx=floor($x0/$C)*$C+$C/2; my $gz=floor($z0/$C)*$C+$C/2; my $W=ceil(($x1-$gx)/$C)+1; my $H=ceil(($z1-$gz)/$C)+1;
my (@cnt,@hy,@st);
for my $f(@F){ my @p=@{$f->{p}}; my($a,$b,$c,$d)=(min(map{$_->[0]}@p),max(map{$_->[0]}@p),min(map{$_->[2]}@p),max(map{$_->[2]}@p));
  for my $j(max(0,floor(($c-$gz)/$C))..min($H-1,ceil(($d-$gz)/$C))){ for my $i(max(0,floor(($a-$gx)/$C))..min($W-1,ceil(($b-$gx)/$C))){
    my($x,$z)=($gx+$i*$C,$gz+$j*$C); next unless poly::inxz($f,$x,$z); my $y=poly::planey($f,$x,$z); my $k=$j*$W+$i; my $n=$cnt[$k]//0;
    next if grep { abs($hy[$k][$_]-$y)<=20 } 0..$n-1; next if $n>=$L; $hy[$k][$n]=$y; $st[$k][$n]=1; $cnt[$k]=$n+1; } } }
my %bucket; for my $wi(0..$#W){ my @p=@{$W[$wi]{p}}; my($a,$b,$c,$d)=(min(map{$_->[0]}@p)-45,max(map{$_->[0]}@p)+45,min(map{$_->[2]}@p)-45,max(map{$_->[2]}@p)+45);
  for my $j(max(0,floor(($c-$gz)/$C))..min($H-1,ceil(($d-$gz)/$C))){ for my $i(max(0,floor(($a-$gx)/$C))..min($W-1,ceil(($b-$gx)/$C))){ push @{$bucket{$j*$W+$i}},$wi; } } }
my $SUB=$ENV{SUB}//1; my $evals=0;
for my $k(keys %bucket){ my $n=$cnt[$k]//0; next unless $n; my($x,$z)=($gx+($k%$W)*$C,$gz+int($k/$W)*$C); my @ws=map{$W[$_]}@{$bucket{$k}};
  for my $l(0..$n-1){ my $y=$hy[$k][$l]; my $free=0;
    SAMP: for my $ox($SUB?(0,-12,12,-6,6):(0)){ for my $oz($SUB?(0,-12,12,-6,6):(0)){ my $ok=1; $evals++;
      for my $w(@ws){ if(poly::projdist([$x+$ox,$y-$LIFT,$z+$oz],$w)<$CL){$ok=0;last} } if($ok){$free=1; last SAMP} } }
    $st[$k][$l]=2 unless $free; } }
print STDERR "sample evaluations $evals\n";
# flood from the first trace point
open T,'<',$ARGV[5] or die "trace file"; my @tr=map{[split]}grep{/^$fld /}<T>;
sub node { my($x,$y,$z,$r)=@_; my $ci=int(($x-$gx)/$C+0.5); my $cj=int(($z-$gz)/$C+0.5); my($best,$bd)=(-1,1e9);
  for my $j($cj-$r..$cj+$r){for my $i($ci-$r..$ci+$r){ next if $i<0||$j<0||$i>=$W||$j>=$H; my $k=$j*$W+$i; for my $l(0..($cnt[$k]//0)-1){ next unless $st[$k][$l]==1 && abs($hy[$k][$l]-$y)<=80; my $d=($gx+$i*$C-$x)**2+($gz+$j*$C-$z)**2; ($best,$bd)=("$k,$l",$d) if $d<$bd; } } } $best }
my %reach; my $s=node(@{$tr[0]}[1..3],2); my @q=($s); $reach{$s}=1;
my @DX=(0,1,1,1,0,-1,-1,-1); my @DZ=(1,1,0,-1,-1,-1,0,1);
sub step { my($i,$j,$y)=@_; return undef if $i<0||$j<0||$i>=$W||$j>=$H; my $k=$j*$W+$i; my($b,$bd)=(undef,$MS); for my $l(0..($cnt[$k]//0)-1){ my $d=abs($hy[$k][$l]-$y); ($b,$bd)=("$k,$l",$d) if $st[$k][$l]==1 && $d<=$bd } $b }
while(@q){ my($k,$l)=split /,/,shift @q; my($i,$j)=($k%$W,int($k/$W)); my $y=$hy[$k][$l];
  for my $s(0..7){ my $nk=step($i+$DX[$s],$j+$DZ[$s],$y); next unless defined $nk; next if $reach{$nk};
    next if $DX[$s]&&$DZ[$s]&&(!defined step($i+$DX[$s],$j,$y)||!defined step($i,$j+$DZ[$s],$y)); $par{$nk}="$k,$l"; $reach{$nk}=1; push @q,$nk; } }
my ($ok,$near,$bad)=(0,0,0); my @badl;
for my $t(@tr){ my $n0=node(@{$t}[1..3],0); my $n1=node(@{$t}[1..3],1);
  if(defined $n0 && $n0 ne '-1' && $reach{$n0}){$ok++} elsif($n1 ne '-1' && $reach{$n1}){$near++} else {$bad++; push @badl, "@$t"} }
printf "grid %dx%d, %d floors, %d walls, reachable nodes %d; trace: %d on a reachable cell, %d next to one, %d not (%s)\n",$W,$H,scalar@F,scalar@W,scalar(keys %reach),$ok,$near,$bad,join('; ',@badl[0..min(5,$#badl)]);
# points of interest from the dump
open D,'<',$dump; while(<D>){ next unless /^\s+gEvnRng (\d+) \S+ \S+ type (\d+) name "([^"]+)" flag \S+ pos (\S+) (\S+) (\S+) .*box type \S+ size (\S+) (\S+) (\S+)/; next if $seen{$1}++;
  my($ix,$ty,$nm,$x,$y,$z)=($1,$2,$3,$4,$5,$6); next if $nm=~/eve/; my $fy=$nm=~/d_/?$y+100:$y; my $r=$nm=~/d_/?150:250; my $rr=int($r/$C)+1; my $hit=''; my $bestd;
  my $ci=int(($x-$gx)/$C+0.5); my $cj=int(($z-$gz)/$C+0.5);
  OUT: for my $j($cj-$rr..$cj+$rr){for my $i($ci-$rr..$ci+$rr){ next if $i<0||$j<0||$i>=$W||$j>=$H; next if ($gx+$i*$C-$x)**2+($gz+$j*$C-$z)**2>$r*$r; my $k=$j*$W+$i;
    for my $l(0..($cnt[$k]//0)-1){ if($reach{"$k,$l"} && abs($hy[$k][$l]-$fy)<=200){ my $dd=sqrt(($gx+$i*$C-$x)**2+($gz+$j*$C-$z)**2); if(!defined $bestd || $dd<$bestd){$bestd=$dd; $hit=sprintf "reachable, nearest cell %.0f from it, height %+.0f", $dd, $hy[$k][$l]-$fy;} } } } }
  printf "  %-12s %s\n", $nm, $hit||'NOT reachable'; }
if($ENV{WIN}){ my($wx,$wz,$wr)=split /,/,$ENV{WIN}; my $ci=int(($wx-$gx)/$C+0.5); my $cj=int(($wz-$gz)/$C+0.5); my $r=int($wr/$C);
  print "map around $wx $wz (north up; R reachable, . walkable not reached, # blocked, : floor only other level within 80, ' ' none, X target)\n";
  for(my $j=$cj+$r;$j>=$cj-$r;$j--){ my $row=''; for my $i($ci-$r..$ci+$r){ if($i==$ci&&$j==$cj){$row.='X';next} my $k=$j*$W+$i; my $c=' ';
    for my $l(0..($cnt[$k]//0)-1){ my $ch=$reach{"$k,$l"}?'R':$st[$k][$l]==1?'.':'#'; $c=$ch if $c eq ' ' || $ch eq 'R' || ($ch eq '.' && $c eq '#'); }
    $row.=$c; } print "$row\n"; } }
