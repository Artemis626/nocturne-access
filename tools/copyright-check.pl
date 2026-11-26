#!/usr/bin/perl
# Copyright check (26 Nov 2026, docs/PUBLIC-RELEASE-PLAN.md): finds game text quoted in the repository.
# Example: perl tools/copyright-check.pl 6 $(find research/gamedata -name "*_en.txt") research/gamedata/common/all_*.txt -- $(git ls-files)
# History: git log --all -p --format= | grep "^+" > added.txt, then pass added.txt as the target.
# Usage: match.pl N corpusfiles... -- targetfiles...
# Reports runs of >= N consecutive words (normalised) shared with the game text corpus,
# and whole game lines of >= 3 words quoted in the targets.
use strict; use warnings;
my $N = shift @ARGV;
my (@corpus, @targets); my $sep = 0;
for (@ARGV) { if ($_ eq '--') { $sep = 1; next } $sep ? push(@targets, $_) : push(@corpus, $_) }
sub words { my $s = lc shift; $s =~ s/<[^>]*>//g; $s =~ s/[^a-z0-9']+/ /g; $s =~ s/^ +| +$//g; return split / /, $s }
my %sh; my %line;
for my $f (@corpus) {
  open my $h, '<', $f or next;
  while (<$h>) { next unless /=\s*(.+?)\s*$/; my $v = $1; next if $v eq 'NULL';
    my @w = words($v); next unless @w;
    $line{join ' ', @w} = 1 if @w >= 4;
    for my $i (0 .. $#w - $N + 1) { $sh{join ' ', @w[$i .. $i + $N - 1]} = 1 } }
}
printf STDERR "corpus: %d shingles, %d lines\n", scalar keys %sh, scalar keys %line;
for my $f (@targets) {
  open my $h, '<', $f or next; my $ln = 0;
  while (my $t = <$h>) { $ln++; my @w = words($t); my ($best, $bs) = (0, ''); my $run = 0; my $start = 0;
    for my $i (0 .. $#w - $N + 1) { if ($sh{join ' ', @w[$i .. $i + $N - 1]}) { $start = $i unless $run; $run++;
        my $len = $run + $N - 1; if ($len > $best) { $best = $len; $bs = join ' ', @w[$start .. $start + $len - 1] } } else { $run = 0 } }
    print "$f:$ln\t$best\t$bs\n" if $best;
  }
}
