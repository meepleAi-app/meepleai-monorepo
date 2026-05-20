from pathlib import Path
from scripts.mockup_demo.html_patcher import patch_html_element

DEMO_MARKER = "/* DEMO-NAV */"


def test_patch_anchor_adds_href(tmp_path):
    f = tmp_path / "x.html"
    f.write_text('<a class="nav-item" href="#">Games</a>', encoding="utf-8")
    patch_html_element(f, selector_snippet='<a class="nav-item" href="#">Games',
                       destination="sp4-games-index.html")
    content = f.read_text(encoding="utf-8")
    assert 'href="sp4-games-index.html"' in content
    assert DEMO_MARKER in content


def test_idempotent(tmp_path):
    f = tmp_path / "x.html"
    f.write_text('<a class="nav-item" href="#">Games</a>', encoding="utf-8")
    patch_html_element(f, selector_snippet='<a class="nav-item" href="#">Games',
                       destination="sp4-games-index.html")
    once = f.read_text(encoding="utf-8")
    patch_html_element(f, selector_snippet='<a class="nav-item" href="#">Games',
                       destination="sp4-games-index.html")
    twice = f.read_text(encoding="utf-8")
    assert once == twice


def test_button_gets_onclick(tmp_path):
    f = tmp_path / "x.html"
    f.write_text('<button class="cta">Play</button>', encoding="utf-8")
    patch_html_element(f, selector_snippet='<button class="cta">Play',
                       destination="librogame-runthrough-game-onboarding.html")
    content = f.read_text(encoding="utf-8")
    assert "onclick=" in content
    assert "librogame-runthrough-game-onboarding.html" in content


def test_out_of_scope_skipped(tmp_path):
    f = tmp_path / "x.html"
    original = '<button class="cta">Play</button>'
    f.write_text(original, encoding="utf-8")
    patched = patch_html_element(f, selector_snippet=original, destination="OUT_OF_SCOPE")
    assert patched is False
    assert f.read_text(encoding="utf-8") == original


def test_empty_destination_skipped(tmp_path):
    f = tmp_path / "x.html"
    original = '<button class="cta">Play</button>'
    f.write_text(original, encoding="utf-8")
    patched = patch_html_element(f, selector_snippet=original, destination="")
    assert patched is False
    assert f.read_text(encoding="utf-8") == original
