#!/usr/bin/env python3
"""Convert EmbeddingServiceComprehensiveTests.cs."""
import re
from pathlib import Path

file_path = Path("tests/Api.Tests/EmbeddingServiceComprehensiveTests.cs")
content = file_path.read_text(encoding='utf-8')

# Assert.Throws (sync)
content = re.sub(
    r'var ex = Assert\.Throws<(\w+)>\(\(\) =>\s*',
    r'var ex = FluentActions.Invoking(() => ',
    content
)

# Close the Throws and add Should().Throw
lines = content.split('\n')
new_lines = []
i = 0
while i < len(lines):
    line = lines[i]

    # If line has FluentActions.Invoking and next line closes paren
    if 'FluentActions.Invoking' in line and 'var ex =' in line:
        # Find the closing line
        new_lines.append(line)
        i += 1
        indent_count = 0
        while i < len(lines):
            if lines[i].strip().startswith('new ') or lines[i].strip().endswith('));'):
                # This is the closing line
                close_line = lines[i].rstrip(');').rstrip() + ')'
                new_lines.append(close_line)
                # Add .Should().Throw line
                indent = '    ' * (len(lines[i]) - len(lines[i].lstrip())) // 4
                new_lines.append(f'{indent}    .Should().Throw<InvalidOperationException>();')
                i += 1
                break
            new_lines.append(lines[i])
            i += 1
    else:
        new_lines.append(line)
        i += 1

content = '\n'.join(new_lines)

# Assert.Contains(string, ex.Message) → ex.Message.Should().Contain(string)
content = re.sub(
    r'Assert\.Contains\("([^"]+)", ex\.Message\);',
    r'ex.Which.Message.Should().Contain("\1");',
    content
)

# Assert.True/False for Success
content = re.sub(r'Assert\.True\(result\.Success\);', r'result.Success.Should().BeTrue();', content)
content = re.sub(r'Assert\.False\(result\.Success\);', r'result.Success.Should().BeFalse();', content)

# Assert.Equal for counts and lengths
content = re.sub(r'Assert\.Equal\(([0-9]+), result\.Embeddings\.Count\);', r'result.Embeddings.Count.Should().Be(\1);', content)
content = re.sub(r'Assert\.Equal\(([0-9]+), result\.Embeddings\[0\]\.Length\);', r'result.Embeddings[0].Length.Should().Be(\1);', content)

# Assert.Single
content = re.sub(r'Assert\.Single\(result\.Embeddings\);', r'result.Embeddings.Should().ContainSingle();', content)

# Assert.All
content = re.sub(
    r'Assert\.All\(result\.Embeddings, emb => Assert\.Equal\(([0-9]+), emb\.Length\)\);',
    r'result.Embeddings.Should().OnlyContain(emb => emb.Length == \1);',
    content
)

# Assert.Contains for error messages
content = re.sub(
    r'Assert\.Contains\("([^"]+)", result\.ErrorMessage\);',
    r'result.ErrorMessage.Should().Contain("\1");',
    content
)

file_path.write_text(content, encoding='utf-8')
print("[OK] Converted")
