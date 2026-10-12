package UnityBundle;
# Read-only reader for the game's Unity asset bundles (smt3hd_Data/StreamingAssets/PC; UnityFS version 6, LZ4 / LZ4HC
# blocks, Unity 2019.4) and the SerializedFile (version 21 or so) inside them, enough to list objects and read TextAsset
# and MonoBehaviour bytes. Offline only: game data files, never the game's executable.
use strict; use warnings;

sub lz4_block {
    my ($src, $outlen) = @_;
    my $out = '';
    my ($i, $n) = (0, length $src);
    while ($i < $n) {
        my $token = ord substr($src, $i++, 1);
        my $lit = $token >> 4;
        if ($lit == 15) { my $b; do { $b = ord substr($src, $i++, 1); $lit += $b } while ($b == 255); }
        $out .= substr($src, $i, $lit); $i += $lit;
        last if $i >= $n;
        my $off = unpack 'v', substr($src, $i, 2); $i += 2;
        my $ml = $token & 15;
        if ($ml == 15) { my $b; do { $b = ord substr($src, $i++, 1); $ml += $b } while ($b == 255); }
        $ml += 4;
        my $start = length($out) - $off;
        die "lz4: bad offset" if $start < 0 || $off == 0;
        if ($off >= $ml) { $out .= substr($out, $start, $ml) }
        else { for (1 .. $ml) { $out .= substr($out, $start++, 1) } }
    }
    die "lz4: got " . length($out) . " of $outlen" if defined $outlen && length($out) != $outlen;
    return $out;
}

sub cstr { my ($buf, $pos) = @_; my $e = index($$buf, "\0", $$pos); my $s = substr($$buf, $$pos, $e - $$pos); $$pos = $e + 1; return $s; }

sub decompress {
    my ($flags, $data, $ulen) = @_;
    my $c = $flags & 0x3F;
    return $data if $c == 0;
    return lz4_block($data, $ulen) if $c == 2 || $c == 3;
    die "compression $c not supported";
}

# Returns { nodes => [ { path, data } ] } for a bundle file.
sub open_bundle {
    my ($file) = @_;
    open my $fh, '<:raw', $file or die "$file: $!";
    local $/; my $buf = <$fh>; close $fh;
    my $pos = 0;
    my $sig = cstr(\$buf, \$pos);
    die "$file: not UnityFS ($sig)" unless $sig eq 'UnityFS';
    my $ver = unpack 'N', substr($buf, $pos, 4); $pos += 4;
    cstr(\$buf, \$pos); cstr(\$buf, \$pos);
    my ($hi, $lo) = unpack 'NN', substr($buf, $pos, 8); $pos += 8;
    my ($csize, $usize, $flags) = unpack 'NNN', substr($buf, $pos, 12); $pos += 12;
    $pos = ($pos + 15) & ~15 if $ver >= 7;
    my $info;
    if ($flags & 0x80) { $info = substr($buf, length($buf) - $csize, $csize) }
    else { $info = substr($buf, $pos, $csize); $pos += $csize }
    $info = decompress($flags, $info, $usize);
    my $ip = 16;
    my $nblocks = unpack 'N', substr($info, $ip, 4); $ip += 4;
    my @blocks;
    for (1 .. $nblocks) { my ($u, $c, $f) = unpack 'NNn', substr($info, $ip, 10); $ip += 10; push @blocks, [$u, $c, $f]; }
    my $nnodes = unpack 'N', substr($info, $ip, 4); $ip += 4;
    my @nodes;
    for (1 .. $nnodes) {
        my ($oh, $ol, $sh, $sl, $nf) = unpack 'NNNNN', substr($info, $ip, 20); $ip += 20;
        my $path = cstr(\$info, \$ip);
        push @nodes, { offset => $oh * 4294967296 + $ol, size => $sh * 4294967296 + $sl, path => $path };
    }
    my $data = '';
    for my $b (@blocks) { $data .= decompress($b->[2], substr($buf, $pos, $b->[1]), $b->[0]); $pos += $b->[1]; }
    $_->{data} = substr($data, $_->{offset}, $_->{size}) for @nodes;
    return { nodes => \@nodes };
}

# SerializedFile: header (big-endian), then metadata (little-endian when the endian byte is 0).
sub parse_serialized {
    my ($d) = @_;
    my ($msize, $fsize, $ver, $doff) = unpack 'NNNN', substr($d, 0, 16);
    my $pos = 16;
    my $endian = ord substr($d, $pos, 1); $pos += 4;
    if ($ver >= 22) { die "SerializedFile $ver not supported" }
    my $le = $endian == 0;
    my $u32 = sub { my $v = unpack($le ? 'V' : 'N', substr($d, $pos, 4)); $pos += 4; $v };
    my $i32 = sub { my $v = unpack($le ? 'l<' : 'l>', substr($d, $pos, 4)); $pos += 4; $v };
    my $i64 = sub { my $v = unpack($le ? 'q<' : 'q>', substr($d, $pos, 8)); $pos += 8; $v };
    my $unity = cstr(\$d, \$pos);
    my $platform = $u32->();
    my $hasTree = ord substr($d, $pos, 1); $pos += 1;
    my $ntypes = $u32->();
    my @types;
    for (1 .. $ntypes) {
        my $cid = $i32->();
        my $stripped = ord substr($d, $pos, 1); $pos += 1;
        my $script = unpack($le ? 's<' : 's>', substr($d, $pos, 2)); $pos += 2;
        $pos += 16 if $cid == 114 || $script >= 0;   # script id hash
        $pos += 16;                                    # old type hash
        my $tree;
        if ($hasTree) {
            my $nn = $u32->(); my $sb = $u32->();
            my @n;
            for (1 .. $nn) {
                my ($v, $lvl, $arr) = unpack 'vCC', substr($d, $pos, 4); $pos += 4;
                my $toff = $u32->(); my $noff = $u32->(); my $size = $i32->(); my $idx = $u32->(); my $fl = $u32->();
                $pos += 8 if $ver >= 19;   # ref type hash
                push @n, { level => $lvl, array => $arr, typeoff => $toff, nameoff => $noff, size => $size, flags => $fl };
            }
            my $strs = substr($d, $pos, $sb); $pos += $sb;
            for my $x (@n) {
                $x->{type} = strof($strs, $x->{typeoff});
                $x->{name} = strof($strs, $x->{nameoff});
            }
            $pos += 4 * $u32->() if $ver >= 21;   # type dependencies
            $tree = \@n;
        }
        push @types, { classid => $cid, script => $script, tree => $tree };
    }
    my $nobj = $u32->();
    my @objs;
    for (1 .. $nobj) {
        $pos = ($pos + 3) & ~3;
        my $pid = $i64->(); my $off = $u32->(); my $size = $u32->(); my $tidx = $u32->();
        push @objs, { pathid => $pid, offset => $doff + $off, size => $size, type => $types[$tidx] };
    }
    return { version => $ver, types => \@types, objects => \@objs, data => \$d, le => $le };
}

my %common;
sub strof {
    my ($strs, $off) = @_;
    if ($off & 0x80000000) { return common_string($off & 0x7FFFFFFF) }
    my $e = index($strs, "\0", $off);
    return substr($strs, $off, $e - $off);
}

# Unity's common string table (offsets into it), the names used by type trees.
my $COMMON = "AABB\0AnimationClip\0AnimationCurve\0AnimationState\0Array\0Base\0BitField\0bitset\0bool\0char\0ColorRGBA\0Component\0data\0deque\0double\0dynamic_array\0FastPropertyName\0first\0float\0Font\0GameObject\0Generic Mono\0GradientNEW\0GUID\0GUIStyle\0int\0list\0long long\0map\0Matrix4x4f\0MdFour\0MonoBehaviour\0MonoScript\0m_ByteSize\0m_Curve\0m_EditorClassIdentifier\0m_EditorHideFlags\0m_Enabled\0m_ExtensionPtr\0m_GameObject\0m_Index\0m_IsArray\0m_IsStatic\0m_MetaFlag\0m_Name\0m_ObjectHideFlags\0m_PrefabInternal\0m_PrefabParentObject\0m_Script\0m_StaticEditorFlags\0m_Type\0m_Version\0Object\0pair\0PPtr<Component>\0PPtr<GameObject>\0PPtr<Material>\0PPtr<MonoBehaviour>\0PPtr<MonoScript>\0PPtr<Object>\0PPtr<Prefab>\0PPtr<Sprite>\0PPtr<TextAsset>\0PPtr<Texture>\0PPtr<Texture2D>\0PPtr<Transform>\0Prefab\0Quaternionf\0Rectf\0RectInt\0RectOffset\0second\0set\0short\0size\0SInt16\0SInt32\0SInt64\0SInt8\0staticvector\0string\0TextAsset\0TextMesh\0Texture\0Texture2D\0Transform\0TypelessData\0UInt16\0UInt32\0UInt64\0UInt8\0unsigned int\0unsigned long long\0unsigned short\0vector\0Vector2f\0Vector3f\0Vector4f\0m_ScriptingClassIdentifier\0Gradient\0Type*\0int2_storage\0int3_storage\0BoundsInt\0m_CorrespondingSourceObject\0m_PrefabInstance\0m_PrefabAsset\0FileSize\0Hash128\0";
sub common_string { my ($off) = @_; my $e = index($COMMON, "\0", $off); return $e < 0 ? "?$off" : substr($COMMON, $off, $e - $off); }

# Reads an object by its type tree into a Perl structure (hashes, arrays, scalars; byte arrays as strings).
sub read_tree {
    my ($sf, $obj) = @_;
    my $tree = $obj->{type}{tree} or return undef;
    my $d = $sf->{data};
    my $pos = $obj->{offset};
    my $i = 0;
    my $v = read_node($tree, \$i, $d, \$pos, $sf->{le});
    return $v;
}

sub read_node {
    my ($t, $ip, $d, $pos, $le) = @_;
    my $n = $t->[$$ip];
    my $lvl = $n->{level};
    my @kids;
    my $j = $$ip + 1;
    while ($j < @$t && $t->[$j]{level} > $lvl) { push @kids, $j if $t->[$j]{level} == $lvl + 1; $j++; }
    my $end = $j;
    my $val;
    my $type = $n->{type};
    if ($n->{array} || ($type eq 'vector' || $type eq 'staticvector' || $type eq 'set')) {
        my $arr = $n->{array} ? $$ip : $kids[0];
        my $an = $t->[$arr];
        my @ak; my $k = $arr + 1;
        while ($k < @$t && $t->[$k]{level} > $an->{level}) { push @ak, $k if $t->[$k]{level} == $an->{level} + 1; $k++; }
        my $count = unpack($le ? 'l<' : 'l>', substr($$d, $$pos, 4)); $$pos += 4;
        my $elem = $ak[1];
        my $et = $t->[$elem];
        if ($et->{type} =~ /^(UInt8|SInt8|char)$/ && $et->{size} == 1) { $val = substr($$d, $$pos, $count); $$pos += $count; }
        else {
            my @a;
            for (1 .. $count) { my $x = $elem; push @a, read_node($t, \$x, $d, $pos, $le); }
            $val = \@a;
        }
        $$pos = ($$pos + 3) & ~3 if ($an->{flags} & 0x4000) || ($n->{flags} & 0x4000);
    }
    elsif ($type eq 'string') {
        my $len = unpack($le ? 'l<' : 'l>', substr($$d, $$pos, 4)); $$pos += 4;
        $val = substr($$d, $$pos, $len); $$pos += $len;
        $$pos = ($$pos + 3) & ~3;
    }
    elsif (!@kids) {
        my %fmt = (SInt8 => 'c', UInt8 => 'C', char => 'C', bool => 'C', SInt16 => 's<', short => 's<', UInt16 => 'v', 'unsigned short' => 'v',
                   SInt32 => 'l<', int => 'l<', UInt32 => 'V', 'unsigned int' => 'V', Type => 'l<', float => 'f<', double => 'd<',
                   SInt64 => 'q<', 'long long' => 'q<', UInt64 => 'Q<', 'unsigned long long' => 'Q<', FileSize => 'Q<');
        my $f = $fmt{$type} // ($n->{size} == 4 ? 'l<' : $n->{size} == 1 ? 'C' : $n->{size} == 2 ? 's<' : 'q<');
        $val = unpack $f, substr($$d, $$pos, $n->{size}); $$pos += $n->{size};
    }
    else {
        my %h;
        for my $k (@kids) { my $x = $k; $h{$t->[$k]{name}} = read_node($t, \$x, $d, $pos, $le); }
        $val = \%h;
    }
    $$pos = ($$pos + 3) & ~3 if $n->{flags} & 0x4000;
    $$ip = $end;
    return $val;
}

1;
