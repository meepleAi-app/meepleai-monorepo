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


def test_index_to_detail():
    # Click on a card inside sp4-games-index → game detail
    decision = resolve_destination(_click(
        text="Catan", classes="game-card", file_name="sp4-games-index.jsx", tag="div"
    ))
    assert decision.destination == "sp4-game-detail.html"
    assert decision.rule == "index-to-detail"


def test_detail_action_keyword():
    decision = resolve_destination(_click(
        text="Avvia libro game", classes="cta-primary", file_name="sp4-game-detail.jsx", tag="button"
    ))
    assert decision.destination == "librogame-runthrough-game-onboarding.html"
    assert decision.rule == "detail-action-keyword"


def test_canonical_takes_priority_over_keyword():
    # 'Settings' is a canonical sidebar entry, must not match any keyword
    decision = resolve_destination(_click(text="Settings", classes="sidebar nav-item"))
    assert decision.destination == "settings.html"
    assert decision.rule == "canonical-sidebar"
