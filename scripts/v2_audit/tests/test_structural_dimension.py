import pytest
from pathlib import Path
from scripts.v2_audit.structural_dimension import audit_structural
from scripts.v2_audit.component_inspector import ComponentSnapshot
from scripts.v2_audit.mockup_inspector import MockupSnapshot


@pytest.fixture(autouse=True)
def _clear_route_tree_h1_cache():
    yield
    from scripts.v2_audit import structural_dimension
    fn = getattr(structural_dimension, "_route_tree_has_h1", None)
    if fn is not None and hasattr(fn, "cache_clear"):
        fn.cache_clear()


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


def test_route_tree_has_h1_finds_h1_in_page_tsx(tmp_path, monkeypatch):
    """When route's page.tsx contains <h1>, returns True."""
    from scripts.v2_audit import structural_dimension, nav_dimension

    app_dir = tmp_path / "apps" / "web" / "src" / "app"
    route_dir = app_dir / "(authenticated)" / "h1-route"
    route_dir.mkdir(parents=True)
    (route_dir / "page.tsx").write_text(
        'export default function Page() { return <h1>Title</h1>; }\n',
        encoding="utf-8",
    )

    monkeypatch.setattr(
        nav_dimension, "_REPO_ROOT_OVERRIDE", tmp_path, raising=False
    )
    structural_dimension._route_tree_has_h1.cache_clear()

    assert structural_dimension._route_tree_has_h1("/h1-route") is True


def test_route_tree_has_h1_missing_route_returns_false(tmp_path, monkeypatch):
    """When route folder doesn't exist, returns False."""
    from scripts.v2_audit import structural_dimension, nav_dimension

    monkeypatch.setattr(
        nav_dimension, "_REPO_ROOT_OVERRIDE", tmp_path, raising=False
    )
    structural_dimension._route_tree_has_h1.cache_clear()

    assert structural_dimension._route_tree_has_h1("/no-such-route") is False


def test_audit_structural_skips_h1_finding_when_parent_has_h1(tmp_path, monkeypatch):
    """When parent route has h1, sub-component's 'missing h1' finding is suppressed."""
    from scripts.v2_audit import structural_dimension
    from scripts.v2_audit.component_inspector import ComponentSnapshot
    from scripts.v2_audit.mockup_inspector import MockupSnapshot
    from pathlib import Path

    monkeypatch.setattr(
        structural_dimension, "_route_tree_has_h1", lambda r: True
    )

    comp = ComponentSnapshot(
        path=Path("Sub.tsx"),
        landmarks={"section"},
        headings={"h3"},
    )
    mock = MockupSnapshot(
        path=Path("y.html"),
        landmarks={"section"},
        headings={"h1", "h3"},
    )

    findings = list(structural_dimension.audit_structural(comp, mock, route="/some-route"))
    h1_findings = [f for f in findings if "<h1>" in f.description]
    assert len(h1_findings) == 0, f"Expected 0 h1 findings (parent has h1), got: {[f.description for f in h1_findings]}"
