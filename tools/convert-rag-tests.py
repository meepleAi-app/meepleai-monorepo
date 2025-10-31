#!/usr/bin/env python3
"""Convert RagServiceTests.cs to FluentAssertions."""
import re
from pathlib import Path

file_path = Path("tests/Api.Tests/RagServiceTests.cs")
content = file_path.read_text(encoding='utf-8')

# Pattern 1: Assert.Equal(string, property)
content = re.sub(
    r'Assert\.Equal\("([^"]+)", result\.answer\);',
    r'result.answer.Should().Be("\1");',
    content
)

content = re.sub(
    r'Assert\.Equal\("([^"]+)", result\.script\);',
    r'result.script.Should().Be("\1");',
    content
)

# Pattern 2: Assert.Equal(number, count/tokens)
content = re.sub(
    r'Assert\.Equal\(([0-9]+), result\.snippets\.Count\);',
    r'result.snippets.Count.Should().Be(\1);',
    content
)

content = re.sub(
    r'Assert\.Equal\(([0-9]+), result\.(\w+)\);',
    r'result.\2.Should().Be(\1);',
    content
)

# Pattern 3: Assert.Equal for nested properties
content = re.sub(
    r'Assert\.Equal\(([a-zA-Z0-9_]+), result\.outline\.mainTopic\);',
    r'result.outline.mainTopic.Should().Be(\1);',
    content
)

content = re.sub(
    r'Assert\.Equal\(([0-9]+), result\.outline\.sections\.Count\);',
    r'result.outline.sections.Count.Should().Be(\1);',
    content
)

# Pattern 4: Assert.Equal for array elements
content = re.sub(
    r'Assert\.Equal\("([^"]+)", result\.snippets\[(\d+)\]\.text\);',
    r'result.snippets[\2].text.Should().Be("\1");',
    content
)

content = re.sub(
    r'Assert\.Equal\("([^"]+)", result\.snippets\[(\d+)\]\.source\);',
    r'result.snippets[\2].source.Should().Be("\1");',
    content
)

content = re.sub(
    r'Assert\.Equal\(([0-9]+), result\.snippets\[(\d+)\]\.page\);',
    r'result.snippets[\2].page.Should().Be(\1);',
    content
)

# Pattern 5: Assert.Contains
content = re.sub(
    r'Assert\.Contains\("([^"]+)", result\.script\);',
    r'result.script.Should().Contain("\1");',
    content
)

# Pattern 6: Assert.True with comparison
content = re.sub(
    r'Assert\.True\(result\.estimatedReadingTimeMinutes > 0\);',
    r'result.estimatedReadingTimeMinutes.Should().BeGreaterThan(0);',
    content
)

# Pattern 7: Assert.Same
content = re.sub(
    r'Assert\.Same\(cachedResponse, result\);',
    r'result.Should().BeSameAs(cachedResponse);',
    content
)

# Pattern 8: Assert.Collection (complex - need to handle manually)
# Skip this one for now

file_path.write_text(content, encoding='utf-8')
print("Converted RagServiceTests.cs")

# Check remaining
remaining = content.count("Assert.")
print(f"Remaining Assert statements: {remaining}")
