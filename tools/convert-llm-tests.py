#!/usr/bin/env python3
"""
FluentAssertions Migration Script for LlmServiceTests.cs
Converts xUnit Assert.* patterns to FluentAssertions syntax

Phase 3 Target: 68 assertions in LlmServiceTests.cs
"""

import re
import sys

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

def convert_assert_equal(content: str) -> tuple[str, int]:
    """Convert Assert.Equal(expected, actual) to actual.Should().Be(expected)"""
    pattern = r'Assert\.Equal\(([^,]+),\s*([^)]+)\);'
    count = len(re.findall(pattern, content))
    content = re.sub(pattern, r'\2.Should().Be(\1);', content)
    return content, count

def convert_assert_not_equal(content: str) -> tuple[str, int]:
    """Convert Assert.NotEqual(expected, actual) to actual.Should().NotBe(expected)"""
    pattern = r'Assert\.NotEqual\(([^,]+),\s*([^)]+)\);'
    count = len(re.findall(pattern, content))
    content = re.sub(pattern, r'\2.Should().NotBe(\1);', content)
    return content, count

def convert_assert_null(content: str) -> tuple[str, int]:
    """Convert Assert.Null(expr) to expr.Should().BeNull()"""
    pattern = r'Assert\.Null\(([^)]+)\);'
    count = len(re.findall(pattern, content))
    content = re.sub(pattern, r'\1.Should().BeNull();', content)
    return content, count

def convert_assert_single(content: str) -> tuple[str, int]:
    """Convert Assert.Single(collection) to collection.Should().ContainSingle()"""
    # First pass: simple Assert.Single(expr)
    pattern1 = r'var\s+(\w+)\s*=\s*Assert\.Single\(([^)]+)\);'
    matches1 = re.findall(pattern1, content)
    count1 = len(matches1)
    content = re.sub(pattern1, r'var \1 = \2.Should().ContainSingle().Subject;', content)

    # Second pass: standalone Assert.Single(expr)
    pattern2 = r'Assert\.Single\(([^)]+)\);'
    count2 = len(re.findall(pattern2, content))
    content = re.sub(pattern2, r'\1.Should().ContainSingle();', content)

    return content, count1 + count2

def convert_assert_in_range(content: str) -> tuple[str, int]:
    """Convert Assert.InRange(value, low, high) to value.Should().BeInRange(low, high)"""
    pattern = r'Assert\.InRange\(([^,]+),\s*([^,]+),\s*([^)]+)\);'
    count = len(re.findall(pattern, content))
    content = re.sub(pattern, r'\1.Should().BeInRange(\2, \3);', content)
    return content, count

def convert_assert_all(content: str) -> tuple[str, int]:
    """Convert Assert.All(collection, pred) to collection.Should().OnlyContain(pred)"""
    pattern = r'Assert\.All\(([^,]+),\s*model\s*=>\s*Assert\.Equal\(([^,]+),\s*model\)\);'
    count = len(re.findall(pattern, content))
    content = re.sub(pattern, r'\1.Should().OnlyContain(model => model == \2);', content)
    return content, count

def main():
    file_path = "tests/Api.Tests/LlmServiceTests.cs"

    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    original_content = content
    total_conversions = 0

    # Apply conversions in order
    conversions = [
        ("Assert.True", convert_assert_true),
        ("Assert.False", convert_assert_false),
        ("Assert.Equal", convert_assert_equal),
        ("Assert.NotEqual", convert_assert_not_equal),
        ("Assert.Null", convert_assert_null),
        ("Assert.Single", convert_assert_single),
        ("Assert.InRange", convert_assert_in_range),
        ("Assert.All", convert_assert_all),
    ]

    for name, converter in conversions:
        content, count = converter(content)
        if count > 0:
            print(f"[OK] Converted {count} {name} assertions")
            total_conversions += count

    if content != original_content:
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(content)
        print(f"\n[SUCCESS] Successfully migrated {total_conversions} assertions in LlmServiceTests.cs")
        return 0
    else:
        print("[WARN] No conversions needed")
        return 1

if __name__ == "__main__":
    sys.exit(main())
