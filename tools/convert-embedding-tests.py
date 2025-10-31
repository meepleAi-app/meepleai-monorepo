#!/usr/bin/env python3
"""Convert EmbeddingServiceMultilingualTests.cs specifically."""
import re
from pathlib import Path

file_path = Path("tests/Api.Tests/Services/EmbeddingServiceMultilingualTests.cs")
content = file_path.read_text(encoding='utf-8')

# Pattern 1: Assert.True(result.Success) → result.Success.Should().BeTrue()
content = re.sub(
    r'Assert\.True\(result\.Success\);',
    r'result.Success.Should().BeTrue();',
    content
)

# Pattern 2: Assert.True with message
content = re.sub(
    r'Assert\.True\(result\.Success, \$"Expected success but got failure\. Error: \{result\.ErrorMessage\}"\);',
    r'result.Success.Should().BeTrue($"Expected success but got failure. Error: {result.ErrorMessage}");',
    content
)

# Pattern 3: Assert.False(result.Success)
content = re.sub(
    r'Assert\.False\(result\.Success\);',
    r'result.Success.Should().BeFalse();',
    content
)

# Pattern 4: Simple Assert.Equal for counts
content = re.sub(
    r'Assert\.Equal\(([0-9]+), result\.Embeddings\.Count\);',
    r'result.Embeddings.Count.Should().Be(\1);',
    content
)

# Pattern 5: Assert.Equal for array lengths
content = re.sub(
    r'Assert\.Equal\(([0-9]+), result\.Embeddings\[0\]\.Length\);( //[^\n]*)?',
    r'result.Embeddings[0].Length.Should().Be(\1);\2',
    content
)

# Pattern 6: Assert.Equal for string properties with JsonDocument
content = re.sub(
    r'Assert\.Equal\("([^"]+)", requestData\.GetProperty\("([^"]+)"\)\.GetString\(\)\);',
    r'requestData.GetProperty("\2").GetString().Should().Be("\1");',
    content
)

# Pattern 7: Assert.Equal for array length with JsonDocument
content = re.sub(
    r'Assert\.Equal\(([0-9]+), requestData\.GetProperty\("([^"]+)"\)\.GetArrayLength\(\)\);',
    r'requestData.GetProperty("\2").GetArrayLength().Should().Be(\1);',
    content
)

# Pattern 8: Assert.Equal with error messages
content = re.sub(
    r'Assert\.Equal\("([^"]+)", result\.ErrorMessage\);',
    r'result.ErrorMessage.Should().Be("\1");',
    content
)

# Pattern 9: Assert.All for collections
content = re.sub(
    r'Assert\.All\(result\.Embeddings, emb => Assert\.Equal\(([0-9]+), emb\.Length\)\);( //[^\n]*)?',
    r'result.Embeddings.Should().OnlyContain(emb => emb.Length == \1);\2',
    content
)

file_path.write_text(content, encoding='utf-8')
print("✅ Converted EmbeddingServiceMultilingualTests.cs")
