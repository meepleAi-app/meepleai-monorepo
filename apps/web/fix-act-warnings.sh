#!/bin/bash
# Script to wrap render() calls with act() in ProcessingProgress.test.tsx

FILE="src/components/__tests__/ProcessingProgress.test.tsx"

# Backup the file
cp "$FILE" "$FILE.bak"

# Use sed to wrap render calls that aren't already in act()
# Match: "      render(<ProcessingProgress" (6 spaces indent)
# Replace with: "      await act(async () => {\n        render(<ProcessingProgress" and close later
# This is complex, so we'll use perl for better multiline handling

perl -i -pe '
  # Match standalone render calls (not already wrapped in act)
  if (/^(\s+)(render\(<ProcessingProgress.*?\/>.*?\);)\s*$/ && $prev_line !~ /act\(async \(\) => \{/) {
    $indent = $1;
    $render_call = $2;
    $_ = "${indent}await act(async () => {\n${indent}  ${render_call}\n${indent}});\n";
  }
  $prev_line = $_;
' "$FILE"

echo "Fixed render calls in $FILE"
echo "Original backed up to $FILE.bak"
