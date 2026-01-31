#!/usr/bin/env python3
"""
Safe FluentAssertions migration script.
Handles complex patterns correctly.
"""
import re
import sys
from pathlib import Path
import subprocess

def add_using(content):
    """Add using FluentAssertions if missing."""
    if "using FluentAssertions;" in content:
        return content
    return content.replace("using Xunit;", "using Xunit;\nusing FluentAssertions;")

def convert_simple_assertions(content):
    """Convert only 100% safe assertions."""
    # Pattern 1: Assert.NotNull(variable) → variable.Should().NotBeNull()
    content = re.sub(
        r'Assert\.NotNull\(([a-zA-Z0-9_\.]+)\);',
        r'\1.Should().NotBeNull();',
        content
    )

    # Pattern 2: Assert.Null(variable) → variable.Should().BeNull()
    content = re.sub(
        r'Assert\.Null\(([a-zA-Z0-9_\.]+)\);',
        r'\1.Should().BeNull();',
        content
    )

    # Pattern 3: Assert.Empty(variable) → variable.Should().BeEmpty()
    content = re.sub(
        r'Assert\.Empty\(([a-zA-Z0-9_\.]+)\);',
        r'\1.Should().BeEmpty();',
        content
    )

    # Pattern 4: Assert.NotEmpty(variable) → variable.Should().NotBeEmpty()
    content = re.sub(
        r'Assert\.NotEmpty\(([a-zA-Z0-9_\.]+)\);',
        r'\1.Should().NotBeEmpty();',
        content
    )

    # Pattern 5: Assert.Equal(HttpStatusCode.X, response.StatusCode)
    content = re.sub(
        r'Assert\.Equal\(HttpStatusCode\.(\w+), response\.StatusCode\);',
        r'response.StatusCode.Should().Be(HttpStatusCode.\1);',
        content
    )

    return content

def process_file(file_path):
    """Process a single file."""
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    original = content
    content = add_using(content)
    content = convert_simple_assertions(content)

    if content != original:
        with open(file_path, 'w', encoding='utf-8', newline='\r\n') as f:
            f.write(content)
        return True
    return False

def main():
    test_dir = Path("tests/Api.Tests")

    # Already migrated files
    skip_files = {
        "EncryptionServiceTests.cs",
        "CacheWarmingServiceTests.cs",
        "AdminAuthorizationTests.cs",
        "AdminRequestsEndpointsTests.cs",
        "AdminStatsEndpointsTests.cs",
        "AgentEndpointsErrorTests.cs",
        "AgentFeedbackServiceTests.cs"
    }

    # Find all .cs files with Assert
    files_to_process = []
    for cs_file in test_dir.rglob("*.cs"):
        if cs_file.name in skip_files:
            continue
        if "Assert." in cs_file.read_text(encoding='utf-8'):
            files_to_process.append(cs_file)

    print(f"Found {len(files_to_process)} files to migrate")

    converted = 0
    batch_size = 20

    for i in range(0, len(files_to_process), batch_size):
        batch = files_to_process[i:i+batch_size]
        print(f"\nBatch {i//batch_size + 1} ({len(batch)} files)...")

        for file_path in batch:
            if process_file(file_path):
                converted += 1

        # Verify build
        print("  Building...")
        result = subprocess.run(
            ["dotnet", "build", "--no-restore"],
            capture_output=True,
            text=True
        )

        if result.returncode != 0:
            print(f"  [X] Build failed! Reverting batch...")
            subprocess.run(["git", "restore"] + [str(f) for f in batch])
            print(f"  Batch {i//batch_size + 1} skipped (complex patterns)")
            converted -= len(batch)
        else:
            print(f"  [OK] Batch {i//batch_size + 1} OK")

    print(f"\n[OK] Migrated {converted} files successfully")

if __name__ == "__main__":
    main()
