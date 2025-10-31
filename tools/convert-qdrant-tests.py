#!/usr/bin/env python3
"""Convert QdrantServiceIntegrationTests.cs."""
import re
from pathlib import Path

file_path = Path("tests/Api.Tests/QdrantServiceIntegrationTests.cs")
content = file_path.read_text(encoding='utf-8')

# Pattern 1: Assert.True(result.Success)
content = re.sub(
    r'Assert\.True\((\w+)\.Success\);',
    r'\1.Success.Should().BeTrue();',
    content
)

# Pattern 2: Assert.True(deleteResult)
content = re.sub(
    r'Assert\.True\((\w+)\);',
    r'\1.Should().BeTrue();',
    content
)

# Pattern 3: Assert.Equal for simple numbers
content = re.sub(
    r'Assert\.Equal\(([0-9]+), (\w+)\.IndexedCount\);',
    r'\2.IndexedCount.Should().Be(\1);',
    content
)

content = re.sub(
    r'Assert\.Equal\(([0-9]+), (\w+)\.Results\.Count\);',
    r'\2.Results.Count.Should().Be(\1);',
    content
)

content = re.sub(
    r'Assert\.Equal\(([0-9]+), (\w+)\.Results\[0\]\.Page\);',
    r'\2.Results[0].Page.Should().Be(\1);',
    content
)

# Pattern 4: Assert.Equal for variables
content = re.sub(
    r'Assert\.Equal\((\w+), (\w+)\.Results\[0\]\.PdfId\);',
    r'\2.Results[0].PdfId.Should().Be(\1);',
    content
)

# Pattern 5: Assert.Contains with predicate
content = re.sub(
    r'Assert\.Contains\((\w+)\.Results, ([^)]+)\);',
    r'\1.Results.Should().Contain(\2);',
    content
)

file_path.write_text(content, encoding='utf-8')
print("[OK] Converted QdrantServiceIntegrationTests.cs")
