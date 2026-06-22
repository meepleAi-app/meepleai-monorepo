"""Builds audit-report.md from a flat findings list."""
from __future__ import annotations
from collections import defaultdict
from pathlib import Path

from scripts.v2_audit.finding import Finding
from scripts.v2_audit.confidence_scorer import partition_by_confidence


def build_report(
    findings: list[Finding],
    out_path: Path,
    total_components: int,
    matrix_drift: list[tuple[str, str]],
) -> dict:
    main, queue = partition_by_confidence(findings)
    stats = {
        "total_findings": len(findings),
        "main_report_count": len(main),
        "manual_queue_count": len(queue),
        "critical": sum(1 for f in main if f.severity == "critical"),
        "important": sum(1 for f in main if f.severity == "important"),
        "minor": sum(1 for f in main if f.severity == "minor"),
    }

    out_path.parent.mkdir(parents=True, exist_ok=True)
    with out_path.open("w", encoding="utf-8", newline="\n") as out:
        out.write("# V2 Component Audit Report\n\n")
        out.write(f"**Total components audited:** {total_components}\n")
        out.write(f"**Total findings:** {stats['total_findings']}\n")
        out.write(f"**Main report:** {stats['main_report_count']} "
                  f"(Critical: {stats['critical']} · Important: {stats['important']} · Minor: {stats['minor']})\n")
        out.write(f"**Manual review queue:** {stats['manual_queue_count']}\n\n")

        if matrix_drift:
            out.write("## Matrix Drift\n\n")
            out.write("Rows marked `done` but file missing:\n\n")
            for comp, path in matrix_drift:
                out.write(f"- `{comp}` — expected at `{path}`\n")
            out.write("\n")

        out.write("## Findings by Route\n\n")
        by_route: dict[str, list[Finding]] = defaultdict(list)
        for f in main:
            by_route[f.route].append(f)
        for route in sorted(by_route):
            findings_in_route = by_route[route]
            _write_route_section(out, route, findings_in_route)

        out.write("\n## Manual Review Queue\n\n")
        out.write("LOW-confidence findings — review and promote/demote manually.\n\n")
        if not queue:
            out.write("_None._\n")
        else:
            by_route_q: dict[str, list[Finding]] = defaultdict(list)
            for f in queue:
                by_route_q[f.route].append(f)
            for route in sorted(by_route_q):
                out.write(f"### Route `{route}`\n\n")
                for f in by_route_q[route]:
                    out.write(f"- [{f.severity.upper()}] `{f.component}` ({f.dimension}): "
                              f"{f.description}\n")
                out.write("\n")

    return stats


def _write_route_section(out, route: str, findings: list[Finding]):
    out.write(f"### Route `{route}`\n\n")
    severity_weight = {"critical": 3, "important": 2, "minor": 1}
    findings_sorted = sorted(findings, key=lambda f: -severity_weight[f.severity])
    for f in findings_sorted:
        out.write(f"- **[{f.severity.upper()}]** `{f.component}` ({f.dimension}): "
                  f"{f.description}\n")
        if f.evidence:
            for k, v in f.evidence.items():
                out.write(f"  - `{k}`: `{v}`\n")
    out.write("\n")
