from pathlib import Path
from scripts.v2_audit.report_builder import build_report
from scripts.v2_audit.finding import Finding, Severity, Confidence, Dimension


def test_report_has_sections(tmp_path):
    findings = [
        Finding(
            dimension=Dimension.NAV,
            severity=Severity.CRITICAL,
            confidence=Confidence.HIGH,
            component="GameDetailHero.tsx",
            route="/games/[id]",
            description="Missing Link to game-onboarding",
            evidence={"line": 42},
        ),
        Finding(
            dimension=Dimension.STRUCTURAL,
            severity=Severity.IMPORTANT,
            confidence=Confidence.LOW,
            component="GamesGrid.tsx",
            route="/games",
            description="Missing <main>",
            evidence={},
        ),
    ]
    out = tmp_path / "report.md"
    stats = build_report(findings, out_path=out, total_components=108, matrix_drift=[])
    content = out.read_text(encoding="utf-8")
    assert "# V2 Component Audit Report" in content
    assert "## Findings by Route" in content
    assert "## Manual Review Queue" in content
    assert "/games/[id]" in content
    assert "Missing Link to game-onboarding" in content
    assert stats["total_findings"] == 2


def test_report_groups_by_route(tmp_path):
    findings = [
        Finding(Dimension.NAV, Severity.CRITICAL, Confidence.HIGH, "A.tsx", "/a", "d1", {}),
        Finding(Dimension.NAV, Severity.IMPORTANT, Confidence.HIGH, "B.tsx", "/a", "d2", {}),
        Finding(Dimension.NAV, Severity.MINOR, Confidence.MEDIUM, "C.tsx", "/b", "d3", {}),
    ]
    out = tmp_path / "report.md"
    build_report(findings, out_path=out, total_components=3, matrix_drift=[])
    content = out.read_text(encoding="utf-8")
    assert "### Route `/a`" in content
    assert "### Route `/b`" in content


def test_report_contains_matrix_drift(tmp_path):
    out = tmp_path / "report.md"
    build_report(
        [],
        out_path=out,
        total_components=10,
        matrix_drift=[("MissingComponent", "apps/web/src/missing.tsx")],
    )
    content = out.read_text(encoding="utf-8")
    assert "Matrix Drift" in content
    assert "MissingComponent" in content
