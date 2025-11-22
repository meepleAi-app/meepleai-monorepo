#!/usr/bin/env python3
"""
Fix null-coalescing validation in all C# handler constructors.
Adds ?? throw new ArgumentNullException(nameof(param)) pattern.
"""

import re
import os
import sys
from pathlib import Path
from typing import List, Tuple

def find_handler_files(base_path: Path) -> List[Path]:
    """Find all *Handler.cs files in BoundedContexts."""
    handlers = []
    contexts_path = base_path / "apps/api/src/Api/BoundedContexts"
    if contexts_path.exists():
        for handler in contexts_path.rglob("*Handler.cs"):
            handlers.append(handler)
    return handlers

def needs_fixing(content: str) -> bool:
    """Check if file needs null-coalescing fixes."""
    # Check for direct assignment pattern
    has_direct_assignment = bool(re.search(r'^\s+_\w+\s*=\s*\w+;$', content, re.MULTILINE))
    # Check if already has null-coalescing
    has_null_coalescing = '??' in content
    return has_direct_assignment and not has_null_coalescing

def extract_constructor_info(content: str) -> Tuple[str, List[Tuple[str, str]]]:
    """Extract constructor and its parameter assignments."""
    # Find constructor pattern
    constructor_pattern = r'public\s+(\w+Handler)\s*\([^)]*\)\s*\{([^}]+)\}'
    match = re.search(constructor_pattern, content, re.DOTALL)

    if not match:
        return "", []

    constructor_body = match.group(2)

    # Find all field assignments
    assignment_pattern = r'^\s+(_\w+)\s*=\s*(\w+);$'
    assignments = []
    for line in constructor_body.split('\n'):
        assign_match = re.match(assignment_pattern, line)
        if assign_match:
            field_name = assign_match.group(1)
            param_name = assign_match.group(2)
            assignments.append((field_name, param_name))

    return constructor_body, assignments

def fix_constructor(content: str) -> str:
    """Fix constructor by adding null-coalescing to assignments."""
    _, assignments = extract_constructor_info(content)

    if not assignments:
        return content

    # Replace each assignment with null-coalescing version
    for field_name, param_name in assignments:
        # Skip TimeProvider with default value pattern
        if 'TimeProvider.System' in content:
            continue

        old_pattern = f'{field_name} = {param_name};'
        new_pattern = f'{field_name} = {param_name} ?? throw new ArgumentNullException(nameof({param_name}));'
        content = content.replace(old_pattern, new_pattern)

    return content

def main():
    """Main execution."""
    repo_root = Path(__file__).parent.parent
    handlers = find_handler_files(repo_root)

    fixed_count = 0
    fixed_files = []
    total_params = 0

    print(f"Found {len(handlers)} handler files")

    for handler_file in handlers:
        try:
            with open(handler_file, 'r', encoding='utf-8') as f:
                content = f.read()

            if not needs_fixing(content):
                continue

            _, assignments = extract_constructor_info(content)
            if not assignments:
                continue

            new_content = fix_constructor(content)

            if new_content != content:
                with open(handler_file, 'w', encoding='utf-8') as f:
                    f.write(new_content)

                fixed_count += 1
                total_params += len(assignments)
                rel_path = handler_file.relative_to(repo_root)
                fixed_files.append((str(rel_path), len(assignments)))
                print(f"✓ Fixed {rel_path} ({len(assignments)} params)")

        except Exception as e:
            print(f"✗ Error processing {handler_file}: {e}", file=sys.stderr)

    # Print summary
    print(f"\n{'='*60}")
    print(f"SUMMARY")
    print(f"{'='*60}")
    print(f"Total handlers fixed: {fixed_count}")
    print(f"Total parameters validated: {total_params}")
    print(f"\nFixed files by context:")

    # Group by bounded context
    contexts = {}
    for file_path, param_count in fixed_files:
        parts = file_path.split(os.sep)
        if 'BoundedContexts' in parts:
            idx = parts.index('BoundedContexts')
            if idx + 1 < len(parts):
                context = parts[idx + 1]
                if context not in contexts:
                    contexts[context] = []
                contexts[context].append((file_path, param_count))

    for context in sorted(contexts.keys()):
        files = contexts[context]
        params = sum(p for _, p in files)
        print(f"\n{context}: {len(files)} files, {params} parameters")
        for file_path, param_count in files:
            print(f"  - {file_path} ({param_count} params)")

if __name__ == "__main__":
    main()
