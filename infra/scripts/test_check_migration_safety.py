#!/usr/bin/env python3
"""Tests for check-migration-safety.py — 10+ scenarios per Adzic spec-panel (refs #1087)."""

import importlib.util
import sys
import unittest
from pathlib import Path

# Import the script as a module (hyphenated filename → use importlib).
# Must register in sys.modules before exec_module so @dataclass can resolve cls.__module__.
_SCRIPT_PATH = Path(__file__).resolve().parent / "check-migration-safety.py"
_spec = importlib.util.spec_from_file_location("check_migration_safety", _SCRIPT_PATH)
mod = importlib.util.module_from_spec(_spec)
sys.modules["check_migration_safety"] = mod
_spec.loader.exec_module(mod)

GATE_SHIP_DATE = "20260518"  # Migrations strictly older are grandfathered.


def _block(migration_id: str, body: str) -> str:
    """EF-Core-style SQL block: header comment + body inside a transaction."""
    return (
        f"-- Migration: {migration_id}_TestMigration\n"
        f"START TRANSACTION;\n"
        f"{body}\n"
        f"COMMIT;\n"
    )


class ParserPositiveTests(unittest.TestCase):
    """Patterns that MUST be flagged as unsafe."""

    def test_drop_column_is_flagged(self):
        sql = _block("20260601120000", 'ALTER TABLE "users" DROP COLUMN "email";')
        findings = mod.scan_sql(sql, gate_ship_date=GATE_SHIP_DATE)
        self.assertTrue(any(f.pattern == "DROP_COLUMN" for f in findings))

    def test_drop_table_is_flagged(self):
        sql = _block("20260601120000", 'DROP TABLE "legacy_users";')
        findings = mod.scan_sql(sql, gate_ship_date=GATE_SHIP_DATE)
        self.assertTrue(any(f.pattern == "DROP_TABLE" for f in findings))

    def test_alter_column_type_is_flagged(self):
        sql = _block("20260601120000", 'ALTER TABLE "users" ALTER COLUMN "age" TYPE smallint;')
        findings = mod.scan_sql(sql, gate_ship_date=GATE_SHIP_DATE)
        self.assertTrue(any(f.pattern == "ALTER_COLUMN_TYPE" for f in findings))

    def test_rename_column_is_flagged(self):
        sql = _block("20260601120000", 'ALTER TABLE "users" RENAME COLUMN "email" TO "email_addr";')
        findings = mod.scan_sql(sql, gate_ship_date=GATE_SHIP_DATE)
        self.assertTrue(any(f.pattern == "RENAME_COLUMN" for f in findings))

    def test_rename_table_is_flagged(self):
        sql = _block("20260601120000", 'ALTER TABLE "users" RENAME TO "users_v2";')
        findings = mod.scan_sql(sql, gate_ship_date=GATE_SHIP_DATE)
        self.assertTrue(any(f.pattern == "RENAME_TABLE" for f in findings))

    def test_add_column_not_null_without_default_is_flagged(self):
        sql = _block("20260601120000", 'ALTER TABLE "users" ADD COLUMN "status" varchar(20) NOT NULL;')
        findings = mod.scan_sql(sql, gate_ship_date=GATE_SHIP_DATE)
        self.assertTrue(any(f.pattern == "ADD_COLUMN_NOT_NULL_NO_DEFAULT" for f in findings))


class ParserNegativeTests(unittest.TestCase):
    """Patterns that MUST NOT be flagged."""

    def test_add_column_nullable_is_safe(self):
        sql = _block("20260601120000", 'ALTER TABLE "users" ADD COLUMN "status" varchar(20) NULL;')
        findings = mod.scan_sql(sql, gate_ship_date=GATE_SHIP_DATE)
        self.assertEqual([], [f for f in findings if not f.allowed])

    def test_add_column_not_null_with_default_is_safe(self):
        sql = _block(
            "20260601120000",
            "ALTER TABLE \"users\" ADD COLUMN \"status\" varchar(20) NOT NULL DEFAULT 'active';",
        )
        findings = mod.scan_sql(sql, gate_ship_date=GATE_SHIP_DATE)
        self.assertEqual([], [f for f in findings if not f.allowed])

    def test_create_table_is_safe(self):
        sql = _block(
            "20260601120000",
            'CREATE TABLE "new_table" ("id" uuid NOT NULL, CONSTRAINT "pk_new_table" PRIMARY KEY ("id"));',
        )
        findings = mod.scan_sql(sql, gate_ship_date=GATE_SHIP_DATE)
        self.assertEqual([], [f for f in findings if not f.allowed])

    def test_data_only_sql_is_safe(self):
        sql = _block("20260601120000", "UPDATE \"users\" SET \"status\" = 'active' WHERE \"status\" IS NULL;")
        findings = mod.scan_sql(sql, gate_ship_date=GATE_SHIP_DATE)
        self.assertEqual([], [f for f in findings if not f.allowed])

    def test_create_index_concurrently_is_safe(self):
        sql = _block("20260601120000", 'CREATE INDEX CONCURRENTLY "ix_users_email" ON "users" ("email");')
        findings = mod.scan_sql(sql, gate_ship_date=GATE_SHIP_DATE)
        self.assertEqual([], [f for f in findings if not f.allowed])


class AllowDirectiveTests(unittest.TestCase):
    """`-- safe: <rationale>` directive bypasses with audit trail."""

    def test_drop_column_with_safe_directive_is_allowed(self):
        sql = _block(
            "20260601120000",
            "-- safe: drop legacy email column after 7-day soak (§8.3 expand-contract)\n"
            'ALTER TABLE "users" DROP COLUMN "email";',
        )
        findings = mod.scan_sql(sql, gate_ship_date=GATE_SHIP_DATE)
        unsafe = [f for f in findings if not f.allowed]
        self.assertEqual([], unsafe, "directive with rationale should bypass")
        allowed = [f for f in findings if f.allowed]
        self.assertEqual(1, len(allowed))
        self.assertIn("drop legacy email column", allowed[0].rationale)

    def test_empty_rationale_is_rejected(self):
        sql = _block(
            "20260601120000",
            "-- safe:\n"
            'ALTER TABLE "users" DROP COLUMN "email";',
        )
        findings = mod.scan_sql(sql, gate_ship_date=GATE_SHIP_DATE)
        unsafe = [f for f in findings if not f.allowed]
        self.assertEqual(1, len(unsafe), "empty rationale must not bypass")

    def test_whitespace_only_rationale_is_rejected(self):
        sql = _block(
            "20260601120000",
            "-- safe:    \n"
            'ALTER TABLE "users" DROP COLUMN "email";',
        )
        findings = mod.scan_sql(sql, gate_ship_date=GATE_SHIP_DATE)
        unsafe = [f for f in findings if not f.allowed]
        self.assertEqual(1, len(unsafe), "whitespace-only rationale must not bypass")

    def test_directive_in_different_block_does_not_bypass(self):
        """Directive scope is per-migration: a directive in migration A must NOT cover unsafe SQL in migration B."""
        sql = (
            _block(
                "20260601120000",
                "-- safe: this is a legitimate drop in migration A\n"
                'ALTER TABLE "table_a" DROP COLUMN "x";',
            )
            + _block(
                "20260601130000",
                'ALTER TABLE "table_b" DROP COLUMN "y";',
            )
        )
        findings = mod.scan_sql(sql, gate_ship_date=GATE_SHIP_DATE)
        unsafe = [f for f in findings if not f.allowed]
        self.assertEqual(1, len(unsafe))
        self.assertEqual("20260601130000", unsafe[0].migration_id)


class EdgeCaseTests(unittest.TestCase):

    def test_grandfathered_migration_is_skipped(self):
        """Migrations strictly older than the gate ship date are grandfathered."""
        sql = _block("20260101120000", 'ALTER TABLE "users" DROP COLUMN "email";')
        findings = mod.scan_sql(sql, gate_ship_date=GATE_SHIP_DATE)
        self.assertEqual([], findings)

    def test_gate_ship_date_migration_is_scanned(self):
        """A migration timestamped exactly at the gate ship date is in scope (gate is inclusive)."""
        sql = _block(f"{GATE_SHIP_DATE}120000", 'ALTER TABLE "users" DROP COLUMN "email";')
        findings = mod.scan_sql(sql, gate_ship_date=GATE_SHIP_DATE)
        self.assertEqual(1, len(findings))

    def test_multi_statement_block_reports_all_unsafe(self):
        sql = _block(
            "20260601120000",
            'ALTER TABLE "users" DROP COLUMN "x";\n'
            'ALTER TABLE "orders" DROP COLUMN "y";',
        )
        findings = mod.scan_sql(sql, gate_ship_date=GATE_SHIP_DATE)
        unsafe = [f for f in findings if not f.allowed]
        self.assertEqual(2, len(unsafe))

    def test_pattern_in_string_literal_is_not_flagged(self):
        """`DROP COLUMN` inside a SQL string literal must not trigger the scanner."""
        sql = _block(
            "20260601120000",
            "INSERT INTO \"audit_log\" (\"event\") VALUES ('legacy DROP COLUMN cleanup');",
        )
        findings = mod.scan_sql(sql, gate_ship_date=GATE_SHIP_DATE)
        self.assertEqual([], [f for f in findings if not f.allowed])

    def test_case_insensitive_match(self):
        sql = _block("20260601120000", 'alter table "users" drop column "email";')
        findings = mod.scan_sql(sql, gate_ship_date=GATE_SHIP_DATE)
        self.assertEqual(1, len(findings))


class CliExitCodeTests(unittest.TestCase):
    """End-to-end CLI behaviour: exit 0 on clean, exit 1 on unsafe."""

    def test_main_returns_zero_when_no_unsafe_pattern(self):
        sql = _block("20260601120000", 'ALTER TABLE "users" ADD COLUMN "status" varchar(20) NULL;')
        rc = mod.run_check(sql, gate_ship_date=GATE_SHIP_DATE)
        self.assertEqual(0, rc)

    def test_main_returns_one_when_unsafe_pattern(self):
        sql = _block("20260601120000", 'ALTER TABLE "users" DROP COLUMN "email";')
        rc = mod.run_check(sql, gate_ship_date=GATE_SHIP_DATE)
        self.assertEqual(1, rc)

    def test_main_returns_zero_when_unsafe_pattern_is_directived(self):
        sql = _block(
            "20260601120000",
            "-- safe: drop legacy column after soak\n"
            'ALTER TABLE "users" DROP COLUMN "email";',
        )
        rc = mod.run_check(sql, gate_ship_date=GATE_SHIP_DATE)
        self.assertEqual(0, rc)


# ---------------------------------------------------------------------------
# Real `dotnet ef migrations script --idempotent` output (#3659)
# ---------------------------------------------------------------------------
#
# Everything above this line uses `_block()`, whose docstring claims to produce
# an "EF-Core-style SQL block" by prepending `-- Migration: <id>`. EF has never
# emitted that header. The parser was green against a format that does not
# exist, which is why the gate ran for three months without reading a single
# statement -- including on a PR that dropped a column.
#
# The excerpt below is copied VERBATIM from the tail of
# `dotnet ef migrations script --idempotent` run against this repo on
# 2026-08-11. Do not tidy it: its value is being byte-for-byte real.
#
# Note the shape it proves: one `migrationBuilder` call per `DO $EF$` block, so
# the `-- safe:` directive and the DROP COLUMN it authorises sit in DIFFERENT
# blocks joined only by their MigrationId. A parser grouping per block would
# lose that association.
REAL_EF_TAIL = '''\
DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260811130532_PdfDocumentXminConcurrency') THEN
    -- safe: drop dead bytea concurrency column, replaced by the xmin system column
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260811130532_PdfDocumentXminConcurrency') THEN
    ALTER TABLE pdf_documents DROP COLUMN "RowVersion";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260811130532_PdfDocumentXminConcurrency') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260811130532_PdfDocumentXminConcurrency', '9.0.11');
    END IF;
END $EF$;
COMMIT;
'''

REAL_EF_PREAMBLE = '''\
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

'''


class RealEfFormatTests(unittest.TestCase):
    """The regression suite that would have caught #3659 on day one."""

    def test_real_ef_output_contains_no_legacy_header(self):
        """Pins the fact the old parser depended on and never checked.

        The pre-#3659 parser split on `-- Migration: <id>` alone. This asserts
        that real EF output contains no such header -- i.e. that a header-only
        parser reads ZERO statements from it. It is the reason every other test
        in this class fails against the old implementation, and it is worth
        stating separately because it is the assumption, not the symptom.
        """
        self.assertNotIn("-- Migration:", REAL_EF_PREAMBLE + REAL_EF_TAIL)

    def test_real_ef_output_is_recognised_at_all(self):
        """The single assertion whose absence let the gate run blind."""
        result = mod.scan(REAL_EF_PREAMBLE + REAL_EF_TAIL, gate_ship_date=GATE_SHIP_DATE)
        self.assertGreater(
            result.migrations_seen, 0,
            "real EF output must yield at least one migration; if this fails the "
            "gate is scanning nothing and every result below is vacuous",
        )

    def test_drop_column_in_real_format_is_found(self):
        findings = mod.scan_sql(REAL_EF_PREAMBLE + REAL_EF_TAIL, gate_ship_date=GATE_SHIP_DATE)
        self.assertTrue(any(f.pattern == "DROP_COLUMN" for f in findings))

    def test_directive_in_a_different_block_still_authorises(self):
        """Cross-block association: directive and statement share only the id."""
        findings = mod.scan_sql(REAL_EF_PREAMBLE + REAL_EF_TAIL, gate_ship_date=GATE_SHIP_DATE)
        drop = next(f for f in findings if f.pattern == "DROP_COLUMN")
        self.assertTrue(drop.allowed)
        self.assertIn("xmin", drop.rationale)

    def test_many_blocks_collapse_into_one_migration(self):
        """EF emits one block per call: 3 blocks here, but a single migration."""
        result = mod.scan(REAL_EF_PREAMBLE + REAL_EF_TAIL, gate_ship_date=GATE_SHIP_DATE)
        self.assertEqual(1, result.migrations_seen)

    def test_line_numbers_are_absolute_in_the_file(self):
        sql = REAL_EF_PREAMBLE + REAL_EF_TAIL
        findings = mod.scan_sql(sql, gate_ship_date=GATE_SHIP_DATE)
        drop = next(f for f in findings if f.pattern == "DROP_COLUMN")
        actual = sql.splitlines()[drop.line_number - 1]
        self.assertIn("DROP COLUMN", actual)

    def test_history_bootstrap_is_not_reported_as_unattributed(self):
        """Its continuation lines carry no verb; flagging them would fail every run."""
        result = mod.scan(REAL_EF_PREAMBLE + REAL_EF_TAIL, gate_ship_date=GATE_SHIP_DATE)
        self.assertEqual([], result.unattributed)


class NothingScannedTests(unittest.TestCase):
    """Scanning nothing must be an error, never a pass (#3659)."""

    def test_unknown_format_yields_zero_migrations(self):
        result = mod.scan(
            'ALTER TABLE users DROP COLUMN email;\n', gate_ship_date=GATE_SHIP_DATE
        )
        self.assertEqual(0, result.migrations_seen)

    def test_ef_block_without_recognisable_guard_is_unattributed(self):
        """Simulates EF changing its output: the SQL must not vanish silently."""
        sql = (
            "DO $EF$\nBEGIN\n"
            '    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE migration = 42) THEN\n'
            '    DROP TABLE "victims";\n'
            "    END IF;\nEND $EF$;\n"
        )
        result = mod.scan(sql, gate_ship_date=GATE_SHIP_DATE)
        self.assertEqual(0, result.migrations_seen)
        self.assertTrue(
            any("DROP TABLE" in text for _, text in result.unattributed),
            "SQL inside an unrecognised block must surface as unattributed",
        )

    def test_ddl_outside_any_block_is_unattributed(self):
        sql = REAL_EF_PREAMBLE + REAL_EF_TAIL + 'DROP TABLE "sneaky_orphan";\n'
        result = mod.scan(sql, gate_ship_date=GATE_SHIP_DATE)
        self.assertTrue(any("sneaky_orphan" in text for _, text in result.unattributed))

    def test_unterminated_block_is_not_discarded(self):
        sql = (
            "DO $EF$\nBEGIN\n"
            '    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = \'20260601120000_Trunc\') THEN\n'
            '    ALTER TABLE "users" DROP COLUMN "email";\n'
        )
        findings = mod.scan_sql(sql, gate_ship_date=GATE_SHIP_DATE)
        self.assertTrue(any(f.pattern == "DROP_COLUMN" for f in findings))


class CoverageReportTests(unittest.TestCase):
    """`{"unsafe": [], "allowed": []}` must never again be ambiguous (#3659)."""

    def test_coverage_counters_distinguish_clean_from_unread(self):
        clean = mod.scan(REAL_EF_PREAMBLE + REAL_EF_TAIL, gate_ship_date=GATE_SHIP_DATE)
        unread = mod.scan("SELECT 1;\n", gate_ship_date=GATE_SHIP_DATE)
        self.assertGreater(clean.lines_scanned, 0)
        self.assertEqual(0, unread.lines_scanned)
        self.assertNotEqual(clean.migrations_seen, unread.migrations_seen)


if __name__ == "__main__":
    unittest.main()
