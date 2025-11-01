import os
import re
import glob

# Directories to process
test_dir = r"D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests"

# Process all .cs files recursively
for filepath in glob.glob(os.path.join(test_dir, "**", "*.cs"), recursive=True):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    original_content = content

    # Fix NumericAssertions<int>.BeTrue() pattern - should use comparison methods
    content = re.sub(
        r'([\w\[\]\.]+\.GetInt32\(\)|[\w\[\]\.]+\.GetArrayLength\(\)|[\w\[\]\.]+) >= (\d+)\.Should\(\)\.BeTrue\(\)',
        r'\1.Should().BeGreaterThanOrEqualTo(\2)',
        content
    )
    content = re.sub(
        r'([\w\[\]\.]+) > (\d+)\.Should\(\)\.BeTrue\(\)',
        r'\1.Should().BeGreaterThan(\2)',
        content
    )

    # Fix .BeEquivalentTo() on numeric assertions
    content = re.sub(
        r'([\w\[\]\.]+)\.Should\(\)\.BeEquivalentTo\((\d+)\)',
        r'\1.Should().Be(\2)',
        content
    )

    # Fix collection .Be() patterns - should use BeEquivalentTo()
    # For string collections
    content = re.sub(
        r'([\w\[\]\.]+)\.Should\(\)\.Contain\(([^)]+)\)\.Should\(\)\.Be\(([^)]+)\)',
        r'\1.Should().Contain(\2).And.BeEquivalentTo(\3)',
        content
    )

    # Fix AndWhichConstraint access to .Message property
    content = re.sub(
        r'var (\w+) = ([\w\.]+)\.Should\(\)\.ContainSingle\(\);[\s\n\r]*(\w+)\.Message',
        r'var \1 = \2.Should().ContainSingle().Which;\n        \1.Message',
        content
    )

    # Fix InnerException access on ExceptionAssertions
    content = re.sub(
        r'(act\.Should\(\)\.Throw[^;]+);[\s\n\r]*exception\.InnerException',
        r'\1;\n        exception.Which.InnerException',
        content
    )

    # Fix ObjectAssertions.BeFalse() - convert to BooleanAssertions
    content = re.sub(
        r'\.GetProperty\("(\w+)"\)\.GetBoolean\(\)\.Should\(\)\.BeFalse\(\)',
        r'.GetProperty("\1").GetBoolean().Should().BeFalse()',
        content
    )

    # Write back if changed
    if content != original_content:
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(content)
        print(f"Fixed: {os.path.basename(filepath)}")

print("Processing complete!")