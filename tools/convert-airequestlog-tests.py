#!/usr/bin/env python3
"""
FluentAssertions Migration Script for AiRequestLogServiceTests.cs
Converts xUnit Assert.* patterns to FluentAssertions syntax

Phase 12 Target: 58 assertions in AiRequestLogServiceTests.cs - 40% MILESTONE!
Distribution: Equal(50), Single(5), True(1), DoesNotContain(1), Contains(1)
Expected automation: 95%+ (simplest file yet!)
"""

import re
import sys

def convert_assert_equal(content: str) -> tuple[str, int]:
    """Convert Assert.Equal(expected, actual) to actual.Should().Be(expected)"""
    pattern = r'Assert\.Equal\(([^,]+),\s*([^)]+)\);'
    count = len(re.findall(pattern, content))
    content = re.sub(pattern, r'\2.Should().Be(\1);', content)
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

def convert_assert_true(content: str) -> tuple[str, int]:
    """Convert Assert.True(expr) to expr.Should().BeTrue()"""
    pattern = r'Assert\.True\(([^)]+)\);'
    count = len(re.findall(pattern, content))
    content = re.sub(pattern, r'\1.Should().BeTrue();', content)
    return content, count

def convert_assert_contains(content: str) -> tuple[str, int]:
    """Convert Assert.Contains(needle, haystack) to haystack.Should().Contain(needle)"""
    pattern = r'Assert\.Contains\(([^,]+),\s*([^)]+)\);'
    count = len(re.findall(pattern, content))
    content = re.sub(pattern, r'\2.Should().Contain(\1);', content)
    return content, count

def convert_assert_does_not_contain(content: str) -> tuple[str, int]:
    """Convert Assert.DoesNotContain(needle, haystack) to haystack.Should().NotContain(needle)"""
    pattern = r'Assert\.DoesNotContain\(([^,]+),\s*([^)]+)\);'
    count = len(re.findall(pattern, content))
    content = re.sub(pattern, r'\2.Should().NotContain(\1);', content)
    return content, count

def main():
    file_path = "tests/Api.Tests/AiRequestLogServiceTests.cs"

    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    original_content = content
    total_conversions = 0

    # Apply conversions in order (most common first)
    conversions = [
        ("Assert.Equal", convert_assert_equal),
        ("Assert.Single", convert_assert_single),
        ("Assert.True", convert_assert_true),
        ("Assert.Contains", convert_assert_contains),
        ("Assert.DoesNotContain", convert_assert_does_not_contain),
    ]

    for name, converter in conversions:
        content, count = converter(content)
        if count > 0:
            print(f"[OK] Converted {count} {name} assertions")
            total_conversions += count

    if content != original_content:
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(content)
        print(f"\n[SUCCESS] Successfully migrated {total_conversions} assertions")
        print(f"[MILESTONE] 40% completion achieved!")
        return 0
    else:
        print("[WARN] No conversions needed")
        return 1

if __name__ == "__main__":
    sys.exit(main())
