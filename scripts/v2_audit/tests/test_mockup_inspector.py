from pathlib import Path
from scripts.v2_audit.mockup_inspector import inspect_mockup, MockupSnapshot

# Anchor to repo root so the test is cwd-independent, and target a STABLE mockup.
# (sp4-game-detail.html was deleted during mockup churn — #2998 fix.)
_REPO_ROOT = Path(__file__).resolve().parents[3]
REAL_MOCKUP = _REPO_ROOT / "admin-mockups" / "design_files" / "sp4-dashboard.html"


def test_inspect_real_mockup_landmarks():
    snap = inspect_mockup(REAL_MOCKUP)
    # sp4-dashboard.html has HTML landmarks (header/section) and headings (h1..h3)
    assert len(snap.landmarks) >= 1
    assert snap.headings


def test_inspect_extracts_link_destinations():
    snap = inspect_mockup(REAL_MOCKUP)
    # sp4-dashboard has wired hrefs to actual .html mockups
    assert any(".html" in dest for dest in snap.link_destinations)


def test_inspect_synthetic_mockup(tmp_path):
    f = tmp_path / "test.html"
    f.write_text(
        '<html><body>'
        '<header><h1>Title</h1></header>'
        '<main><a href="sp4-games-index.html">Games</a></main>'
        '</body></html>',
        encoding="utf-8",
    )
    snap = inspect_mockup(f)
    assert "header" in snap.landmarks
    assert "main" in snap.landmarks
    assert "h1" in snap.headings
    assert "sp4-games-index.html" in snap.link_destinations
