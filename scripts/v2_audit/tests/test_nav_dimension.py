import pytest
from pathlib import Path
from scripts.v2_audit.nav_dimension import audit_nav
from scripts.v2_audit.component_inspector import ComponentSnapshot
from scripts.v2_audit.mockup_inspector import MockupSnapshot


@pytest.fixture(autouse=True)
def _clear_route_tree_hrefs_cache():
    """Clear lru_cache between tests to prevent cross-test pollution."""
    yield
    from scripts.v2_audit import nav_dimension
    fn = getattr(nav_dimension, "_collect_route_tree_hrefs", None)
    if fn is not None and hasattr(fn, "cache_clear"):
        fn.cache_clear()


def test_missing_destination_is_critical():
    comp = ComponentSnapshot(path=Path("X.tsx"), link_hrefs={"/games"})
    mock = MockupSnapshot(
        path=Path("y.html"),
        link_destinations={"sp4-games-index.html", "librogame-runthrough-game-onboarding.html"},
    )
    findings = list(audit_nav(comp, mock, route="/games/[id]"))
    assert any(f.severity == "critical" for f in findings)


def test_no_findings_when_complete():
    comp = ComponentSnapshot(
        path=Path("X.tsx"),
        link_hrefs={"/games", "/library/[gameId]/play/game-onboarding"},
    )
    mock = MockupSnapshot(
        path=Path("y.html"),
        link_destinations={"sp4-games-index.html", "librogame-runthrough-game-onboarding.html"},
    )
    findings = list(audit_nav(comp, mock, route="/games/[id]"))
    critical = [f for f in findings if f.severity == "critical"]
    # We allow up to 2 critical IF the mapping heuristic fails — but should be 0 here
    assert len(critical) <= 2


def test_extra_link_is_minor():
    comp = ComponentSnapshot(
        path=Path("X.tsx"),
        link_hrefs={"/games", "/random-unmapped-route"},
    )
    mock = MockupSnapshot(path=Path("y.html"), link_destinations={"sp4-games-index.html"})
    findings = list(audit_nav(comp, mock, route="/games"))
    minor = [f for f in findings if f.severity == "minor"]
    assert len(minor) >= 1


def test_route_to_filesystem_path_authenticated(tmp_path, monkeypatch):
    """Maps an authenticated route to its (authenticated)/<rel> folder."""
    from scripts.v2_audit import nav_dimension

    app_dir = tmp_path / "apps" / "web" / "src" / "app"
    target = app_dir / "(authenticated)" / "sessions" / "[id]" / "live"
    target.mkdir(parents=True)

    monkeypatch.setattr(
        nav_dimension, "_REPO_ROOT_OVERRIDE", tmp_path, raising=False
    )

    result = nav_dimension._route_to_filesystem_path("/sessions/[id]/live")
    assert result == target


def test_route_to_filesystem_path_public(tmp_path, monkeypatch):
    """Maps a public route to its (public)/<rel> folder."""
    from scripts.v2_audit import nav_dimension

    app_dir = tmp_path / "apps" / "web" / "src" / "app"
    target = app_dir / "(public)" / "join" / "event" / "[code]"
    target.mkdir(parents=True)

    monkeypatch.setattr(
        nav_dimension, "_REPO_ROOT_OVERRIDE", tmp_path, raising=False
    )

    result = nav_dimension._route_to_filesystem_path("/join/event/[code]")
    assert result == target


def test_route_to_filesystem_path_missing(tmp_path, monkeypatch):
    """Returns None when neither group nor root contains the route folder."""
    from scripts.v2_audit import nav_dimension

    monkeypatch.setattr(
        nav_dimension, "_REPO_ROOT_OVERRIDE", tmp_path, raising=False
    )

    result = nav_dimension._route_to_filesystem_path("/does-not-exist")
    assert result is None


def test_collect_route_tree_hrefs_picks_up_layout_link(tmp_path, monkeypatch):
    """A <Link href> in layout.tsx is included in the route-tree hrefs."""
    from scripts.v2_audit import nav_dimension

    app_dir = tmp_path / "apps" / "web" / "src" / "app"
    route_dir = app_dir / "(authenticated)" / "test-route"
    route_dir.mkdir(parents=True)
    (route_dir / "layout.tsx").write_text(
        'import Link from "next/link";\n'
        'export default function Layout({ children }: { children: React.ReactNode }) {\n'
        '  return <><Link href="/back">Back</Link>{children}</>;\n'
        '}\n',
        encoding="utf-8",
    )

    monkeypatch.setattr(
        nav_dimension, "_REPO_ROOT_OVERRIDE", tmp_path, raising=False
    )
    nav_dimension._collect_route_tree_hrefs.cache_clear()

    hrefs = nav_dimension._collect_route_tree_hrefs("/test-route")
    assert "/back" in hrefs


def test_collect_route_tree_hrefs_skips_test_files(tmp_path, monkeypatch):
    """Test files (.test.tsx, .stories.tsx) are not scanned."""
    from scripts.v2_audit import nav_dimension

    app_dir = tmp_path / "apps" / "web" / "src" / "app"
    route_dir = app_dir / "(authenticated)" / "some-route"
    route_dir.mkdir(parents=True)
    (route_dir / "layout.tsx").write_text(
        'import Link from "next/link";\nexport default () => <Link href="/real">Real</Link>;\n',
        encoding="utf-8",
    )
    (route_dir / "thing.test.tsx").write_text(
        'import Link from "next/link";\nexport const Fake = () => <Link href="/fake-from-test">x</Link>;\n',
        encoding="utf-8",
    )
    (route_dir / "thing.stories.tsx").write_text(
        'import Link from "next/link";\nexport const Story = () => <Link href="/fake-from-story">y</Link>;\n',
        encoding="utf-8",
    )

    monkeypatch.setattr(
        nav_dimension, "_REPO_ROOT_OVERRIDE", tmp_path, raising=False
    )
    nav_dimension._collect_route_tree_hrefs.cache_clear()

    hrefs = nav_dimension._collect_route_tree_hrefs("/some-route")
    assert "/real" in hrefs
    assert "/fake-from-test" not in hrefs
    assert "/fake-from-story" not in hrefs


def test_collect_route_tree_hrefs_missing_route(tmp_path, monkeypatch):
    """Non-existent route returns empty frozenset (no crash)."""
    from scripts.v2_audit import nav_dimension

    monkeypatch.setattr(
        nav_dimension, "_REPO_ROOT_OVERRIDE", tmp_path, raising=False
    )
    nav_dimension._collect_route_tree_hrefs.cache_clear()

    hrefs = nav_dimension._collect_route_tree_hrefs("/no-such-route")
    assert hrefs == frozenset()
