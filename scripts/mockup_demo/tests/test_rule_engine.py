from pathlib import Path
from scripts.mockup_demo.clickable_extractor import Clickable
from scripts.mockup_demo.rule_engine import resolve_destination, Decision


def _click(text="", classes="", file_name="sp4-dashboard.jsx", tag="li"):
    return Clickable(
        file_path=Path(f"admin-mockups/design_files/{file_name}"),
        tag=tag,
        line_number=1,
        text=text,
        classes=classes,
        snippet="<...>",
        on_click_existing=None,
        kind="jsx" if file_name.endswith(".jsx") else "html",
    )


def test_canonical_sidebar_lookup():
    decision = resolve_destination(_click(text="Games", classes="sidebar nav-item"))
    assert decision.destination == "sp4-games-index.html"
    assert decision.rule == "canonical-sidebar"
    assert decision.confidence >= 0.95


def test_canonical_public_topbar_lookup():
    decision = resolve_destination(_click(text="FAQ", classes="topbar-link", file_name="sp3-join.html", tag="a"))
    assert decision.destination == "sp3-faq-enhanced.html"
    assert decision.rule == "canonical-public-topbar"


def test_no_match_returns_none_destination():
    decision = resolve_destination(_click(text="Some random text", classes=""))
    assert decision.destination is None
    assert decision.rule == "no-match"
