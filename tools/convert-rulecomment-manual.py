#!/usr/bin/env python3
"""
Manual conversions for RuleCommentServiceTests.cs
Handles complex patterns that the automated script missed
"""

import re
import sys

def convert_throws_async_multiline(content: str) -> tuple[str, int]:
    """
    Convert multiline Assert.ThrowsAsync patterns to FluentActions
    Pattern:
        var ex = await Assert.ThrowsAsync<ExceptionType>(
            () => _service.Method(...));
    To:
        var act = async () => await _service.Method(...);
        var ex = await act.Should().ThrowAsync<ExceptionType>();
    """
    # Match multiline ThrowsAsync with variable assignment
    pattern = r'var\s+(\w+)\s*=\s*await\s+Assert\.ThrowsAsync<([^>]+)>\(\s*\(\)\s*=>\s*_service\.([^\)]+\([^\)]*\))\);'

    matches = re.findall(pattern, content, re.DOTALL)
    count = 0

    for match in matches:
        var_name, exception_type, method_call = match
        # Clean up whitespace in method_call
        method_call = ' '.join(method_call.split())

        old = f"var {var_name} = await Assert.ThrowsAsync<{exception_type}>(\n            () => _service.{method_call});"
        new = f"var act = async () => await _service.{method_call};\n        var {var_name} = await act.Should().ThrowAsync<{exception_type}>();"

        if old in content:
            content = content.replace(old, new)
            count += 1

    return content, count

def convert_does_not_contain(content: str) -> tuple[str, int]:
    """Convert Assert.DoesNotContain(collection, predicate) to collection.Should().NotContain(predicate)"""
    pattern = r'Assert\.DoesNotContain\(([^,]+),\s*([^)]+)\);'
    count = len(re.findall(pattern, content))
    content = re.sub(pattern, r'\1.Should().NotContain(\2);', content)
    return content, count

def convert_equal_with_first(content: str) -> tuple[str, int]:
    """Convert Assert.Equal(expected, actual.First().Property) to actual.First().Property.Should().Be(expected)"""
    pattern = r'Assert\.Equal\(([^,]+),\s*([^)]+\.First\(\)[^)]*)\);'
    count = len(re.findall(pattern, content))
    content = re.sub(pattern, r'\2.Should().Be(\1);', content)
    return content, count

def convert_single_with_first(content: str) -> tuple[str, int]:
    """Convert Assert.Single(collection.First().Property) to collection.First().Property.Should().ContainSingle()"""
    pattern = r'Assert\.Single\(([^)]+\.First\(\)[^)]*)\);'
    count = len(re.findall(pattern, content))
    content = re.sub(pattern, r'\1.Should().ContainSingle();', content)
    return content, count

def convert_false_with_first(content: str) -> tuple[str, int]:
    """Convert Assert.False(obj.First().Property) to obj.First().Property.Should().BeFalse()"""
    pattern = r'Assert\.False\(([^)]+\.First\(\)[^)]*)\);'
    count = len(re.findall(pattern, content))
    content = re.sub(pattern, r'\1.Should().BeFalse();', content)
    return content, count

def convert_all_nested(content: str) -> tuple[str, int]:
    """Convert Assert.All(items, item => Assert.X(...)) to items.Should().OnlyContain(...)"""
    # Pattern 1: Assert.All(x, y => Assert.Equal(val, y.Prop))
    pattern1 = r'Assert\.All\(([^,]+),\s*(\w+)\s*=>\s*Assert\.Equal\(([^,]+),\s*\2\.([^)]+)\)\);'
    matches1 = re.findall(pattern1, content)
    count = len(matches1)

    for match in matches1:
        collection, var_name, expected, property_access = match
        old = f"Assert.All({collection}, {var_name} => Assert.Equal({expected}, {var_name}.{property_access}));"
        new = f"{collection}.Should().OnlyContain({var_name} => {var_name}.{property_access} == {expected});"
        content = content.replace(old, new)

    # Pattern 2: Assert.All(x, y => Assert.True(condition))
    pattern2 = r'Assert\.All\(([^,]+),\s*(\w+)\s*=>\s*Assert\.True\((\2\.[^)]+)\)\);'
    matches2 = re.findall(pattern2, content)
    count += len(matches2)

    for match in matches2:
        collection, var_name, condition = match
        old = f"Assert.All({collection}, {var_name} => Assert.True({condition}));"
        new = f"{collection}.Should().OnlyContain({var_name} => {condition});"
        content = content.replace(old, new)

    return content, count

def main():
    file_path = "tests/Api.Tests/Services/RuleCommentServiceTests.cs"

    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    original_content = content
    total_conversions = 0

    conversions = [
        ("Assert.ThrowsAsync (multiline)", convert_throws_async_multiline),
        ("Assert.DoesNotContain", convert_does_not_contain),
        ("Assert.Equal with .First()", convert_equal_with_first),
        ("Assert.Single with .First()", convert_single_with_first),
        ("Assert.False with .First()", convert_false_with_first),
        ("Assert.All with nested", convert_all_nested),
    ]

    for name, converter in conversions:
        content, count = converter(content)
        if count > 0:
            print(f"[OK] Converted {count} {name} assertions")
            total_conversions += count

    if content != original_content:
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(content)
        print(f"\n[SUCCESS] Successfully migrated {total_conversions} additional assertions")
        return 0
    else:
        print("[WARN] No conversions needed")
        return 1

if __name__ == "__main__":
    sys.exit(main())
