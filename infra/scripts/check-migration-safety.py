#!/usr/bin/env python3
"""Migration safety gate — parses `dotnet ef migrations script` output and flags
forbidden patterns per rollback-runbook §8.2 unless a `-- safe: <rationale>`
directive bypasses with audit trail (refs #1087).

Usage
-----
    check-migration-safety.py --sql staging.sql [--gate-ship-date YYYYMMDD]
                              [--format text|json] [--report path]

Exit codes
----------
    0 — no unsafe pattern (or all bypassed with valid directives)
    1 — at least one unsafe pattern without directive
    2 — invocation error (missing file, malformed args)

Convention
----------
Author a migration that contains a forbidden pattern only when the migration
is rollback-safe per §8.3 (expand → migrate → contract). Document the rationale
on the FIRST line of the migration's Up() body:

    public partial class DropEmailColumn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("-- safe: drop legacy email column after 7-day soak (§8.3)");
            migrationBuilder.DropColumn("email", "users");
        }
    }

The directive is emitted into the generated SQL by EF Core and the gate picks
it up. CODEOWNERS already routes any migration change to the backend lead;
a CI comment is posted whenever a directive is exercised, for audit trail.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Iterable


# ---------------------------------------------------------------------------
# Forbidden patterns — keep in sync with rollback-runbook.md §8.2
# ---------------------------------------------------------------------------

PATTERNS: list[tuple[str, re.Pattern[str], str]] = [
    (
        "DROP_COLUMN",
        re.compile(r"\bDROP\s+COLUMN\b", re.IGNORECASE),
        "DROP COLUMN -- previous code may still read it",
    ),
    (
        "DROP_TABLE",
        re.compile(r"\bDROP\s+TABLE\b", re.IGNORECASE),
        "DROP TABLE -- previous code may still reference it",
    ),
    (
        "ALTER_COLUMN_TYPE",
        re.compile(r"\bALTER\s+COLUMN\b[^;]*?\bTYPE\b", re.IGNORECASE),
        "ALTER COLUMN TYPE -- may narrow / break previous code",
    ),
    (
        "RENAME_COLUMN",
        re.compile(r"\bRENAME\s+COLUMN\b", re.IGNORECASE),
        "RENAME COLUMN -- previous code reads the old name",
    ),
    (
        "RENAME_TABLE",
        re.compile(r"\bALTER\s+TABLE\b[^;]*?\bRENAME\s+TO\b", re.IGNORECASE),
        "RENAME TABLE -- previous code references the old name",
    ),
]

# ADD COLUMN ... NOT NULL without DEFAULT — handled separately so we can look
# ahead for DEFAULT within the same statement.
_ADD_COL_RE = re.compile(r"\bADD\s+COLUMN\b[^;]*", re.IGNORECASE)
_NOT_NULL_RE = re.compile(r"\bNOT\s+NULL\b", re.IGNORECASE)
_DEFAULT_RE = re.compile(r"\bDEFAULT\b", re.IGNORECASE)

# Migration block header emitted by `dotnet ef migrations script`.
_MIGRATION_HEADER_RE = re.compile(r"--\s*Migration:\s*(\d{14})_(\S+)", re.IGNORECASE)

# Allow directive — rationale must be non-empty after trimming.
# Use [ \t]* (NOT \s*) so the directive cannot accidentally consume the newline
# and capture text from the following line.
_SAFE_DIRECTIVE_RE = re.compile(r"--[ \t]*safe:[ \t]*(.+?)[ \t]*$", re.IGNORECASE | re.MULTILINE)


@dataclass
class Finding:
    migration_id: str
    pattern: str
    description: str
    excerpt: str
    line_number: int
    allowed: bool = False
    rationale: str = ""

    def to_dict(self) -> dict:
        return asdict(self)


@dataclass
class _Block:
    migration_id: str
    name: str
    start_line: int  # 1-based, line of the header in the full SQL
    body: str  # SQL text of the block (after the header, before next header)


# ---------------------------------------------------------------------------
# Block splitting
# ---------------------------------------------------------------------------


def _split_into_blocks(sql: str) -> list[_Block]:
    """Split SQL output into per-migration blocks keyed by `-- Migration: <id>`.

    Lines before the first header are dropped (they are EF preamble — `START
    TRANSACTION`, schema setup, etc. — not attributable to any single migration
    and never contain forbidden patterns from §8.2).
    """
    lines = sql.splitlines(keepends=True)
    blocks: list[_Block] = []
    current: _Block | None = None
    buf: list[str] = []

    for idx, line in enumerate(lines, start=1):
        m = _MIGRATION_HEADER_RE.match(line.strip())
        if m:
            if current is not None:
                current.body = "".join(buf)
                blocks.append(current)
            current = _Block(migration_id=m.group(1), name=m.group(2), start_line=idx, body="")
            buf = []
        elif current is not None:
            buf.append(line)
    if current is not None:
        current.body = "".join(buf)
        blocks.append(current)
    return blocks


# ---------------------------------------------------------------------------
# String-literal stripping (so patterns in 'literals' don't false-positive)
# ---------------------------------------------------------------------------

_STRING_LITERAL_RE = re.compile(r"'(?:''|[^'])*'")


def _strip_string_literals(line: str) -> str:
    """Replace every SQL string literal with empty quotes, preserving column count
    is not required — only that pattern regexes don't match inside literals."""
    return _STRING_LITERAL_RE.sub("''", line)


# ---------------------------------------------------------------------------
# Per-block scanning
# ---------------------------------------------------------------------------


def _scan_block(block: _Block) -> list[Finding]:
    """Scan a single migration block and return findings (with `allowed` set)."""
    findings: list[Finding] = []

    # 1. Collect safe directives (non-empty rationale only).
    rationales: list[str] = []
    for m in _SAFE_DIRECTIVE_RE.finditer(block.body):
        rationale = m.group(1).strip()
        if rationale:
            rationales.append(rationale)

    primary_rationale = rationales[0] if rationales else ""

    # 2. Scan each line for forbidden patterns. Comments and string literals
    #    are stripped to suppress false positives.
    for offset, raw_line in enumerate(block.body.splitlines(), start=1):
        stripped = raw_line.lstrip()
        if stripped.startswith("--"):
            continue  # SQL line comment — never executable
        code = _strip_string_literals(raw_line)

        for name, pattern, description in PATTERNS:
            if pattern.search(code):
                findings.append(
                    Finding(
                        migration_id=block.migration_id,
                        pattern=name,
                        description=description,
                        excerpt=raw_line.strip(),
                        line_number=block.start_line + offset,
                        allowed=bool(primary_rationale),
                        rationale=primary_rationale,
                    )
                )

        # ADD COLUMN ... NOT NULL without DEFAULT (look-ahead within statement).
        for match in _ADD_COL_RE.finditer(code):
            stmt = match.group(0)
            if _NOT_NULL_RE.search(stmt) and not _DEFAULT_RE.search(stmt):
                findings.append(
                    Finding(
                        migration_id=block.migration_id,
                        pattern="ADD_COLUMN_NOT_NULL_NO_DEFAULT",
                        description="ADD COLUMN NOT NULL without DEFAULT -- fails on existing rows",
                        excerpt=raw_line.strip(),
                        line_number=block.start_line + offset,
                        allowed=bool(primary_rationale),
                        rationale=primary_rationale,
                    )
                )

    return findings


# ---------------------------------------------------------------------------
# Public API
# ---------------------------------------------------------------------------


def scan_sql(sql: str, gate_ship_date: str) -> list[Finding]:
    """Scan a `dotnet ef migrations script` output for §8.2 violations.

    Migrations strictly older than `gate_ship_date` (YYYYMMDD prefix of the
    migration timestamp) are grandfathered and skipped.
    """
    if not re.fullmatch(r"\d{8}", gate_ship_date):
        raise ValueError(f"gate_ship_date must be YYYYMMDD; got {gate_ship_date!r}")

    findings: list[Finding] = []
    for block in _split_into_blocks(sql):
        # Grandfathering: migration timestamp is YYYYMMDDhhmmss; compare the date prefix.
        if block.migration_id[:8] < gate_ship_date:
            continue
        findings.extend(_scan_block(block))
    return findings


def run_check(sql: str, gate_ship_date: str) -> int:
    """Convenience wrapper: scan + print summary + return exit code."""
    findings = scan_sql(sql, gate_ship_date)
    return _report(findings, fmt="text", out=sys.stdout)


def _report(findings: Iterable[Finding], fmt: str, out, report_path: Path | None = None) -> int:
    findings = list(findings)
    unsafe = [f for f in findings if not f.allowed]
    allowed = [f for f in findings if f.allowed]

    if fmt == "json":
        payload = {
            "unsafe": [f.to_dict() for f in unsafe],
            "allowed": [f.to_dict() for f in allowed],
            "exit_code": 1 if unsafe else 0,
        }
        out.write(json.dumps(payload, indent=2) + "\n")
    else:
        # ASCII-only markers: Windows cp1252 stdout cannot encode emoji / em-dash / bullets.
        if unsafe:
            out.write(f"\n[FAIL] Migration safety gate FAILED -- {len(unsafe)} unsafe pattern(s) found:\n\n")
            for f in unsafe:
                out.write(
                    f"  - Migration {f.migration_id} (line {f.line_number}): "
                    f"{f.pattern}: {f.description}\n"
                    f"      {f.excerpt}\n"
                )
            out.write(
                "\nIf this migration is rollback-safe per rollback-runbook section 8.3,\n"
                "add as the first statement of the Up() body:\n"
                '  migrationBuilder.Sql("-- safe: <non-empty rationale>");\n'
                "See docs/for-developers/operations/rollback-runbook.md section 8.4.\n"
            )
        else:
            out.write(f"[OK] Migration safety gate passed ({len(findings)} pattern(s) scanned, "
                      f"{len(allowed)} bypassed by directive).\n")

        if allowed:
            out.write("\nAudit log -- directive-bypassed patterns (CODEOWNERS approval required):\n")
            for f in allowed:
                out.write(
                    f"  - Migration {f.migration_id} (line {f.line_number}): "
                    f"{f.pattern} -> rationale: {f.rationale}\n"
                )

    if report_path is not None:
        report_path.write_text(
            json.dumps(
                {
                    "unsafe": [f.to_dict() for f in unsafe],
                    "allowed": [f.to_dict() for f in allowed],
                },
                indent=2,
            ),
            encoding="utf-8",
        )

    return 1 if unsafe else 0


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------


def _parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Migration safety gate — see rollback-runbook §8.4 and issue #1087.",
    )
    parser.add_argument(
        "--sql",
        required=True,
        type=Path,
        help="Path to `dotnet ef migrations script` output (.sql)",
    )
    parser.add_argument(
        "--gate-ship-date",
        default="20260518",
        help="YYYYMMDD; migrations strictly older are grandfathered (default: 20260518)",
    )
    parser.add_argument(
        "--format",
        choices=("text", "json"),
        default="text",
        help="Output format (default: text)",
    )
    parser.add_argument(
        "--report",
        type=Path,
        default=None,
        help="Optional path to write a structured JSON report (audit artifact)",
    )
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = _parse_args(argv if argv is not None else sys.argv[1:])
    if not args.sql.is_file():
        sys.stderr.write(f"error: SQL file not found: {args.sql}\n")
        return 2
    sql = args.sql.read_text(encoding="utf-8")
    findings = scan_sql(sql, gate_ship_date=args.gate_ship_date)
    return _report(findings, fmt=args.format, out=sys.stdout, report_path=args.report)


if __name__ == "__main__":
    sys.exit(main())
