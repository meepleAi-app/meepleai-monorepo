#!/usr/bin/env python3
"""
FluentAssertions Migration Script for PasswordResetServiceTests.cs
Converts xUnit Assert.* patterns to FluentAssertions syntax

Phase 11 Target: 58 assertions in PasswordResetServiceTests.cs
Distribution: True(12), Equal(12), False(11), ThrowsAsync(8), Contains(8), DoesNotContain(3), NotEqual(2), All(2), StartsWith(1), NotNull(1)
Expected automation: 75-80%
"""

import re
import sys

def convert_assert_true(content: str) -> tuple[str, int]:
    """Convert Assert.True(expr) to expr.Should().BeTrue()"""
    pattern = r'Assert\.True\(([^)]+)\);'
    count = len(re.findall(pattern, content))
    content = re.sub(pattern, r'\1.Should().BeTrue();', content)
    return content, count

def convert_assert_equal(content: str) -> tuple[str, int]:
    """Convert Assert.Equal(expected, actual) to actual.Should().Be(expected)"""
    pattern = r'Assert\.Equal\(([^,]+),\s*([^)]+)\);'
    count = len(re.findall(pattern, content))
    content = re.sub(pattern, r'\2.Should().Be(\1);', content)
    return content, count

def convert_assert_false(content: str) -> tuple[str, int]:
    """Convert Assert.False(expr) to expr.Should().BeFalse()"""
    pattern = r'Assert\.False\(([^)]+)\);'
    count = len(re.findall(pattern, content))
    content = re.sub(pattern, r'\1.Should().BeFalse();', content)
    return content, count

def convert_assert_contains(content: str) -> tuple[str, int]:
    """Convert Assert.Contains(needle, haystack) to haystack.Should().Contain(needle)"""
    pattern = r'Assert\.Contains\(([^,]+),\s*([^)]+)\);'
    count = len(re.findall(pattern, content))
    content = re.sub(pattern, r'\2.Should().Contain(\1);', content)
    return content, count

def convert_assert_not_equal(content: str) -> tuple[str, int]:
    """Convert Assert.NotEqual(expected, actual) to actual.Should().NotBe(expected)"""
    pattern = r'Assert\.NotEqual\(([^,]+),\s*([^)]+)\);'
    count = len(re.findall(pattern, content))
    content = re.sub(pattern, r'\2.Should().NotBe(\1);', content)
    return content, count

def convert_assert_not_null(content: str) -> tuple[str, int]:
    """Convert Assert.NotNull(expr) to expr.Should().NotBeNull()"""
    pattern = r'Assert\.NotNull\(([^)]+)\);'
    count = len(re.findall(pattern, content))
    content = re.sub(pattern, r'\1.Should().NotBeNull();', content)
    return content, count

def main():
    file_path = "tests/Api.Tests/PasswordResetServiceTests.cs"

    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    original_content = content
    total_conversions = 0

    # Apply conversions in order
    conversions = [
        ("Assert.True", convert_assert_true),
        ("Assert.Equal", convert_assert_equal),
        ("Assert.False", convert_assert_false),
        ("Assert.Contains", convert_assert_contains),
        ("Assert.NotEqual", convert_assert_not_equal),
        ("Assert.NotNull", convert_assert_not_null),
    ]

    for name, converter in conversions:
        content, count = converter(content)
        if count > 0:
            print(f"[OK] Converted {count} {name} assertions")
            total_conversions += count

    if content != original_content:
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(content)
        print(f"\n[SUCCESS] Successfully migrated {total_conversions} assertions in PasswordResetServiceTests.cs")
        return 0
    else:
        print("[WARN] No conversions needed")
        return 1

if __name__ == "__main__":
    sys.exit(main())
