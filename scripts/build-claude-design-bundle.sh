#!/usr/bin/env bash
#
# build-claude-design-bundle.sh — rebuild a Claude-Design INPUT bundle from
# admin-mockups/design_files/ (the source of truth) into claude-design-bundle/<run>/.
#
# claude-design-bundle/ is gitignored (.gitignore:289) — the bundles are regenerable
# seeds you upload to claude.ai/design, NOT committed artifacts. This script restores
# the cp-able parts (shared scaffold + mockups). The authored companion files
# (00-system-prompt.md, 01-manifest.md, README.md) are NOT touched — recover them from
# git history or from docs/for-developers/workflows/claude-design-demo-prompts.md.
#
# Usage:  scripts/build-claude-design-bundle.sh [sp6|sp7|sp8|sp9|all]   (default: all)
#
# Tracking issues: SP6 → #1888 (libro-game) · SP7 → #1889 (game-night agent-builder)
#                  SP8 → #1890 (mobile parity + libro-game companion)
set -euo pipefail
cd "$(dirname "$0")/.."

SRC="admin-mockups/design_files"
SCAFFOLD=(tokens.css components.css data.js 00-hub.html state-matrix.html)

SP6_MOCKUPS=(
  librogame-game-night-storyboard.html
  librogame-runthrough-encounter-cheatsheet.html
  librogame-runthrough-error-states.html
  librogame-runthrough-game-detail.html
  librogame-runthrough-game-onboarding.html
  librogame-runthrough-glossary-editor.html
  librogame-runthrough-library-search.html
  librogame-runthrough-play-session.html
  librogame-runthrough-play-session.jsx
  librogame-runthrough-quota-credits.html
  librogame-runthrough-resume-picker.html
  librogame-runthrough-session-end.html
  librogame-runthrough-setup-chat.html
  librogame-runthrough-setup-wizard.html
  librogame-runthrough-translate-viewer.html
  sp6-libro-game-house-rule.html
  sp6-libro-game-house-rule.jsx
)

SP7_MOCKUPS=(
  sp7-game-night-new.html
  sp7-game-night-new.jsx
  sp7-game-night-detail-rsvp.html
  sp7-game-night-detail-rsvp.jsx
  sp7-game-night-live.html
  sp7-game-night-live.jsx
  sp7-game-night-transition.html
  sp7-game-night-transition.jsx
  sp7-game-night-summary.html
  sp7-game-night-summary.jsx
  sp7-game-night-join-public.jsx
  sp7-notifications-hub.html
  sp7-notifications-hub.jsx
  sp7-notifications-preferences.html
  sp7-notifications-preferences.jsx
)

SP8_MOCKUPS=(
  # Library mobile parity (route /library <768px)
  sp4-library-mobile.html
  sp4-library-mobile.jsx
  sp4-library-desktop.html
  sp4-library-desktop.jsx
  primitive-nav-bottom-mobile.html
  mobile-app.jsx
  # Libro-game companion (state-05 diary / state-06 paragrafi / state-07 end-campaign)
  librogame-runthrough-play-session.html
  librogame-runthrough-play-session.jsx
  librogame-runthrough-translate-viewer.html
)

# SP9 mobile-gamenight-social (#2989): GENERATION bundle — only existing reference mockups
# (the sp9-* mobile mockups are the OUTPUT, generated in-session, not seeds).
SP9_REFS=(
  sp7-game-night-transition.html
  sp7-game-night-transition.jsx
  primitive-nav-bottom-mobile.html
  mobile-app.jsx
)

# SP8 also ships the two design briefs (not in design_files/) so the demo has the DoD in-bundle.
copy_sp8_briefs() {
  local dest="claude-design-bundle/sp8-mobile/briefs"
  mkdir -p "$dest"
  cp admin-mockups/briefs/_common.md \
     admin-mockups/briefs/SP8-mobile-parity.md \
     admin-mockups/briefs/SP8-libro-game-companion.md "$dest/"
  echo "[built] +3 briefs → $dest/"
}

copy_sp9_briefs() {
  local dest="claude-design-bundle/sp9-gamenight-mobile/briefs"
  mkdir -p "$dest"
  cp admin-mockups/briefs/_common.md \
     admin-mockups/briefs/SP9-mobile-gamenight-social.md "$dest/"
  echo "[built] +2 briefs → $dest/"
}

build_bundle() {
  local name="$1"; shift
  local dest="claude-design-bundle/$name"
  mkdir -p "$dest/mockups"
  local f
  for f in "${SCAFFOLD[@]}"; do
    cp "$SRC/$f" "$dest/$f"
  done
  for f in "$@"; do
    cp "$SRC/$f" "$dest/mockups/$f"
  done
  # Mockups reference shared assets as siblings (href="tokens.css"), so mirror them into
  # mockups/ too — otherwise the relative refs break in the structured bundle (#1890 demo finding).
  for f in tokens.css components.css data.js; do
    cp "$SRC/$f" "$dest/mockups/$f"
  done
  echo "[built] $dest — scaffold ${#SCAFFOLD[@]} + mockups $# (+3 mirrored assets in mockups/)"
  for f in 00-system-prompt.md 01-manifest.md README.md; do
    [ -f "$dest/$f" ] || echo "  ⚠ companion missing: $dest/$f (restore from git / claude-design-demo-prompts.md)"
  done
}

target="${1:-all}"
case "$target" in
  sp6) build_bundle sp6-libro-game "${SP6_MOCKUPS[@]}" ;;
  sp7) build_bundle sp7-game-night "${SP7_MOCKUPS[@]}" ;;
  sp8) build_bundle sp8-mobile "${SP8_MOCKUPS[@]}"; copy_sp8_briefs ;;
  sp9) build_bundle sp9-gamenight-mobile "${SP9_REFS[@]}"; copy_sp9_briefs ;;
  all)
    build_bundle sp6-libro-game "${SP6_MOCKUPS[@]}"
    build_bundle sp7-game-night "${SP7_MOCKUPS[@]}"
    build_bundle sp8-mobile "${SP8_MOCKUPS[@]}"; copy_sp8_briefs
    ;;
  *) echo "usage: $0 [sp6|sp7|sp8|sp9|all]" >&2; exit 2 ;;
esac
