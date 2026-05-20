from pathlib import Path
from scripts.v2_audit.structural_dimension import audit_structural
from scripts.v2_audit.component_inspector import ComponentSnapshot
from scripts.v2_audit.mockup_inspector import MockupSnapshot


def test_missing_landmark_is_critical():
    comp = ComponentSnapshot(path=Path("X.tsx"), landmarks={"section"})
    mock = MockupSnapshot(
        path=Path("y.html"),
        landmarks={"section", "header", "main"},
    )
    findings = list(audit_structural(comp, mock, route="/x"))
    critical = [f for f in findings if f.severity == "critical"]
    assert len(critical) >= 1


def test_missing_h1_is_important():
    comp = ComponentSnapshot(path=Path("X.tsx"), landmarks={"section"}, headings={"h2"})
    mock = MockupSnapshot(
        path=Path("y.html"),
        landmarks={"section"},
        headings={"h1", "h2"},
    )
    findings = list(audit_structural(comp, mock, route="/x"))
    assert any("h1" in f.description.lower() for f in findings)


def test_low_confidence_when_high_divergence():
    """Component has zero landmarks; mockup has many → all findings LOW confidence."""
    comp = ComponentSnapshot(path=Path("X.tsx"), landmarks=set())
    mock = MockupSnapshot(
        path=Path("y.html"),
        landmarks={"header", "main", "section", "aside", "footer"},
    )
    findings = list(audit_structural(comp, mock, route="/x"))
    assert all(f.confidence == "low" for f in findings)


def test_no_findings_when_match():
    comp = ComponentSnapshot(path=Path("X.tsx"), landmarks={"section", "header"}, headings={"h1"})
    mock = MockupSnapshot(path=Path("y.html"), landmarks={"section", "header"}, headings={"h1"})
    findings = list(audit_structural(comp, mock, route="/x"))
    assert len(findings) == 0
