import os
import re
import glob

# Directories to process
test_dir = r"D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests"

# Pattern replacements
patterns = [
    # Fix await act.Should().ThrowAsync<>().Subject pattern
    (r'var (\w+) = await act\.Should\(\)\.ThrowAsync<([^>]+)>\(\)\.Subject;',
     r'var \1 = await act.Should().ThrowAsync<\2>();'),

    # Fix act.Should().Throw<>().Subject.Subject pattern
    (r'var (\w+) = act\.Should\(\)\.Throw<([^>]+)>\(\)\.Subject\.Subject;',
     r'var \1 = act.Should().Throw<\2>().Which;'),

    # Fix single .Subject pattern for Throw
    (r'var (\w+) = act\.Should\(\)\.Throw<([^>]+)>\(\)\.Subject;',
     r'var \1 = act.Should().Throw<\2>();'),
]

# Process all .cs files recursively
for filepath in glob.glob(os.path.join(test_dir, "**", "*.cs"), recursive=True):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    original_content = content

    # Apply all patterns
    for pattern, replacement in patterns:
        content = re.sub(pattern, replacement, content)

    # Write back if changed
    if content != original_content:
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(content)
        print(f"Fixed: {os.path.basename(filepath)}")

print("Processing complete!")