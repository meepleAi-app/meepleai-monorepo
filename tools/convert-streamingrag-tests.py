#!/usr/bin/env python3
"""
FluentAssertions Migration Script for StreamingRagServiceTests.cs
Converts xUnit Assert.* patterns to FluentAssertions syntax

Phase 8 Target: 70 assertions in StreamingRagServiceTests.cs
Distribution: Equal(45), True(5), Single(3), Contains(1), others(16)
Expected high automation (85%+)
"""

import re
import sys

def convert_assert_equal(content: str) -> tuple[str, int]:
    """Convert Assert.Equal(expected, actual) to actual.Should().Be(expected)"""
    pattern = r'Assert\.Equal\(([^,]+),\s*([^)]+)\);'
    count = len(re.findall(pattern, content))
    content = re.sub(pattern, r'\2.Should().Be(\1);', content)
    return content, count

def convert_assert_true(content: str) -> tuple[str, int]:
    """Convert Assert.True(expr) to expr.Should().BeTrue()"""
    pattern = r'Assert\.True\(([^)]+)\);'
    count = len(re.findall(pattern, content))
    content = re.sub(pattern, r'\1.Should().BeTrue();', content)
    return content, count

def convert_assert_false(content: str) -> tuple[str, int]:
    """Convert Assert.False(expr) to expr.Should().BeFalse()"""
    pattern = r'Assert\.False\(([^)]+)\);'
    count = len(re.findall(pattern, content))
    content = re.sub(pattern, r'\1.Should().BeFalse();', content)
    return content, count

def convert_assert_single(content: str) -> tuple[str, int]:
    """Convert Assert.Single(collection) to collection.Should().ContainSingle()"""
    # First pass: var x = Assert.Single(expr)
    pattern1 = r'var\s+(\w+)\s*=\s*Assert\.Single\(([^)]+)\);'
    matches1 = re.findall(pattern1, content)
    count1 = len(matches1)
    content = re.sub(pattern1, r'var \1 = \2.Should().ContainSingle().Subject;', content)

    # Second pass: standalone Assert.Single(expr)
    pattern2 = r'Assert\.Single\(([^)]+)\);'
    count2 = len(re.findall(pattern2, content))
    content = re.sub(pattern2, r'\1.Should().ContainSingle();', content)

    return content, count1 + count2

def convert_assert_contains(content: str) -> tuple[str, int]:
    """Convert Assert.Contains(needle, haystack) to haystack.Should().Contain(needle)"""
    pattern = r'Assert\.Contains\(([^,]+),\s*([^)]+)\);'
    count = len(re.findall(pattern, content))
    content = re.sub(pattern, r'\2.Should().Contain(\1);', content)
    return content, count

def convert_assert_not_empty(content: str) -> tuple[str, int]:
    """Convert Assert.NotEmpty(collection) to collection.Should().NotBeEmpty()"""
    pattern = r'Assert\.NotEmpty\(([^)]+)\);'
    count = len(re.findall(pattern, content))
    content = re.sub(pattern, r'\1.Should().NotBeEmpty();', content)
    return content, count

def convert_assert_empty(content: str) -> tuple[str, int]:
    """Convert Assert.Empty(collection) to collection.Should().BeEmpty()"""
    pattern = r'Assert\.Empty\(([^)]+)\);'
    count = len(re.findall(pattern, content))
    content = re.sub(pattern, r'\1.Should().BeEmpty();', content)
    return content, count

def main():
    file_path = "tests/Api.Tests/StreamingRagServiceTests.cs"

    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    original_content = content
    total_conversions = 0

    # Apply conversions in order (most common first)
    conversions = [
        ("Assert.Equal", convert_assert_equal),
        ("Assert.True", convert_assert_true),
        ("Assert.Single", convert_assert_single),
        ("Assert.Contains", convert_assert_contains),
        ("Assert.False", convert_assert_false),
        ("Assert.NotEmpty", convert_assert_not_empty),
        ("Assert.Empty", convert_assert_empty),
    ]

    for name, converter in conversions:
        content, count = converter(content)
        if count > 0:
            print(f"[OK] Converted {count} {name} assertions")
            total_conversions += count

    if content != original_content:
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(content)
        print(f"\n[SUCCESS] Successfully migrated {total_conversions} assertions in StreamingRagServiceTests.cs")
        return 0
    else:
        print("[WARN] No conversions needed")
        return 1

if __name__ == "__main__":
    sys.exit(main())
