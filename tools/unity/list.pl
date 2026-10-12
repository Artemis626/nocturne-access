#!/usr/bin/perl
# Lists the objects of a game asset bundle: node, path id, class id, size, name (TextAsset / MonoBehaviour m_Name).
# Usage: perl tools/unity/list.pl <bundle> [name filter regex]
use strict; use warnings;
use FindBin; use lib $FindBin::Bin;
use UnityBundle;
my ($file, $filter) = @ARGV;
my $b = UnityBundle::open_bundle($file);
for my $node (@{ $b->{nodes} }) {
    printf "node %s (%d bytes)\n", $node->{path}, length $node->{data};
    next if $node->{path} =~ /\.res[S]?$/;
    my $sf = eval { UnityBundle::parse_serialized($node->{data}) };
    if (!$sf) { print "  not a serialized file: $@"; next; }
    printf "  serialized version %d, %d types, %d objects\n", $sf->{version}, scalar @{ $sf->{types} }, scalar @{ $sf->{objects} };
    for my $o (@{ $sf->{objects} }) {
        my $name = '';
        my $cid = $o->{type}{classid};
        if ($o->{type}{tree} && ($cid == 49 || $cid == 114 || $cid == 28 || $cid == 1)) {
            my $len = unpack 'l<', substr(${ $sf->{data} }, $o->{offset} + ($cid == 114 ? 28 : 0), 4);
            $name = substr(${ $sf->{data} }, $o->{offset} + ($cid == 114 ? 32 : 4), $len) if $len > 0 && $len < 200;
        }
        next if defined $filter && $name !~ /$filter/;
        printf "  %20s class %4d size %8d %s\n", $o->{pathid}, $cid, $o->{size}, $name;
    }
}
