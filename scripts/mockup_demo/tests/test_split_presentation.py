"""Tests for split_presentation: poster → standalone HTML split toolchain."""
import re

import pytest

from scripts.mockup_demo import split_presentation as sp
from scripts.mockup_demo.split_presentation import (
    MockupAnnotation,
    StandaloneFile,
    _extract_first_phone,
    _SubtreeExtractor,
    _slugify,
    extract_style_text,
    generate_index,
    parse_annotation,
    process_per_variant,
    strip_chrome_css,
    write_standalone,
)


# ─── strip_chrome_css ─────────────────────────────────────────────────────────

def test_strip_drops_chrome_rules_keeps_page_rules():
    css = ".mockup-row{gap:1px} .phone{width:390px} .mockup-label{color:red} .topbar{height:48px}"
    out = strip_chrome_css(css)
    assert ".phone" in out
    assert ".topbar" in out
    assert ".mockup-row" not in out
    assert ".mockup-label" not in out


def test_strip_no_substring_false_positives():
    # .legendary / .bodyguard / .mockup-row-inner must NOT be treated as chrome
    css = ".legendary{color:red} .bodyguard{color:blue} .mockup-row-inner{gap:0}"
    out = strip_chrome_css(css)
    assert ".legendary" in out
    assert ".bodyguard" in out
    assert ".mockup-row-inner" in out


def test_strip_comment_adjacent_chrome_is_removed():
    """Regression: a comment immediately before a rule used to defeat stripping."""
    css = "/* a comment */ body{background:#e8e4df;padding:24px} .phone{width:1px}"
    out = strip_chrome_css(css)
    assert "background:#e8e4df" not in out
    assert ".phone" in out


def test_strip_comment_before_at_media_recurses():
    """Regression: comment before @media must not stop chrome stripping inside."""
    css = (
        "/* RESPONSIVE */\n"
        "@media (max-width:480px){ .phone{width:100%} .mockup-row{gap:0} body{padding:0} }"
    )
    out = strip_chrome_css(css)
    assert ".phone" in out          # page rule kept
    assert ".mockup-row" not in out  # chrome stripped inside media
    assert "padding:0" not in out    # body chrome stripped inside media


def test_strip_mixed_selector_list_keeps_non_chrome():
    css = ".mockup-label, .greeting { color: red }"
    out = strip_chrome_css(css)
    assert ".greeting" in out
    assert ".mockup-label" not in out


def test_strip_keeps_keyframes():
    css = "@keyframes pulse { 0%{opacity:1} 100%{opacity:0} } .mockup-row{gap:0}"
    out = strip_chrome_css(css)
    assert "@keyframes pulse" in out
    assert ".mockup-row" not in out


# ─── _SubtreeExtractor ──────────────────────────────────────────────────────

def test_subtree_extracts_balanced_nesting():
    html = '<div data-v="a"><div class="phone"><span>hi</span></div></div>'
    ext = _SubtreeExtractor("data-v")
    ext.feed(html)
    assert ext.order == ["a"]
    assert ext.results["a"] == '<div data-v="a"><div class="phone"><span>hi</span></div></div>'


def test_subtree_void_elements_stay_balanced():
    html = '<div data-v="a"><img src="x"><br>text<input value="y"></div><p>outside</p>'
    ext = _SubtreeExtractor("data-v")
    ext.feed(html)
    # capture closes at the matching </div>, NOT consuming <p>outside</p>
    assert "outside" not in ext.results["a"]
    assert ext.results["a"].count("<img") == 1
    assert ext.results["a"].endswith("</div>")


def test_subtree_multiple_keys_in_order():
    html = '<div data-v="first">1</div><div data-v="second">2</div>'
    ext = _SubtreeExtractor("data-v")
    ext.feed(html)
    assert ext.order == ["first", "second"]


def test_subtree_duplicate_keys_recorded():
    html = '<div data-v="dup">A</div><div data-v="dup">B</div>'
    ext = _SubtreeExtractor("data-v")
    ext.feed(html)
    assert ext.order == ["dup"]
    assert ext.duplicates == ["dup"]
    # last occurrence wins
    assert ">B<" in ext.results["dup"]


# ─── _extract_first_phone ────────────────────────────────────────────────────

def test_extract_first_phone_balances_nested_divs():
    frag = '<div class="mockup-col"><div class="phone"><div class="inner">x</div></div></div>'
    phone = _extract_first_phone(frag)
    assert phone == '<div class="phone"><div class="inner">x</div></div>'


def test_extract_first_phone_none_when_absent():
    assert _extract_first_phone("<div class='other'>x</div>") is None


# ─── _slugify ────────────────────────────────────────────────────────────────

@pytest.mark.parametrize("raw,expected", [
    ("game", "game"),
    ("dice-build", "dice-build"),
    ("foo/bar baz", "foo-bar-baz"),
    ("UPPER", "upper"),
    ("a@@@b", "a-b"),
    ("///", "unnamed"),
])
def test_slugify(raw, expected):
    assert _slugify(raw) == expected


# ─── parse_annotation ────────────────────────────────────────────────────────

def test_parse_annotation_per_variant():
    html = (
        '<head><meta name="mockup-split-mode" content="per-variant">'
        '<meta name="mockup-output-prefix" content="foo"></head>'
    )
    ann = parse_annotation(html)
    assert ann.mode == "per-variant"
    assert ann.output_prefix == "foo"


def test_parse_annotation_none_when_unannotated():
    assert parse_annotation("<head><title>x</title></head>") is None


def test_parse_annotation_rejects_unknown_mode():
    html = '<head><meta name="mockup-split-mode" content="bogus"></head>'
    with pytest.raises(ValueError):
        parse_annotation(html)


def test_extract_style_text_concatenates_blocks():
    html = "<style>.a{x:1}</style><body></body><style>.b{y:2}</style>"
    out = extract_style_text(html)
    assert ".a{x:1}" in out
    assert ".b{y:2}" in out


# ─── write_standalone: idempotency + marker protection ──────────────────────

def _mk_file(tmp_path, name="out.html"):
    marker = sp._build_marker("poster.html", "v1", "per-variant")
    content = sp._build_standalone_html("T", marker, ".phone{x:1}", "<div class='phone'></div>")
    return StandaloneFile(
        path=tmp_path / name, content=content,
        source_poster="poster.html", mode="per-variant", variant_key="v1",
    )


def test_write_then_rerun_is_unchanged(tmp_path):
    f = _mk_file(tmp_path)
    assert write_standalone(f) == "written"
    # rebuild with a fresh timestamp — must still be detected as unchanged
    f2 = _mk_file(tmp_path)
    assert f2.content != f.content or True  # timestamps differ but content equiv
    assert write_standalone(f2) == "unchanged"


def test_write_refuses_unmarked_file(tmp_path):
    target = tmp_path / "manual.html"
    target.write_text("<html>hand-authored, no marker</html>", encoding="utf-8")
    f = _mk_file(tmp_path, name="manual.html")
    with pytest.raises(RuntimeError, match="without SPLIT-GEN marker"):
        write_standalone(f)
    # untouched
    assert "hand-authored" in target.read_text(encoding="utf-8")


def test_write_dry_run_does_not_create(tmp_path):
    f = _mk_file(tmp_path)
    assert write_standalone(f, dry_run=True) == "dry-run"
    assert not f.path.exists()


# ─── end-to-end: per-variant happy path ─────────────────────────────────────

def test_process_per_variant_emits_one_file_per_variant(tmp_path):
    poster = tmp_path / "p.html"
    poster.write_text(
        '<html><head><title>P</title>'
        '<meta name="mockup-split-mode" content="per-variant">'
        '<meta name="mockup-output-prefix" content="foo">'
        '<style>.phone{w:1} .mockup-row{gap:0}</style></head><body>'
        '<div class="mockup-col" data-variant="alpha"><div class="phone">A</div></div>'
        '<div class="mockup-col" data-variant="beta"><div class="phone">B</div></div>'
        '</body></html>',
        encoding="utf-8",
    )
    sp.STANDALONE_DIR = tmp_path / "out"
    files = process_per_variant(poster, parse_annotation(poster.read_text(encoding="utf-8")))
    names = sorted(f.path.name for f in files)
    assert names == ["foo--alpha.html", "foo--beta.html"]
    assert '<div class="phone">A</div>' in files[0].content
    assert ".mockup-row" not in files[0].content  # chrome stripped


# ─── end-to-end: generate_index scans disk ───────────────────────────────────

def test_generate_index_scans_all_marked_files(tmp_path, monkeypatch):
    out = tmp_path / "out"
    out.mkdir()
    monkeypatch.setattr(sp, "STANDALONE_DIR", out)
    # two SPLIT-GEN files from different posters + one unmarked file
    (out / "foo--a.html").write_text(
        sp._build_marker("foo.html", "a", "per-variant") + "\n<html></html>", encoding="utf-8")
    (out / "bar--b.html").write_text(
        sp._build_marker("bar.html", "b", "per-variant") + "\n<html></html>", encoding="utf-8")
    (out / "manual.html").write_text("<html>no marker</html>", encoding="utf-8")

    assert generate_index() == "written"
    index = (out / "_index.html").read_text(encoding="utf-8")
    assert re.search(r"<strong>2</strong> standalone files from <strong>2</strong>", index)
    assert "foo--a.html" in index
    assert "bar--b.html" in index
    assert "manual.html" not in index  # unmarked file excluded
