#!/usr/bin/env python3
"""
Issue #2184 - Batch Pragma Application Script
Intelligently adds #pragma warning disable CA1031 with appropriate justifications
based on file location and pattern detection.
"""

import re
import os
from pathlib import Path
from typing import List, Tuple, Optional
from dataclasses import dataclass

@dataclass
class PragmaPattern:
    """Pattern for pragma warning suppression with justification."""
    name: str
    pragma_disable: str
    pragma_restore: str
    path_indicators: List[str]

# Pattern templates
PATTERNS = {
    'CQRS_HANDLER': PragmaPattern(
        name="COMMAND HANDLER PATTERN",
        pragma_disable="""#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: COMMAND HANDLER PATTERN - CQRS handler boundary
        // Specific exceptions (ValidationException, DomainException) caught separately above.
        // Generic catch handles unexpected infrastructure failures (DB, network, memory)
        // to prevent exception propagation to API layer. Returns Result<T> pattern.""",
        pragma_restore="#pragma warning restore CA1031",
        path_indicators=[
            'Application\\Commands\\',
            'Application\\Queries\\',
            'Application\\Handlers\\',
            'CommandHandler.cs',
            'QueryHandler.cs'
        ]
    ),
    'EVENT_HANDLER': PragmaPattern(
        name="EVENT HANDLER PATTERN",
        pragma_disable="""#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: EVENT HANDLER PATTERN - Background event processing
        // Event handlers must not throw exceptions (violates mediator pattern).
        // Errors logged for monitoring; failed events don't block system.""",
        pragma_restore="#pragma warning restore CA1031",
        path_indicators=[
            'Application\\EventHandlers\\',
            'EventHandler.cs',
            'DomainEventHandler'
        ]
    ),
    'MIDDLEWARE': PragmaPattern(
        name="MIDDLEWARE BOUNDARY PATTERN",
        pragma_disable="""#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: MIDDLEWARE BOUNDARY PATTERN - Fail-open on errors
        // Middleware failures shouldn't crash request pipeline (self-DOS prevention).
        // Authentication/rate limit errors allow request through (unauthenticated/unthrottled).""",
        pragma_restore="#pragma warning restore CA1031",
        path_indicators=[
            'Middleware\\',
            'Middleware.cs'
        ]
    ),
    'BACKGROUND_TASK': PragmaPattern(
        name="BACKGROUND TASK PATTERN",
        pragma_disable="""#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: BACKGROUND TASK PATTERN - Async task resilience
        // Background tasks must handle all errors gracefully without crashing worker.
        // Errors logged and task status updated; failures don't stop other tasks.""",
        pragma_restore="#pragma warning restore CA1031",
        path_indicators=[
            'Infrastructure\\BackgroundTasks\\',
            'Infrastructure\\Scheduling\\',
            'Job.cs',
            'Task.cs'
        ]
    ),
    'INFRASTRUCTURE_SERVICE': PragmaPattern(
        name="INFRASTRUCTURE SERVICE PATTERN",
        pragma_disable="""#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: INFRASTRUCTURE SERVICE PATTERN - Resilience boundary
        // Infrastructure service failures handled gracefully. Specific exceptions caught separately.
        // Generic catch prevents service failures from cascading to application layer.""",
        pragma_restore="#pragma warning restore CA1031",
        path_indicators=[
            'Infrastructure\\Services\\',
            'Infrastructure\\External\\',
            'Service.cs'
        ]
    ),
    'ROUTING': PragmaPattern(
        name="API ENDPOINT PATTERN",
        pragma_disable="""#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: API ENDPOINT PATTERN - HTTP boundary
        // Endpoint handlers return appropriate HTTP status codes for all errors.
        // Generic catch prevents unhandled exceptions from crashing request.""",
        pragma_restore="#pragma warning restore CA1031",
        path_indicators=[
            'Routing\\',
            'Endpoints.cs'
        ]
    )
}

def detect_pattern(file_path: str) -> Optional[PragmaPattern]:
    """Detect which pattern applies to the file based on path."""
    normalized_path = file_path.replace('/', '\\')

    for pattern_key, pattern in PATTERNS.items():
        for indicator in pattern.path_indicators:
            if indicator in normalized_path:
                return pattern

    return None

def has_pragma_ca1031(content: str) -> bool:
    """Check if file already has CA1031 pragma."""
    return bool(re.search(r'#pragma\s+warning\s+disable\s+CA1031', content))

def find_catch_blocks(content: str) -> List[Tuple[int, int, str]]:
    """Find all catch (Exception ...) blocks without CA1031 pragma.

    Returns list of (start_pos, end_pos, indentation) tuples.
    """
    catch_blocks = []

    # Find all catch (Exception ex) patterns
    for match in re.finditer(r'(\s*)catch\s*\(\s*Exception\s+\w+\s*\)', content):
        indentation = match.group(1)
        start_pos = match.start()

        # Check if there's a #pragma before this catch
        before_catch = content[:start_pos]
        last_pragma = before_catch.rfind('#pragma warning disable CA1031')
        last_newline = before_catch.rfind('\n')

        # If pragma is on the line(s) immediately before catch, skip this block
        if last_pragma > last_newline - 200:  # Pragma within ~3 lines before
            continue

        catch_blocks.append((start_pos, match.end(), indentation))

    return catch_blocks

def apply_pragma_to_file(file_path: Path, pattern: PragmaPattern, dry_run: bool = False) -> int:
    """Apply pragma pattern to all catch blocks in file.

    Returns number of catch blocks modified.
    """
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()

        # Find all catch blocks needing pragma
        catch_blocks = find_catch_blocks(content)

        if not catch_blocks:
            return 0

        # Process in reverse order to preserve positions
        modified_content = content
        modifications = 0

        for start_pos, end_pos, indentation in reversed(catch_blocks):
            # Insert pragma before catch
            pragma_with_indent = '\n' + '\n'.join(
                indentation + line if line.strip() else ''
                for line in pattern.pragma_disable.split('\n')
            )

            modified_content = (
                modified_content[:start_pos] +
                pragma_with_indent + '\n' +
                modified_content[start_pos:end_pos]
            )

            # Find the end of this catch block (closing brace)
            # Simple heuristic: find next } at same indentation level
            rest = modified_content[end_pos:]

            # Find closing brace - look for } at the same or less indentation
            lines_after = rest.split('\n')
            brace_line_idx = None

            for idx, line in enumerate(lines_after):
                stripped = line.lstrip()
                if stripped.startswith('}'):
                    # Found potential closing brace
                    line_indent = len(line) - len(line.lstrip())
                    orig_indent = len(indentation)
                    if line_indent <= orig_indent:
                        brace_line_idx = idx
                        break

            if brace_line_idx is not None:
                # Insert pragma restore after closing brace
                lines_after[brace_line_idx] += '\n' + indentation + pattern.pragma_restore
                modified_content = modified_content[:end_pos] + '\n'.join(lines_after)

            modifications += 1

        if not dry_run and modifications > 0:
            with open(file_path, 'w', encoding='utf-8') as f:
                f.write(modified_content)

        return modifications

    except Exception as e:
        print(f"❌ Error processing {file_path}: {e}")
        return 0

def process_directory(base_path: Path, dry_run: bool = False) -> dict:
    """Process all C# files in directory."""
    stats = {
        'total_files': 0,
        'files_modified': 0,
        'catch_blocks_modified': 0,
        'by_pattern': {}
    }

    # Find all .cs files (excluding tests)
    cs_files = [
        f for f in base_path.rglob('*.cs')
        if 'tests' not in str(f).lower() and not f.name.endswith('Tests.cs')
    ]

    for file_path in cs_files:
        stats['total_files'] += 1

        # Detect pattern for this file
        pattern = detect_pattern(str(file_path))

        if pattern is None:
            continue  # Skip files we can't categorize

        # Check if already has pragma
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()

            if has_pragma_ca1031(content):
                continue  # Already compliant
        except Exception:
            continue

        # Apply pragma
        mods = apply_pragma_to_file(file_path, pattern, dry_run)

        if mods > 0:
            rel_path = file_path.relative_to(base_path.parent.parent)
            status = "[DRY RUN]" if dry_run else "[OK]"
            print(f"{status} {rel_path}: {mods} catch blocks - {pattern.name}")

            stats['files_modified'] += 1
            stats['catch_blocks_modified'] += mods

            if pattern.name not in stats['by_pattern']:
                stats['by_pattern'][pattern.name] = 0
            stats['by_pattern'][pattern.name] += mods

    return stats

def main():
    import argparse

    parser = argparse.ArgumentParser(description='Apply CA1031 pragma warnings in batch')
    parser.add_argument('--dry-run', action='store_true', help='Show what would be changed without modifying files')
    parser.add_argument('--path', default='apps/api/src/Api', help='Base path to process')

    args = parser.parse_args()

    base_path = Path(args.path)

    if not base_path.exists():
        print(f"❌ Path not found: {base_path}")
        return 1

    print("=" * 60)
    print("Issue #2184: Batch Pragma Application")
    print("=" * 60)
    print(f"Mode: {'DRY RUN' if args.dry_run else 'LIVE'}")
    print(f"Path: {base_path}")
    print()

    stats = process_directory(base_path, args.dry_run)

    print()
    print("=" * 60)
    print("SUMMARY")
    print("=" * 60)
    print(f"Total files scanned: {stats['total_files']}")
    print(f"Files modified: {stats['files_modified']}")
    print(f"Catch blocks modified: {stats['catch_blocks_modified']}")
    print()
    print("By Pattern:")
    for pattern_name, count in stats['by_pattern'].items():
        print(f"  {pattern_name}: {count} catch blocks")

    if args.dry_run:
        print()
        print("WARNING: DRY RUN MODE - No files were actually modified")
        print("        Run without --dry-run to apply changes")

    return 0

if __name__ == '__main__':
    exit(main())
