#!/usr/bin/env perl
# Condenses a Nocturne Access probe log into a readable transcript.
#
# Usage: perl tools/probe-transcript.pl [log] > transcript.txt
#
# - [Probe:TMP] lines: TMP rich-text tags are stripped (<alpha=#00>, <material=...>, <color=...>),
#   the invisible empty-line placeholder "<alpha=#00>あああああ</alpha>" is dropped, and a line is
#   printed only when a component's stripped text changes. The typewriter only moves alpha tags,
#   so a revealed dialogue line prints once.
# - [Probe:Mes], [Scene], [SPEECH], [Probe], muted lines: printed as they are.
# - [UiDump]: printed stripped, between begin/end markers.
use strict;
use warnings;
use utf8;
binmode STDOUT, ':encoding(UTF-8)';

my $log = shift // 'E:/SteamLibrary/steamapps/common/smt3hd/MelonLoader/Latest.log';
open my $fh, '<:encoding(UTF-8)', $log or die "Cannot open $log: $!";

my %last;    # path => last stripped text

sub strip {
    my ($s) = @_;
    $s =~ s/<[^>]*>//g;
    $s =~ s/\s+/ /g;
    $s =~ s/^\s+|\s+$//g;
    return $s;
}

sub short_path {
    my ($p) = @_;
    $p =~ s{^Canvas_UI/}{};
    return $p;
}

while (my $line = <$fh>) {
    $line =~ s/^\x{FEFF}//;
    chomp $line;
    $line =~ s/\r$//;
    my ($time) = $line =~ /^\[(\d\d:\d\d:\d\d)\.\d+\]/ or next;

    if ($line =~ /\[Probe:TMP\] (\S+) \[(\w+)\]( \(hidden\))?( \(\d+ writes\))? = "(.*)"$/) {
        my ($path, $hidden, $raw) = ($1, $3 // '', $5);
        next if $raw =~ /^<alpha=#00>あああああ<\/alpha>$/;
        my $text = strip($raw);
        next if defined $last{$path} && $last{$path} eq $text;
        $last{$path} = $text;
        printf "%s TMP  %s%s = \"%s\"\n", $time, short_path($path), $hidden, $text;
    }
    elsif ($line =~ /\[UiDump\] (.*)$/) {
        my $rest = $1;
        if ($rest =~ /^(\S+) \[(\w+)\] = "(.*)"$/) {
            my ($path, $raw) = ($1, $3);
            next if $raw =~ /^<alpha=#00>あああああ<\/alpha>$/;
            my $text = strip($raw);
            next if $text eq '';
            printf "%s DUMP %s = \"%s\"\n", $time, short_path($path), $text;
        }
        else {
            printf "%s DUMP %s\n", $time, $rest;
        }
    }
    elsif ($line =~ /\[(Probe:Mes|Scene|SPEECH|Probe)\] (.*)$/) {
        printf "%s %-4s %s\n", $time, ($1 eq 'Probe:Mes' ? 'MES' : $1 eq 'SPEECH' ? 'SAY' : $1), $2;
    }
    elsif ($line =~ /\[Probe:TMP\] (muted .*)$/) {
        printf "%s TMP  %s\n", $time, $1;
    }
}
