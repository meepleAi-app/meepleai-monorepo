"""CLI: orchestrate the full audit pipeline.

Usage:
  python -m scripts.v2_audit.run [--dry-run] [--limit N]

Phase A: parse matrix, validate paths.
Phase B: per row, run 4 dimension audits.
Phase C: write report + input snapshot.
"""
from __future__ import annotations
from pathlib import Path
import argparse
import json
import sys

from scripts.v2_audit.matrix_parser import parse_matrix
from scripts.v2_audit.component_inspector import inspect_component
from scripts.v2_audit.mockup_inspector import inspect_mockup
from scripts.v2_audit.nav_dimension import audit_nav
from scripts.v2_audit.structural_dimension import audit_structural
from scripts.v2_audit.token_dimension import audit_tokens
from scripts.v2_audit.props_dimension import audit_props
from scripts.v2_audit.report_builder import build_report

REPO_ROOT = Path(__file__).resolve().parents[2]
MATRIX_PATH = REPO_ROOT / "docs" / "for-developers" / "frontend" / "v2-migration-matrix.md"
MOCKUPS_DIR = REPO_ROOT / "admin-mockups" / "design_files"
REPORT_PATH = REPO_ROOT / "docs" / "superpowers" / "specs" / "audit-report.md"
INPUT_SNAPSHOT_PATH = REPO_ROOT / "docs" / "superpowers" / "specs" / "audit-input-validated.json"


def run_audit(limit: int | None = None, dry_run: bool = False) -> dict:
    rows = list(parse_matrix(MATRIX_PATH, status_filter="done"))
    if limit:
        rows = rows[:limit]
    print(f"Phase A: {len(rows)} done rows to audit")

    matrix_drift = []
    valid_rows = []

    for row in rows:
        comp_path = REPO_ROOT / row.path
        mockup_path = MOCKUPS_DIR / row.mockup

        if not comp_path.exists():
            matrix_drift.append((row.component, row.path))
            continue
        if not mockup_path.exists():
            mockup_jsx = mockup_path.with_suffix(".jsx")
            if mockup_jsx.exists():
                mockup_path = mockup_jsx
            else:
                matrix_drift.append((row.component, str(mockup_path)))
                continue

        valid_rows.append((row, comp_path, mockup_path))

    print(f"Phase A: {len(matrix_drift)} matrix drift entries; {len(valid_rows)} ready for audit")

    if dry_run:
        print("DRY-RUN: would audit", len(valid_rows), "components")
        return {"dry_run": True, "valid_rows": len(valid_rows), "drift": len(matrix_drift)}

    print("Phase B: running 4 dimension audits per row")
    all_findings = []
    for row, comp_path, mockup_path in valid_rows:
        comp = inspect_component(comp_path)
        mock = inspect_mockup(mockup_path)
        comp_text = comp_path.read_text(encoding="utf-8")
        mockup_text = mockup_path.read_text(encoding="utf-8")

        all_findings.extend(audit_nav(comp, mock, row.route))
        all_findings.extend(audit_structural(comp, mock, row.route))
        all_findings.extend(audit_tokens(comp, comp_text, row.route))
        all_findings.extend(audit_props(comp, mockup_text, row.route))

    INPUT_SNAPSHOT_PATH.parent.mkdir(parents=True, exist_ok=True)
    with INPUT_SNAPSHOT_PATH.open("w", encoding="utf-8", newline="\n") as snap:
        json.dump(
            {
                "matrix_path": str(MATRIX_PATH.relative_to(REPO_ROOT)),
                "total_rows": len(rows),
                "valid_rows": len(valid_rows),
                "drift": [{"component": c, "path": p} for c, p in matrix_drift],
                "audited_components": [
                    {"component": r.component, "route": r.route, "path": r.path}
                    for r, _, _ in valid_rows
                ],
            },
            snap,
            indent=2,
        )

    stats = build_report(
        all_findings,
        out_path=REPORT_PATH,
        total_components=len(valid_rows),
        matrix_drift=matrix_drift,
    )
    print(f"Phase C: report written to {REPORT_PATH}")
    print(f"Stats: {stats}")
    return stats


def main(argv=None):
    p = argparse.ArgumentParser()
    p.add_argument("--limit", type=int, help="Audit only first N rows")
    p.add_argument("--dry-run", action="store_true", help="Phase A only")
    args = p.parse_args(argv)
    run_audit(limit=args.limit, dry_run=args.dry_run)
    return 0


if __name__ == "__main__":
    sys.exit(main())
