#!/usr/bin/env python3
"""
FluentAssertions Migration Script for ConfigurationServiceTests.cs
Converts xUnit Assert.* patterns to FluentAssertions syntax

Phase 4 Target: 83 assertions in ConfigurationServiceTests.cs
Distribution: Equal(53), Contains(9), True(8), False(6), Throws(6)
"""

import re
import sys

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

def convert_assert_contains(content: str) -> tuple[str, int]:
    """Convert Assert.Contains(needle, haystack) to haystack.Should().Contain(needle)"""
    pattern = r'Assert\.Contains\(([^,]+),\s*([^)]+)\);'
    count = len(re.findall(pattern, content))
    content = re.sub(pattern, r'\2.Should().Contain(\1);', content)
    return content, count

def convert_assert_throws(content: str) -> tuple[str, int]:
    """
    Convert Assert.ThrowsAsync<T>(() => ...) to await FluentActions patterns
    Note: This is complex and may need manual review
    """
    # Pattern: var exception = await Assert.ThrowsAsync<Type>(() => method());
    pattern1 = r'var\s+(\w+)\s*=\s*await\s+Assert\.ThrowsAsync<([^>]+)>\(\s*\(\)\s*=>\s*([^)]+)\);'
    matches = re.findall(pattern1, content)
    count = len(matches)

    for match in matches:
        var_name, exception_type, method_call = match
        old = f"var {var_name} = await Assert.ThrowsAsync<{exception_type}>(() => {method_call});"
        new = f"var act = async () => await {method_call};\n        var {var_name} = await act.Should().ThrowAsync<{exception_type}>();"
        content = content.replace(old, new)

    return content, count

def main():
    file_path = "tests/Api.Tests/ConfigurationServiceTests.cs"

    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    original_content = content
    total_conversions = 0

    # Apply conversions in order (most common first for efficiency)
    conversions = [
        ("Assert.Equal", convert_assert_equal),
        ("Assert.Contains", convert_assert_contains),
        ("Assert.True", convert_assert_true),
        ("Assert.False", convert_assert_false),
        ("Assert.NotEqual", convert_assert_not_equal),
        ("Assert.ThrowsAsync", convert_assert_throws),
    ]

    for name, converter in conversions:
        content, count = converter(content)
        if count > 0:
            print(f"[OK] Converted {count} {name} assertions")
            total_conversions += count

    if content != original_content:
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(content)
        print(f"\n[SUCCESS] Successfully migrated {total_conversions} assertions in ConfigurationServiceTests.cs")
        return 0
    else:
        print("[WARN] No conversions needed")
        return 1

if __name__ == "__main__":
    sys.exit(main())
