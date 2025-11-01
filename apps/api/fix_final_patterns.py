import os
import re
import glob

# Directories to process
test_dir = r"D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests"

# Process all .cs files recursively
for filepath in glob.glob(os.path.join(test_dir, "**", "*.cs"), recursive=True):
    with open(filepath, 'r', encoding='utf-8') as f:
        lines = f.readlines()

    changed = False
    new_lines = []

    for i, line in enumerate(lines):
        new_line = line

        # Fix GetInt64() > 0.Should().BeTrue() pattern
        new_line = re.sub(
            r'([\w\[\]\.]+\.GetInt64\(\)) > 0\.Should\(\)\.BeTrue\(\)',
            r'\1.Should().BeGreaterThan(0)',
            new_line
        )

        # Fix GetInt64() >= 0.Should().BeTrue() pattern
        new_line = re.sub(
            r'([\w\[\]\.]+\.GetInt64\(\)) >= 0\.Should\(\)\.BeTrue\(\)',
            r'\1.Should().BeGreaterThanOrEqualTo(0)',
            new_line
        )

        # Fix GetInt32() >= X.Should().BeTrue() pattern
        new_line = re.sub(
            r'([\w\[\]\.]+\.GetInt32\(\)) >= (\d+)\.Should\(\)\.BeTrue\(\)',
            r'\1.Should().BeGreaterThanOrEqualTo(\2)',
            new_line
        )

        # Fix GetArrayLength() >= X.Should().BeTrue() pattern
        new_line = re.sub(
            r'([\w\[\]\.]+\.GetArrayLength\(\)) >= (\d+)\.Should\(\)\.BeTrue\(\)',
            r'\1.Should().BeGreaterThanOrEqualTo(\2)',
            new_line
        )

        # Fix .BeEquivalentTo() on int/numeric assertions
        new_line = re.sub(
            r'([\w\[\]\.]+\.GetInt32\(\)|[\w\[\]\.]+Count)\.Should\(\)\.BeEquivalentTo\((\d+)\)',
            r'\1.Should().Be(\2)',
            new_line
        )

        # Fix InnerException access pattern
        if 'exception.InnerException' in new_line and 'Should().Throw' in lines[i-1] if i > 0 else False:
            new_line = new_line.replace('exception.InnerException', 'exception.Which.InnerException')

        # Fix ContainSingle pattern with .Message access
        if '.Should().ContainSingle();' in line and i+1 < len(lines) and '.Message' in lines[i+1]:
            new_line = line.replace('.Should().ContainSingle();', '.Should().ContainSingle().Which;')

        if new_line != line:
            changed = True

        new_lines.append(new_line)

    # Write back if changed
    if changed:
        with open(filepath, 'w', encoding='utf-8') as f:
            f.writelines(new_lines)
        print(f"Fixed: {os.path.basename(filepath)}")

print("Processing complete!")