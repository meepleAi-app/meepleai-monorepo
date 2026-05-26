"""Partition findings into main report vs manual review queue, and identify issue-eligible."""
from __future__ import annotations

from scripts.v2_audit.finding import Finding


def partition_by_confidence(findings: list[Finding]) -> tuple[list[Finding], list[Finding]]:
    """Return (main_report_findings, manual_review_queue)."""
    main = [f for f in findings if f.confidence in ("high", "medium")]
    queue = [f for f in findings if f.confidence == "low"]
    return main, queue


def eligible_for_issue(finding: Finding) -> bool:
    """Critical or Important + HIGH/MEDIUM confidence → eligible for GitHub issue."""
    return (
        finding.severity in ("critical", "important")
        and finding.confidence in ("high", "medium")
    )
