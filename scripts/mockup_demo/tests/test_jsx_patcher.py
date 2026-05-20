from pathlib import Path
from scripts.mockup_demo.jsx_patcher import patch_jsx_element

DEMO_MARKER = "/* DEMO-NAV */"


def test_jsx_element_without_onclick(tmp_path):
    f = tmp_path / "c.jsx"
    f.write_text('<li className="nav-item">Games</li>', encoding="utf-8")
    patch_jsx_element(f, selector_snippet='<li className="nav-item">Games',
                      destination="sp4-games-index.html")
    content = f.read_text(encoding="utf-8")
    assert "onClick" in content
    assert "sp4-games-index.html" in content
    assert DEMO_MARKER in content


def test_jsx_element_with_existing_onclick_wraps(tmp_path):
    f = tmp_path / "c.jsx"
    f.write_text(
        '<button className="cta" onClick={() => handlePlay()}>Play</button>',
        encoding="utf-8",
    )
    patch_jsx_element(f, selector_snippet='<button className="cta" onClick={() => handlePlay()}>Play',
                      destination="librogame-runthrough-game-onboarding.html")
    content = f.read_text(encoding="utf-8")
    assert "handlePlay()" in content  # original preserved
    assert "librogame-runthrough-game-onboarding.html" in content
    assert DEMO_MARKER in content


def test_idempotent(tmp_path):
    f = tmp_path / "c.jsx"
    f.write_text('<li className="nav-item">Games</li>', encoding="utf-8")
    patch_jsx_element(f, selector_snippet='<li className="nav-item">Games',
                      destination="sp4-games-index.html")
    once = f.read_text(encoding="utf-8")
    patch_jsx_element(f, selector_snippet='<li className="nav-item">Games',
                      destination="sp4-games-index.html")
    twice = f.read_text(encoding="utf-8")
    assert once == twice


def test_out_of_scope_skipped(tmp_path):
    f = tmp_path / "c.jsx"
    original = '<li className="nav-item">Games</li>'
    f.write_text(original, encoding="utf-8")
    patched = patch_jsx_element(f, selector_snippet='<li className="nav-item">Games',
                                destination="OUT_OF_SCOPE")
    assert patched is False
    assert f.read_text(encoding="utf-8") == original
