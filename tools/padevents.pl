#!/usr/bin/perl
# Usage: perl tools/padevents.pl <dumps...> (e.g. the whole UserData/NocturneAccess/NavDumps folder); 0.0.69 names an event only from a pad within 100.
# For every dump: event volumes (NNeve_MM, type 10) that a place pad would name (PlaceName rules: pad Attr 0x1000,
# basyo > 0, at most 1000 across, within 300, pad height within 150 of y+100), with their distance to the pad, whether
# the centre or box lies on the pad, and the nearest door named by the same basyo.
use strict; use warnings;
for my $f (@ARGV) {
    open my $fh, '<', $f or next;
    my (@pads, %rng, $field, $area);
    while (<$fh>) {
        ($field, $area) = ($1, $2) if !defined $field && /fieldID (\d+) areaID (\d+)/;
        if (/^\s+(?:d [\d.]+ )?#(\d+) mode2D 1 .*?attr 0x1000 .*?basyo (\d+) .*?\| pts (.*)$/) {
            my ($id, $b, $pts) = ($1, $2, $3);
            next unless $b > 0;
            my @p = $pts =~ /(-?[\d.]+) (-?[\d.]+) (-?[\d.]+) 1/g;
            my ($x0,$x1,$z0,$z1,$y) = (1e9,-1e9,1e9,-1e9,$p[1]);
            for (my $i = 0; $i < @p; $i += 3) { $x0=$p[$i] if $p[$i]<$x0; $x1=$p[$i] if $p[$i]>$x1; $z0=$p[$i+2] if $p[$i+2]<$z0; $z1=$p[$i+2] if $p[$i+2]>$z1; }
            next if $x1-$x0 > 1000 || $z1-$z0 > 1000;
            push @pads, { id=>$id, b=>$b, x0=>$x0, x1=>$x1, z0=>$z0, z1=>$z1, y=>$y };
        }
        if (/^\s+gEvnRng (\d+) id \d+ type 10 name "([^"]+)" .*? pos (-?[\d.]+) (-?[\d.]+) (-?[\d.]+) 1 .*?size (\S+) (\S+) (\S+)/) {
            $rng{$1} //= { name=>$2, x=>$3, y=>$4, z=>$5, size=>"$6 $7 $8" };
        }
    }
    close $fh;
    next unless defined $field;
    my $pad = sub {
        my ($r) = @_;
        my ($best, $bd) = (undef, 300);
        for my $c (@pads) {
            next if abs($c->{y} - ($r->{y} + 100)) > 150;
            my $dx = ::max(0, $c->{x0} - $r->{x}, $r->{x} - $c->{x1});
            my $dz = ::max(0, $c->{z0} - $r->{z}, $r->{z} - $c->{z1});
            my $d = sqrt($dx*$dx + $dz*$dz);
            ($best, $bd) = ($c, $d) if $d <= $bd;
        }
        return ($best, $bd);
    };
    for my $i (sort { $a <=> $b } keys %rng) {
        my $r = $rng{$i};
        next unless $r->{name} =~ /^\d+eve_\d+$/;
        my ($p, $d) = $pad->($r);
        next unless $p;
        my ($door, $dd) = ('-', 1e9);
        for my $j (keys %rng) {
            my $o = $rng{$j};
            next unless $o->{name} =~ /^\d+d_\d+$/;
            my ($op) = $pad->($o);
            next unless $op && $op->{b} == $p->{b};
            my $e = sqrt(($o->{x}-$r->{x})**2 + ($o->{z}-$r->{z})**2);
            ($door, $dd) = ($o->{name}, $e) if $e < $dd;
        }
        my $samepad = '';
        if ($door ne '-') { my ($o) = grep { $_->{name} eq $door } values %rng; my ($op) = $pad->($o); $samepad = $op->{id} == $p->{id} ? 'same pad' : 'other pad'; }
        printf "%s f%d a%d %-9s at %6.0f %6.0f size %-11s pad #%d basyo %d d %4.0f | door %s %s\n",
            substr($f, -23, 19), $field, $area, $r->{name}, $r->{x}, $r->{z}, $r->{size}, $p->{id}, $p->{b}, $d,
            $door eq '-' ? '-' : sprintf("%s %.0f", $door, $dd), $samepad;
    }
}
sub max { my $m = shift; for (@_) { $m = $_ if $_ > $m } $m }
