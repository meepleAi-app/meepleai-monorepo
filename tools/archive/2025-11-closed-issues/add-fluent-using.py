#!/usr/bin/env python3
"""Add using FluentAssertions to all test files."""
import re
from pathlib import Path

test_dir = Path("tests/Api.Tests")

# Already migrated
skip = {
    "EncryptionServiceTests.cs", "CacheWarmingServiceTests.cs",
    "AdminAuthorizationTests.cs", "AdminRequestsEndpointsTests.cs",
    "AdminStatsEndpointsTests.cs", "AgentEndpointsErrorTests.cs",
    "AgentFeedbackServiceTests.cs"
}

count = 0
for cs_file in test_dir.rglob("*.cs"):
    if cs_file.name in skip:
        continue

    content = cs_file.read_text(encoding='utf-8')

    if "Assert." not in content:
        continue

    if "using FluentAssertions;" in content:
        continue

    # Add using after Xunit
    if "using Xunit;" in content:
        content = content.replace(
            "using Xunit;",
            "using Xunit;\nusing FluentAssertions;"
        )
        cs_file.write_text(content, encoding='utf-8')
        count += 1
        print(f"Added: {cs_file.name}")

print(f"\nAdded using FluentAssertions to {count} files")
