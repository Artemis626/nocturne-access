#!/usr/bin/perl
# Writes the bytes of named TextAsset objects (m_Script) from a game asset bundle to a folder.
# Usage: perl tools/unity/extract.pl <bundle> <out dir> <name regex>
use strict; use warnings;
use FindBin; use lib $FindBin::Bin;
use UnityBundle;
my ($file, $out, $re) = @ARGV;
mkdir $out;
my $b = UnityBundle::open_bundle($file);
my $n = 0;
for my $node (@{ $b->{nodes} }) {
    next if $node->{path} =~ /\.res[S]?$/;
    my $sf = eval { UnityBundle::parse_serialized($node->{data}) } or next;
    for my $o (@{ $sf->{objects} }) {
        next unless $o->{type}{classid} == 49;
        my $d = $sf->{data};
        my $p = $o->{offset};
        my $len = unpack 'l<', substr($$d, $p, 4);
        my $name = substr($$d, $p + 4, $len);
        next unless $name =~ /$re/;
        $p = ($p + 4 + $len + 3) & ~3;
        my $blen = unpack 'l<', substr($$d, $p, 4);
        (my $safe = $name) =~ s/[^\w.\-]/_/g;
        open my $fh, '>:raw', "$out/$safe" or die "$out/$safe: $!";
        print $fh substr($$d, $p + 4, $blen);
        close $fh;
        $n++;
    }
}
print "$n files written to $out\n";
