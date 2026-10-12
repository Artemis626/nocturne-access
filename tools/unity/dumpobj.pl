#!/usr/bin/perl
# Prints objects of a game asset bundle through their type trees (MonoBehaviour tables such as GUIDE_EN, FldHitData).
# Usage: perl tools/unity/dumpobj.pl <bundle> <name regex> [max depth]
use strict; use warnings;
use FindBin; use lib $FindBin::Bin;
use UnityBundle;
my ($file, $re, $maxdepth) = @ARGV;
$maxdepth //= 12;
my $b = UnityBundle::open_bundle($file);
for my $node (@{ $b->{nodes} }) {
    next if $node->{path} =~ /\.res[S]?$/;
    my $sf = eval { UnityBundle::parse_serialized($node->{data}) } or next;
    for my $o (@{ $sf->{objects} }) {
        next unless $o->{type}{tree};
        my $v = eval { UnityBundle::read_tree($sf, $o) };
        if (!defined $v) { next }
        my $name = ref $v eq 'HASH' ? ($v->{m_Name} // '') : '';
        next unless $name =~ /$re/;
        print "== $name (class $o->{type}{classid}, path id $o->{pathid})\n";
        show($v, 0, '');
    }
}

sub show {
    my ($v, $depth, $key) = @_;
    my $ind = '  ' x $depth;
    if (ref $v eq 'HASH') {
        if ($depth >= $maxdepth) { print "$ind$key {...}\n"; return }
        print "$ind$key\n" if length $key;
        for my $k (grep { !/^m_(GameObject|Enabled|Script|ObjectHideFlags|EditorHideFlags|EditorClassIdentifier)$/ } sort keys %$v) { show($v->{$k}, $depth + 1, $k) }
    }
    elsif (ref $v eq 'ARRAY') {
        print "$ind$key [" . scalar(@$v) . "]\n";
        my $i = 0;
        for my $e (@$v) { show($e, $depth + 1, "[" . $i++ . "]") }
    }
    else {
        my $s = defined $v ? $v : 'undef';
        if ($s =~ /[^\x09\x0a\x0d\x20-\x7e\x80-\xff]/ && length $s > 8) { $s = sprintf '<%d bytes>', length $s }
        print "$ind$key = $s\n";
    }
}
