from scripts.v2_audit.issue_creator import build_issue_body, build_issue_title
from scripts.v2_audit.finding import Finding, Severity, Confidence, Dimension


def test_title_includes_route_and_count():
    findings = [
        Finding(Dimension.NAV, Severity.CRITICAL, Confidence.HIGH, "X.tsx", "/games/[id]", "d1", {}),
        Finding(Dimension.NAV, Severity.IMPORTANT, Confidence.HIGH, "Y.tsx", "/games/[id]", "d2", {}),
    ]
    title = build_issue_title("/games/[id]", findings)
    assert "/games/[id]" in title
    assert "2" in title
    assert "[audit]" in title.lower()


def test_body_groups_by_severity():
    findings = [
        Finding(Dimension.NAV, Severity.CRITICAL, Confidence.HIGH, "X.tsx", "/games", "d1", {"line": 42}),
        Finding(Dimension.STRUCTURAL, Severity.IMPORTANT, Confidence.HIGH, "X.tsx", "/games", "d2", {}),
        Finding(Dimension.TOKENS, Severity.IMPORTANT, Confidence.MEDIUM, "Y.tsx", "/games", "d3", {}),
    ]
    body = build_issue_body(findings, mockups=["sp4-games-index.html"])
    assert "Critical (1)" in body
    assert "Important (2)" in body
    assert "X.tsx" in body
    assert "d1" in body
    assert "sp4-games-index.html" in body


def test_body_excludes_minor_and_low_confidence():
    findings = [
        Finding(Dimension.NAV, Severity.CRITICAL, Confidence.HIGH, "X.tsx", "/x", "kept", {}),
        Finding(Dimension.NAV, Severity.MINOR, Confidence.HIGH, "X.tsx", "/x", "dropped_minor", {}),
        Finding(Dimension.NAV, Severity.CRITICAL, Confidence.LOW, "X.tsx", "/x", "dropped_low", {}),
    ]
    body = build_issue_body(findings, mockups=["x.html"])
    assert "kept" in body
    assert "dropped_minor" not in body
    assert "dropped_low" not in body
