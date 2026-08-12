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
    2 — invocation error (missing file, malformed args) OR the parser could not
        attribute the SQL to any migration (see "Fail loud on nothing scanned")

Fail loud on nothing scanned (#3659)
------------------------------------
Between 2026-05-18 and 2026-08-11 this gate was green on *every* PR, including
one that dropped a column, because it recognised only a `-- Migration: <id>`
header that `dotnet ef migrations script --idempotent` has never emitted. With
no header there were no blocks, with no blocks nothing was scanned, and "zero
findings" was reported as success. The audit artifact of a PR containing a real
`DROP COLUMN` read, in full: `{"unsafe": [], "allowed": []}`.

The self-tests did not catch it because every fixture prepended that header by
hand — the parser was verified against a format that does not exist.

Hence two invariants, both enforced in `main()`:

  * scanning zero migrations is an ERROR (exit 2), never a pass;
  * SQL the parser cannot attribute to any migration is an ERROR, so a future
    change to EF's output shape fails loudly instead of silently returning to
    the vacuous green above.

"0 unsafe out of 7 migrations scanned" and "0 unsafe because nothing was read"
must never again be indistinguishable — the report carries the counts.

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

Note that EF puts each `migrationBuilder` call in its OWN `DO $EF$` block, so
the directive and the statement it authorises land in *different* blocks that
share only their `MigrationId`. Grouping must therefore be per migration, not
per block — one migration commonly expands into hundreds of blocks.
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

# --- Real `dotnet ef migrations script --idempotent` shape (#3659) -----------
#
#     DO $EF$
#     BEGIN
#         IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory"
#                       WHERE "MigrationId" = '20260811130532_PdfDocumentXminConcurrency') THEN
#         ALTER TABLE pdf_documents DROP COLUMN "RowVersion";
#         END IF;
#     END $EF$;
#
# One statement per block; a single migration expands into hundreds of them
# (measured on this repo: 1012 blocks for 7 migrations).
_EF_BLOCK_OPEN_RE = re.compile(r"^\s*DO\s+\$EF\$\s*$", re.IGNORECASE)
_EF_BLOCK_CLOSE_RE = re.compile(r"^\s*END\s+\$EF\$\s*;\s*$", re.IGNORECASE)

# The guard line that names the migration. `=` (not VALUES) deliberately: it must
# not match the closing `INSERT INTO "__EFMigrationsHistory" … VALUES ('<id>', …)`,
# which carries the same id but marks the migration as applied rather than
# opening it.
_EF_MIGRATION_GUARD_RE = re.compile(
    r'"MigrationId"\s*=\s*\'(\d{14})_([^\']+)\'', re.IGNORECASE
)

# Legacy header, kept only for back-compatibility with hand-written fixtures and
# any caller that pre-annotates its SQL. EF has never emitted this; relying on it
# alone is what made the gate blind for three months (#3659).
_MIGRATION_HEADER_RE = re.compile(r"--\s*Migration:\s*(\d{14})_(\S+)", re.IGNORECASE)

# EF preamble that legitimately sits outside every block: the history-table
# bootstrap and the surrounding transaction. Anything ELSE found outside a block
# means the parser has lost track of the format and must fail loudly.
_PREAMBLE_ALLOWED_RE = re.compile(
    r"^\s*(?:START\s+TRANSACTION\s*;|COMMIT\s*;|BEGIN\s*;|--.*)?\s*$", re.IGNORECASE
)
_HISTORY_TABLE_RE = re.compile(r"__EFMigrationsHistory", re.IGNORECASE)
_DDL_VERB_RE = re.compile(
    r"\b(?:CREATE|ALTER|DROP|INSERT|UPDATE|DELETE|TRUNCATE)\b", re.IGNORECASE
)

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
    """All SQL attributed to ONE migration, however many `DO $EF$` blocks it spans.

    `lines` carries absolute 1-based line numbers from the full SQL file so
    findings point at the real location instead of an offset from a header.
    """

    migration_id: str
    name: str
    lines: list[tuple[int, str]] = field(default_factory=list)

    @property
    def start_line(self) -> int:
        return self.lines[0][0] if self.lines else 0

    @property
    def body(self) -> str:
        return "\n".join(text for _, text in self.lines)


@dataclass
class ScanResult:
    """Findings plus the evidence that scanning actually happened (#3659)."""

    findings: list[Finding] = field(default_factory=list)
    migrations_seen: int = 0  # distinct MigrationIds recognised in the SQL
    migrations_scanned: int = 0  # after grandfathering by --gate-ship-date
    lines_scanned: int = 0  # lines attributed to some migration
    unattributed: list[tuple[int, str]] = field(default_factory=list)


# ---------------------------------------------------------------------------
# Block splitting
# ---------------------------------------------------------------------------


def _split_into_blocks(sql: str) -> tuple[list[_Block], list[tuple[int, str]]]:
    """Group the SQL by migration, and report every line it could not attribute.

    Handles the two shapes that can reach us:

    * the real `dotnet ef migrations script --idempotent` output — a long run of
      `DO $EF$ … END $EF$;` blocks, each naming its migration in the
      `"MigrationId" = '<id>'` guard. All blocks carrying the same id are merged
      into ONE `_Block`, because EF splits a single migration across hundreds of
      them and a `-- safe:` directive routinely lands in a different block than
      the statement it authorises;
    * the legacy `-- Migration: <id>` header, which EF does not emit but which
      pre-annotated fixtures may use.

    Returns `(blocks, unattributed)`. `unattributed` holds lines that belong to
    no migration and are not recognised preamble; the caller treats a non-empty
    list as a parser failure rather than as "nothing to see" (#3659).
    """
    lines = sql.splitlines()
    by_id: dict[str, _Block] = {}
    order: list[str] = []
    unattributed: list[tuple[int, str]] = []

    def _chunk(mig_id: str, name: str) -> _Block:
        if mig_id not in by_id:
            by_id[mig_id] = _Block(migration_id=mig_id, name=name)
            order.append(mig_id)
        return by_id[mig_id]

    legacy_current: _Block | None = None
    in_ef_block = False
    pending: list[tuple[int, str]] = []
    block_id: tuple[str, str] | None = None

    for idx, line in enumerate(lines, start=1):
        if _EF_BLOCK_OPEN_RE.match(line):
            in_ef_block, pending, block_id = True, [], None
            continue

        if in_ef_block:
            if _EF_BLOCK_CLOSE_RE.match(line):
                if block_id is not None:
                    _chunk(*block_id).lines.extend(pending)
                else:
                    # A `DO $EF$` block with no recognisable guard: the parser
                    # does not know whose SQL this is, so it must not be silently
                    # dropped.
                    unattributed.extend(pending)
                in_ef_block, pending, block_id = False, [], None
                continue
            guard = _EF_MIGRATION_GUARD_RE.search(line)
            if guard is not None and block_id is None:
                block_id = (guard.group(1), guard.group(2))
                continue  # the guard itself is scaffolding, not migration SQL
            pending.append((idx, line))
            continue

        legacy = _MIGRATION_HEADER_RE.match(line.strip())
        if legacy:
            legacy_current = _chunk(legacy.group(1), legacy.group(2))
            continue

        if legacy_current is not None:
            legacy_current.lines.append((idx, line))
            continue

        # Outside every block and every legacy header: only EF preamble is
        # expected here (transaction control, the __EFMigrationsHistory
        # bootstrap, blanks and comments).
        # Only DDL/DML is worth flagging: the bootstrap `CREATE TABLE IF NOT
        # EXISTS "__EFMigrationsHistory" (…)` spans several lines whose
        # continuations ("MigrationId" character varying(150) NOT NULL, …) carry
        # no verb, and flagging those would fail the gate on every single run.
        # The invariant that matters is narrower and exact: no executable
        # statement may escape attribution to a migration.
        if _PREAMBLE_ALLOWED_RE.match(line) or _HISTORY_TABLE_RE.search(line):
            continue
        if _DDL_VERB_RE.search(line):
            unattributed.append((idx, line))

    # An unterminated `DO $EF$` block — truncated file or a format we no longer
    # understand. Never silently discard its contents.
    if in_ef_block:
        if block_id is not None:
            _chunk(*block_id).lines.extend(pending)
        else:
            unattributed.extend(pending)

    return [by_id[i] for i in order], unattributed


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
    for line_number, raw_line in block.lines:
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
                        line_number=line_number,
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
                        line_number=line_number,
                        allowed=bool(primary_rationale),
                        rationale=primary_rationale,
                    )
                )

    return findings


# ---------------------------------------------------------------------------
# Public API
# ---------------------------------------------------------------------------


def scan(sql: str, gate_ship_date: str) -> ScanResult:
    """Scan a `dotnet ef migrations script` output for §8.2 violations.

    Migrations strictly older than `gate_ship_date` (YYYYMMDD prefix of the
    migration timestamp) are grandfathered and skipped.

    Returns the findings AND the counters that let the caller tell "scanned
    everything, found nothing" apart from "read nothing at all" (#3659).
    """
    if not re.fullmatch(r"\d{8}", gate_ship_date):
        raise ValueError(f"gate_ship_date must be YYYYMMDD; got {gate_ship_date!r}")

    blocks, unattributed = _split_into_blocks(sql)
    result = ScanResult(migrations_seen=len(blocks), unattributed=unattributed)

    for block in blocks:
        # Grandfathering: migration timestamp is YYYYMMDDhhmmss; compare the date prefix.
        if block.migration_id[:8] < gate_ship_date:
            continue
        result.migrations_scanned += 1
        result.lines_scanned += len(block.lines)
        result.findings.extend(_scan_block(block))
    return result


def scan_sql(sql: str, gate_ship_date: str) -> list[Finding]:
    """Back-compatible wrapper returning findings only."""
    return scan(sql, gate_ship_date).findings


def run_check(sql: str, gate_ship_date: str) -> int:
    """Convenience wrapper: scan + print summary + return exit code."""
    findings = scan_sql(sql, gate_ship_date)
    return _report(findings, fmt="text", out=sys.stdout)


def _report(
    findings: Iterable[Finding],
    fmt: str,
    out,
    report_path: Path | None = None,
    result: ScanResult | None = None,
) -> int:
    findings = list(findings)
    unsafe = [f for f in findings if not f.allowed]
    allowed = [f for f in findings if f.allowed]

    # Coverage counters travel with every report. Without them an empty result is
    # ambiguous, and that ambiguity is exactly what hid #3659 for three months:
    # the artifact of a PR containing a real DROP COLUMN read `{"unsafe": [],
    # "allowed": []}` and was indistinguishable from a clean run.
    coverage = {
        "migrations_seen": result.migrations_seen if result else None,
        "migrations_scanned": result.migrations_scanned if result else None,
        "lines_scanned": result.lines_scanned if result else None,
        "unattributed_statements": len(result.unattributed) if result else None,
    }

    if fmt == "json":
        payload = {
            "unsafe": [f.to_dict() for f in unsafe],
            "allowed": [f.to_dict() for f in allowed],
            "coverage": coverage,
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
            scope = (
                f"{result.migrations_scanned}/{result.migrations_seen} migration(s), "
                f"{result.lines_scanned} line(s) scanned"
                if result
                else f"{len(findings)} pattern(s) scanned"
            )
            out.write(
                f"[OK] Migration safety gate passed ({scope}; "
                f"{len(allowed)} bypassed by directive).\n"
            )

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
                    "coverage": coverage,
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
    result = scan(sql, gate_ship_date=args.gate_ship_date)

    # --- Invariant 1: scanning nothing is a failure, never a pass (#3659) -----
    # A non-empty script that yields zero migrations means the parser no longer
    # understands EF's output. Reporting that as "no unsafe pattern found" is how
    # a DROP COLUMN sailed through this gate.
    if result.migrations_seen == 0 and sql.strip():
        sys.stderr.write(
            # ASCII-only: Windows cp1252 stderr cannot encode ellipsis / em-dash,
            # and a mojibake in the one message that explains a hard failure is
            # the worst possible place for it.
            "error: the SQL contains no recognisable migration.\n"
            f"       Read {len(sql.splitlines())} line(s) and attributed none of them.\n"
            "       Expected `DO $EF$ ... \"MigrationId\" = '<14 digits>_<name>' ... END $EF$;`\n"
            "       blocks from `dotnet ef migrations script --idempotent`.\n"
            "       If EF changed its output shape, fix the parser -- do NOT relax\n"
            "       this check: passing here means nothing was verified (#3659).\n"
        )
        return 2

    # --- Invariant 2: no executable statement may escape attribution ----------
    if result.unattributed:
        sys.stderr.write(
            f"error: {len(result.unattributed)} statement(s) belong to no migration "
            "and were therefore never checked:\n"
        )
        for line_no, text in result.unattributed[:10]:
            sys.stderr.write(f"       line {line_no}: {text.strip()}\n")
        if len(result.unattributed) > 10:
            sys.stderr.write(f"       ... and {len(result.unattributed) - 10} more\n")
        sys.stderr.write(
            "       Unscanned SQL is a parser gap, not a pass. Fix the parser (#3659).\n"
        )
        return 2

    return _report(
        result.findings,
        fmt=args.format,
        out=sys.stdout,
        report_path=args.report,
        result=result,
    )


if __name__ == "__main__":
    sys.exit(main())
