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


if __name__ == "__main__":
    unittest.main()
