#!/usr/bin/perl
use strict;
use warnings;

my $file = 'src/components/__tests__/ProcessingProgress.test.tsx';
open(my $fh, '<', $file) or die "Cannot open $file: $!";
my @lines = <$fh>;
close($fh);

my @output;
my $i = 0;
while ($i < @lines) {
    my $line = $lines[$i];
    
    # Check if this line is a standalone render call (not already in act)
    if ($line =~ /^(\s+)(render\(<ProcessingProgress.*\/>.*\);)\s*$/) {
        my $indent = $1;
        my $render_call = $2;
        
        # Check previous line to see if it's already in act
        my $prev = $i > 0 ? $lines[$i-1] : '';
        if ($prev !~ /act\(async \(\) => \{/) {
            # Wrap in act
            push @output, "${indent}await act(async () => {\n";
            push @output, "${indent}  $render_call\n";
            push @output, "${indent}});\n";
            $i++;
            next;
        }
    }
    
    # Check for destructured render (const { unmount } = render(...))
    if ($line =~ /^(\s+)(const \{.*?\} = render\(<ProcessingProgress.*\/>.*\);)\s*$/) {
        my $indent = $1;
        my $render_call = $2;
        
        # Check previous line
        my $prev = $i > 0 ? $lines[$i-1] : '';
        if ($prev !~ /act\(async \(\) => \{/) {
            # This needs special handling - already done in the file
            push @output, $line;
            $i++;
            next;
        }
    }
    
    push @output, $line;
    $i++;
}

open(my $out, '>', $file) or die "Cannot write $file: $!";
print $out @output;
close($out);

print "Fixed render calls in $file\n";
