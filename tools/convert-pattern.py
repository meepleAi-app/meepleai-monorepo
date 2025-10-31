#!/usr/bin/env python3
"""Convert one assertion pattern at a time across all files."""
import re
import sys
import subprocess
from pathlib import Path

def convert_assert_notnull(content):
    """Convert Assert.NotNull(x) → x.Should().NotBeNull()
    EXCEPT for DateTime nullable properties (ResetsAt, ExpiresAt, CreatedAt, etc.)"""

    # Skip patterns that are DateTime nullable (common property names)
    datetime_props = r'(ResetsAt|ExpiresAt|CreatedAt|UpdatedAt|RevokedAt|DeletedAt)'

    lines = content.split('\n')
    new_lines = []

    for line in lines:
        # Skip if it's a DateTime nullable property
        if re.search(rf'Assert\.NotNull\([^)]*\.{datetime_props}\)', line):
            new_lines.append(line)
            continue

        # Safe conversions for non-DateTime
        new_line = re.sub(
            r'Assert\.NotNull\(([a-zA-Z0-9_]+)\);',
            r'\1.Should().NotBeNull();',
            line
        )
        new_line = re.sub(
            r'Assert\.NotNull\(([a-zA-Z0-9_\.]+)\);',
            r'\1.Should().NotBeNull();',
            new_line
        )
        new_lines.append(new_line)

    return '\n'.join(new_lines)

def convert_assert_null(content):
    """Convert Assert.Null(x) → x.Should().BeNull()"""
    lines = content.split('\n')
    new_lines = []

    for line in lines:
        new_line = re.sub(
            r'Assert\.Null\(([a-zA-Z0-9_]+)\);',
            r'\1.Should().BeNull();',
            line
        )
        new_line = re.sub(
            r'Assert\.Null\(([a-zA-Z0-9_\.]+)\);',
            r'\1.Should().BeNull();',
            new_line
        )
        new_lines.append(new_line)

    return '\n'.join(new_lines)

def convert_assert_empty(content):
    """Convert Assert.Empty(x) → x.Should().BeEmpty()"""
    return re.sub(
        r'Assert\.Empty\(([^)]+)\);',
        r'\1.Should().BeEmpty();',
        content
    )

def convert_assert_notempty(content):
    """Convert Assert.NotEmpty(x) → x.Should().NotBeEmpty()"""
    return re.sub(
        r'Assert\.NotEmpty\(([^)]+)\);',
        r'\1.Should().NotBeEmpty();',
        content
    )

def convert_assert_single(content):
    """Convert Assert.Single(x) → x.Should().ContainSingle()"""
    return re.sub(
        r'Assert\.Single\(([^)]+)\);',
        r'\1.Should().ContainSingle();',
        content
    )

def main():
    pattern_name = sys.argv[1] if len(sys.argv) > 1 else "notnull"

    converters = {
        "notnull": ("Assert.NotNull", convert_assert_notnull),
        "null": ("Assert.Null", convert_assert_null),
        "empty": ("Assert.Empty", convert_assert_empty),
        "notempty": ("Assert.NotEmpty", convert_assert_notempty),
        "single": ("Assert.Single", convert_assert_single),
    }

    if pattern_name not in converters:
        print(f"Unknown pattern: {pattern_name}")
        print(f"Available: {list(converters.keys())}")
        sys.exit(1)

    pattern_desc, converter = converters[pattern_name]

    test_dir = Path("tests/Api.Tests")
    converted_files = 0
    total_conversions = 0

    for cs_file in test_dir.rglob("*.cs"):
        content = cs_file.read_text(encoding='utf-8')

        if pattern_desc not in content:
            continue

        new_content = converter(content)

        if new_content != content:
            conversions = content.count(pattern_desc) - new_content.count(pattern_desc)
            cs_file.write_text(new_content, encoding='utf-8')
            converted_files += 1
            total_conversions += conversions
            print(f"  {cs_file.name}: {conversions} conversions")

    print(f"\nConverted {total_conversions} assertions in {converted_files} files")

    # Verify build
    print("\nBuilding...")
    result = subprocess.run(
        ["dotnet", "build", "--no-restore"],
        capture_output=True,
        text=True,
        cwd="."
    )

    if result.returncode != 0:
        print("[X] Build FAILED!")
        errors = [line for line in result.stderr.split('\n') if 'error CS' in line]
        for err in errors[:10]:
            print(err)
        sys.exit(1)
    else:
        print("[OK] Build PASSED")

if __name__ == "__main__":
    main()
