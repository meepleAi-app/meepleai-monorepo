from scripts.mockup_demo.classify_todos import classify_row


def test_classify_out_of_scope_empty_text():
    """Empty/whitespace text -> OUT_OF_SCOPE."""
    result = classify_row(filename="sp4-game-detail.html", selector="div:L1:", text="")
    assert result == "OUT_OF_SCOPE"


def test_classify_out_of_scope_jsx_expression():
    """JSX expression text like {label} -> OUT_OF_SCOPE."""
    result = classify_row(filename="sp4-game-detail.html", selector="li:L1:{it.label}", text="{it.label}")
    assert result == "OUT_OF_SCOPE"


def test_classify_out_of_scope_emoji_only():
    """Emoji/symbol-only text -> OUT_OF_SCOPE."""
    result = classify_row(filename="sp4-game-detail.html", selector="button:L1:✕", text="✕")
    assert result == "OUT_OF_SCOPE"


def test_classify_text_override_avvia():
    """'Avvia libro game' text maps to game-onboarding (text override)."""
    result = classify_row(
        filename="some-file.html",
        selector="button:L1:Avvia libro game",
        text="Avvia libro game",
    )
    assert "game-onboarding" in result


def test_classify_file_context_default():
    """A file without explicit context falls back to OUT_OF_SCOPE."""
    result = classify_row(
        filename="unknown-file.html",
        selector="button:L1:Generic button text",
        text="Generic button text",
    )
    # The fallback is OUT_OF_SCOPE per current behavior
    assert result == "OUT_OF_SCOPE"
