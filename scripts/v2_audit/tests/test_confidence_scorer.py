from scripts.v2_audit.confidence_scorer import partition_by_confidence, eligible_for_issue
from scripts.v2_audit.finding import Finding, Severity, Confidence, Dimension


def _f(severity, confidence):
    return Finding(
        dimension=Dimension.NAV,
        severity=severity,
        confidence=confidence,
        component="X.tsx",
        route="/x",
        description="d",
        evidence={},
    )


def test_partitions_high_confidence_to_main():
    findings = [
        _f(Severity.CRITICAL, Confidence.HIGH),
        _f(Severity.IMPORTANT, Confidence.MEDIUM),
        _f(Severity.MINOR, Confidence.LOW),
    ]
    main, queue = partition_by_confidence(findings)
    assert len(main) == 2  # high + medium
    assert len(queue) == 1  # low


def test_eligibility_for_issue_creation():
    assert eligible_for_issue(_f(Severity.CRITICAL, Confidence.HIGH))
    assert eligible_for_issue(_f(Severity.IMPORTANT, Confidence.HIGH))
    assert not eligible_for_issue(_f(Severity.MINOR, Confidence.HIGH))  # minor never auto-issues
    assert not eligible_for_issue(_f(Severity.CRITICAL, Confidence.LOW))  # low always manual
