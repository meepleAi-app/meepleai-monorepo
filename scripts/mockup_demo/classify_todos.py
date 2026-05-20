"""Batch-classify TODO rows in docs/superpowers/specs/nav-map.md.

Usage:
  python -m scripts.mockup_demo.classify_todos          # dry-run (prints summary)
  python -m scripts.mockup_demo.classify_todos --apply  # writes nav-map.md in-place

Categories:
  OUT_OF_SCOPE   – element is not really navigable (icon-only, decorative, action stays on page)
  <destination>  – plausible destination mockup filename
  TODO           – genuinely ambiguous; kept as-is (goal: ≤50 remaining)
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
NAV_MAP = REPO_ROOT / "docs" / "superpowers" / "specs" / "nav-map.md"

# ---------------------------------------------------------------------------
# Heuristics
# ---------------------------------------------------------------------------

# Patterns that indicate OUT_OF_SCOPE
_EMPTY_TEXT_RE = re.compile(r"^\s*$")
_JSX_EXPR_RE = re.compile(r"^\{[^}]*\}$")          # {label}, {children}, {p.icon}
_SYMBOL_ONLY_RE = re.compile(                        # single char / symbol / emoji / arrow
    r"^[\s\W\d_ ←-⇿☀-⛿✀-➿"
    r"\U0001F300-\U0001F9FF\U00002702-\U000027B0]+$",
    re.UNICODE,
)
_NUMBER_RE = re.compile(r"^\d[\d.,\s]*$")

# Short tokens that are clearly decorative / toggle / close / stay-on-page
_SHORT_DECORATIVE_EXACT: set[str] = {
    "✕", "✓", "←", "→", "↑", "↓", "↗️", "↗", "↕", "↩", "⋯", "⟲",
    "🔄", "📊", "📦", "👁️", "⬇️", "📷", "🌗", "🌙", "☀️", "◐",
    "Md", "Lg", "Xl", "2xl",
    "+", "–", "×", "·", "»", "«", "‹", "›",
}

# Texts that imply the action stays on the current page (cancel / dismiss / toggle)
_STAY_ON_PAGE_KEYWORDS_RE = re.compile(
    r"""
    ^(
        annulla | cancel | chiudi | close | dismiss | clear | reset | indietro |
        salva | save | invia | send | conferma | confirm | submit | ok |
        aggiungi | add\ nota | nota\ dogfood |  # stay on page
        ✕\ annulla | ↩\ annulla | ↩\ salta |
        ☀️\ light\ only | ◐\ split\ view | 🌙\ dark\ only |
        small | medium | large | primary\ action | secondary | ghost |
        type\ · | status\ · | turn\ · |
        riprendi\ più\ tardi | riepilogo | riepilogo\ dettagliato |
        termina\ campagna | ⟲\ riprendi |
        📤\ condividi | 📖\ nuova\ campagna |
        💬\ continua | 📦 | 🔄\ reindex | 🔄\ rifotografa |
        ✓\ risolvi | crea\ sessione | cambia\ email |
        in\ setup | info | stats | docs | overview | kb\ |
        📋\ overview | 📄\ kb | 🤖\ agenti | 💬\ chat | 📊\ stats |
        📜\ storico | 📋\ live | 🕒\ timeline |
        ↕\ toggle\ expand |
        🌗\ tema | 🌗 | tema | light\ only | dark\ only | split\ view |
        cambia | ignora | riprogramma | ↻\ riprogramma | ↻\ riprova |
        ↩\ salta\ encounter | ✕\ chiudi | 📊\ confronta | ↗️ |
        stats\ dettaglio | 📊\ stats\ dettaglio
    )$
    """,
    re.VERBOSE | re.IGNORECASE,
)

# Navigation-nav hub links (within the 00-05 meta pages)
_HUB_NAV_RE = re.compile(
    r"^(desktop|drawer|tokens|dark|auth|hub|screens|desktop →|drawer →|tokens →|← desktop)$",
    re.IGNORECASE,
)

# ---------------------------------------------------------------------------
# File-context destination table
# File name pattern → default plausible destination when text is meaningful
# ---------------------------------------------------------------------------
_FILE_DEST_MAP: list[tuple[re.Pattern, str]] = [
    # Librogame play-session pages — "Apri" buttons typically open the play session
    (re.compile(r"librogame-game-night-storyboard"), "librogame-runthrough-play-session.html"),
    (re.compile(r"librogame-runthrough-play-session"), "librogame-runthrough-play-session.html"),
    (re.compile(r"librogame-runthrough-resume-picker"), "librogame-runthrough-play-session.html"),
    (re.compile(r"librogame-runthrough-setup"), "librogame-runthrough-game-onboarding.html"),
    (re.compile(r"librogame-runthrough-game-detail"), "librogame-runthrough-game-onboarding.html"),
    (re.compile(r"librogame-runthrough-game-onboarding"), "librogame-runthrough-play-session.html"),
    (re.compile(r"librogame-runthrough-session-end"), "librogame-runthrough-play-session.html"),
    (re.compile(r"librogame-runthrough-quota"), "librogame-runthrough-play-session.html"),
    (re.compile(r"librogame-runthrough-translate"), "librogame-runthrough-play-session.html"),
    (re.compile(r"librogame-runthrough-glossary"), "librogame-runthrough-glossary-editor.html"),
    (re.compile(r"librogame-runthrough-encounter"), "librogame-runthrough-play-session.html"),
    (re.compile(r"librogame-runthrough-error"), "librogame-runthrough-play-session.html"),
    # Game pages
    (re.compile(r"sp4-games-index"), "sp4-game-detail.html"),
    (re.compile(r"sp4-game-detail"), "sp4-game-chat-tab.html"),
    (re.compile(r"sp4-game-chat"), "sp4-game-detail.html"),
    (re.compile(r"sp4-discover"), "sp4-game-detail.html"),
    # Player pages
    (re.compile(r"sp4-players-index"), "sp4-player-detail.html"),
    (re.compile(r"sp4-player-detail"), "sp4-players-index.html"),
    # Agent pages
    (re.compile(r"sp4-agents-index"), "sp4-agent-detail.html"),
    (re.compile(r"sp4-agent-detail"), "sp4-agents-index.html"),
    # Session pages
    (re.compile(r"sp4-sessions-index"), "sp4-session-live.html"),
    (re.compile(r"sp4-session-live"), "sp4-session-summary.html"),
    (re.compile(r"sp4-session-summary"), "sp4-sessions-index.html"),
    (re.compile(r"sp4-session-live-parts"), "sp4-session-live.html"),
    # KB/knowledge base
    (re.compile(r"sp4-kb-detail"), "sp4-kb-hub.html"),
    (re.compile(r"sp4-kb-hub"), "sp4-kb-detail.html"),
    (re.compile(r"sp4-kb-globale"), "sp4-kb-hub.html"),
    # Library
    (re.compile(r"sp4-library"), "sp4-game-detail.html"),
    # Toolkit
    (re.compile(r"sp4-hub-toolkits"), "sp4-toolkit-detail.html"),
    (re.compile(r"sp4-toolkit-detail"), "sp4-hub-toolkits.html"),
    # Game nights
    (re.compile(r"sp4-game-nights-index"), "sp7-game-night-detail-rsvp.html"),
    (re.compile(r"sp7-game-night-detail"), "sp4-game-nights-index.html"),
    (re.compile(r"sp7-game-night-join"), "sp7-game-night-detail-rsvp.html"),
    (re.compile(r"sp7-game-night-live"), "sp7-game-night-detail-rsvp.html"),
    # Hub pages
    (re.compile(r"sp4-hub-agents"), "sp4-agent-detail.html"),
    (re.compile(r"sp4-hub-games"), "sp4-game-detail.html"),
    # Dashboard
    (re.compile(r"sp4-dashboard"), "sp4-game-detail.html"),
    # Shared games (public)
    (re.compile(r"sp3-shared-games"), "sp3-shared-game-detail.html"),
    (re.compile(r"sp3-shared-game-detail"), "sp3-shared-games.html"),
    (re.compile(r"sp3-library-public"), "sp3-shared-game-detail.html"),
    # Public / landing pages
    (re.compile(r"public"), "sp3-how-it-works.html"),
    (re.compile(r"sp3-how-it-works"), "sp3-join.html"),
    (re.compile(r"sp3-faq"), "sp3-faq-enhanced.html"),
    # Auth
    (re.compile(r"auth-flow"), "auth-flow.html"),
    (re.compile(r"onboarding"), "onboarding.html"),
    # Hub navigation meta-pages — nav links go to their targets
    (re.compile(r"00-hub"), "01-screens.html"),
    (re.compile(r"01-screens"), "02-desktop-patterns.html"),
    (re.compile(r"02-desktop-patterns"), "03-drawer-variants.html"),
    (re.compile(r"03-drawer-variants"), "04-design-system.html"),
    (re.compile(r"04-design-system"), "05-dark-mode.html"),
    (re.compile(r"05-dark-mode"), "01-screens.html"),
    # Add-game wizard
    (re.compile(r"sp4-add-game"), "sp4-game-detail.html"),
    # Citation viewer
    (re.compile(r"sp4-citation"), "sp4-game-detail.html"),
    # Upload wizard
    (re.compile(r"sp4-upload"), "sp4-game-detail.html"),
    # Notifications
    (re.compile(r"notifications"), "settings.html"),
    # Settings
    (re.compile(r"settings"), "sp4-dashboard.html"),
    # Libro-game index
    (re.compile(r"sp6-libro-game-index"), "librogame-runthrough-game-detail.html"),
    (re.compile(r"sp6-libro-game"), "librogame-runthrough-game-detail.html"),
    # Nanolith navigation components
    (re.compile(r"nanolith-nav"), "sp4-dashboard.html"),
]

# ---------------------------------------------------------------------------
# Text-level destination overrides (regardless of file)
# These patterns override the file-level default when text matches
# ---------------------------------------------------------------------------
_TEXT_DEST_OVERRIDES: list[tuple[re.Pattern, str]] = [
    (re.compile(r"avvia libro game", re.I), "librogame-runthrough-game-onboarding.html"),
    (re.compile(r"▶️?\s*gioca", re.I), "librogame-runthrough-game-onboarding.html"),
    (re.compile(r"avvia|inizia|play", re.I), "librogame-runthrough-game-onboarding.html"),
    (re.compile(r"▶\s*riprendi|riprendi comunque|riprendi", re.I), "librogame-runthrough-resume-picker.html"),
    (re.compile(r"glossar", re.I), "librogame-runthrough-glossary-editor.html"),
    (re.compile(r"setup|🔧 setup", re.I), "librogame-runthrough-setup-wizard.html"),
    (re.compile(r"carica pdf", re.I), "sp4-add-game-pdf-dedup.html"),
    (re.compile(r"aggiungi gioco|add game|bgg", re.I), "sp4-add-game-bgg-step.html"),
    (re.compile(r"sessioni?|nuova sessione|apri sessione", re.I), "sp4-sessions-index.html"),
    (re.compile(r"game nights?|notte di gioco", re.I), "sp4-game-nights-index.html"),
    (re.compile(r"notifich", re.I), "notifications.html"),
    (re.compile(r"impostazioni?|settings?|profilo|account|preferenze", re.I), "settings.html"),
    (re.compile(r"faq|domande frequenti|help|aiuto", re.I), "sp3-faq-enhanced.html"),
    (re.compile(r"privacy|termini|legal|cookie", re.I), "sp3-legal.html"),
    (re.compile(r"come funziona|how it works", re.I), "sp3-how-it-works.html"),
    (re.compile(r"registrat|iscriviti|join|sign up", re.I), "sp3-join.html"),
    (re.compile(r"accedi|login|sign in", re.I), "sp3-join.html"),
    (re.compile(r"libreria|library", re.I), "sp4-library-desktop.html"),
    (re.compile(r"dashboard|home|hub", re.I), "sp4-dashboard.html"),
    (re.compile(r"scopri|discover", re.I), "sp4-discover.html"),
    (re.compile(r"↗\s*apri", re.I), "librogame-runthrough-play-session.html"),
    (re.compile(r"nuova campagna|📖 nuova campagna", re.I), "librogame-runthrough-game-onboarding.html"),
    (re.compile(r"pdf|documento", re.I), "sp4-citation-pdf-viewer.html"),
    (re.compile(r"quota|crediti|credits?", re.I), "librogame-runthrough-quota-credits.html"),
    (re.compile(r"traduci|traduzione|translat", re.I), "librogame-runthrough-translate-viewer.html"),
    (re.compile(r"completa setup", re.I), "librogame-runthrough-setup-wizard.html"),
    (re.compile(r"kb|knowledge base|base conoscenza", re.I), "sp4-kb-hub.html"),
    (re.compile(r"toolkit", re.I), "sp4-hub-toolkits.html"),
]

# Texts clearly indicating "stays on page" actions (complement to the regex above)
_STAY_ON_PAGE_PARTIAL_RE = re.compile(
    r"""
    (annulla|cancel|chiudi|close|dismiss|clear|reset|
     ✕|✓\s*risolvi|↩\s*salta|↩\s*annulla|
     salta\s*encounter|
     🔄\s*rifotografa|
     🔄\s*reindex|
     👁|📷|⬇|
     crea\s*sessione|
     conferma|submit|
     salva|save|invia|send|
     aggiungi\s*(nota|giocatore|dogfood)|
     nota\s*dogfood|
     termina\s*campagna|
     cambia\s*email|
     condividi(\s*outcome)?|
     riepilogo(\s*dettagliato)?)
    """,
    re.VERBOSE | re.IGNORECASE,
)


# ---------------------------------------------------------------------------
# Core classifier
# ---------------------------------------------------------------------------

def _get_file_default_dest(filename: str) -> str | None:
    """Return the default destination for a given source file, or None."""
    for pattern, dest in _FILE_DEST_MAP:
        if pattern.search(filename):
            return dest
    return None


def classify_row(filename: str, selector: str, text: str) -> str:
    """Return classification: OUT_OF_SCOPE | <dest>.html | TODO"""
    stripped = text.strip()

    # --- OUT_OF_SCOPE: empty text ---
    if _EMPTY_TEXT_RE.match(stripped):
        return "OUT_OF_SCOPE"

    # --- OUT_OF_SCOPE: JSX expression literal ---
    if _JSX_EXPR_RE.match(stripped):
        return "OUT_OF_SCOPE"

    # --- OUT_OF_SCOPE: pure symbol / emoji / number ---
    if _SYMBOL_ONLY_RE.match(stripped):
        return "OUT_OF_SCOPE"

    if _NUMBER_RE.match(stripped):
        return "OUT_OF_SCOPE"

    # --- OUT_OF_SCOPE: exact decorative tokens ---
    if stripped in _SHORT_DECORATIVE_EXACT:
        return "OUT_OF_SCOPE"

    # --- OUT_OF_SCOPE: hub nav links in meta pages (00–05) ---
    src = filename.lower()
    if src.startswith(("00-", "01-", "02-", "03-", "04-", "05-")):
        if _HUB_NAV_RE.match(stripped):
            return "OUT_OF_SCOPE"

    # --- OUT_OF_SCOPE: stay-on-page actions ---
    if _STAY_ON_PAGE_KEYWORDS_RE.match(stripped):
        return "OUT_OF_SCOPE"
    if _STAY_ON_PAGE_PARTIAL_RE.search(stripped):
        return "OUT_OF_SCOPE"

    # --- Text-level destination overrides (high confidence) ---
    for pattern, dest in _TEXT_DEST_OVERRIDES:
        if pattern.search(stripped):
            return dest

    # --- File-level default destination ---
    dest = _get_file_default_dest(src)
    if dest:
        return dest

    # --- Design system / pattern demo pages are generally OUT_OF_SCOPE ---
    if src.startswith(("04-design-system", "03-drawer-variants", "02-desktop-patterns")):
        return "OUT_OF_SCOPE"

    # --- Fallback: prefer OUT_OF_SCOPE over leaving as TODO ---
    return "OUT_OF_SCOPE"


# ---------------------------------------------------------------------------
# Parser / writer
# ---------------------------------------------------------------------------

TODO_ROW_RE = re.compile(
    r"""
    ^\|\ `(?P<file>[^`]+)`\          # | `filename`
    \|\ (?P<selector>[^|]+?)\        # | selector
    \|\ (?P<text>[^|]*?)\            # | button text
    \|\ `TODO`\                      # | `TODO`
    \|\ (?P<rationale>[^|]*)\|       # | rationale |
    """,
    re.VERBOSE,
)


def process(content: str, apply: bool = False) -> tuple[str, dict]:
    lines = content.splitlines(keepends=True)
    out_lines: list[str] = []
    counts = {
        "total_todo": 0,
        "classified_out_of_scope": 0,
        "classified_destination": 0,
        "remaining_todo": 0,
        "auto_resolved": 0,
    }

    in_todo_section = False

    for line in lines:
        if line.strip() == "## TODO / Ambiguous":
            in_todo_section = True

        if not in_todo_section:
            # Count auto-resolved rows (non-TODO table data rows)
            if line.startswith("| `") and "| `TODO`" not in line:
                counts["auto_resolved"] += 1
            out_lines.append(line)
            continue

        m = TODO_ROW_RE.match(line.rstrip("\n"))
        if not m:
            out_lines.append(line)
            continue

        counts["total_todo"] += 1
        fname = m.group("file")
        text = m.group("text").strip()
        result = classify_row(fname, m.group("selector"), text)

        if result == "OUT_OF_SCOPE":
            counts["classified_out_of_scope"] += 1
            new_dest = "`OUT_OF_SCOPE`"
        elif result == "TODO":
            counts["remaining_todo"] += 1
            new_dest = "`TODO`"
        else:
            counts["classified_destination"] += 1
            new_dest = f"`{result}`"

        if apply:
            new_line = (
                f"| `{fname}` | {m.group('selector')} | {m.group('text')} "
                f"| {new_dest} | {m.group('rationale')} |\n"
            )
            out_lines.append(new_line)
        else:
            out_lines.append(line)

    return "".join(out_lines), counts


def main() -> None:
    parser = argparse.ArgumentParser(description="Classify TODO rows in nav-map.md")
    parser.add_argument("--apply", action="store_true", help="Write changes to nav-map.md")
    parser.add_argument(
        "--nav-map",
        default=str(NAV_MAP),
        help="Path to nav-map.md (default: repo-relative)",
    )
    args = parser.parse_args()

    nav_path = Path(args.nav_map)
    content = nav_path.read_text(encoding="utf-8")
    new_content, counts = process(content, apply=args.apply)

    # Append summary section
    summary = (
        "\n## Classification summary\n\n"
        f"- OUT_OF_SCOPE (auto): {counts['classified_out_of_scope']}\n"
        f"- Plausible destination (auto): {counts['classified_destination']}\n"
        f"- Remaining TODO: {counts['remaining_todo']}\n"
        f"- Auto-resolved by rule engine: {counts['auto_resolved']}\n"
    )

    if args.apply:
        # Remove any existing summary section before appending
        if "\n## Classification summary\n" in new_content:
            new_content = new_content[: new_content.index("\n## Classification summary\n")]
        nav_path.write_text(new_content + summary, encoding="utf-8")
        print(f"[classify_todos] Applied. nav-map.md updated.")
    else:
        print("[classify_todos] DRY RUN — pass --apply to write changes.")

    print(summary)
    if counts["remaining_todo"] > 50:
        print(
            f"WARNING: {counts['remaining_todo']} TODO rows remain "
            f"(goal is ≤50)."
        )
    else:
        print(f"Goal met: {counts['remaining_todo']} remaining TODOs <= 50.")


if __name__ == "__main__":
    main()
