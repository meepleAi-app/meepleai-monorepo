#!/usr/bin/env bash
# rewrite-docs-links.sh — Rewrite Markdown link prefixes after a docs/ move.
#
# After running `git mv docs/old/path docs/new/path`, run this script to fix
# all `](old/path/...)` references across all tracked .md files.
#
# Usage:
#   scripts/rewrite-docs-links.sh <old-prefix> <new-prefix>
#
# Examples:
#   scripts/rewrite-docs-links.sh "docs/architecture/" "docs/for-claude/architecture/"
#   scripts/rewrite-docs-links.sh "./architecture/" "./for-claude/architecture/"
#
# Notes:
#   - Operates only on git-tracked .md files (run from repo root).
#   - Run twice: once with the absolute-from-root form (`docs/x/`) and once
#     with the relative-from-docs form (`./x/`) if both styles are used.
#   - Use --dry-run to preview changes.

set -euo pipefail

DRY_RUN=0
if [[ "${1:-}" == "--dry-run" ]]; then
  DRY_RUN=1
  shift
fi

if [[ $# -ne 2 ]]; then
  echo "Usage: $0 [--dry-run] <old-prefix> <new-prefix>" >&2
  echo "Example: $0 'docs/architecture/' 'docs/for-claude/architecture/'" >&2
  exit 2
fi

OLD="$1"
NEW="$2"

# Escape for sed (basic): /, &, and the delimiter (we use |)
escape_sed() {
  printf '%s' "$1" | sed -e 's/[|&]/\\&/g'
}

OLD_ESC=$(escape_sed "$OLD")
NEW_ESC=$(escape_sed "$NEW")

# Find candidate files: tracked .md plus root CLAUDE.md
mapfile -t FILES < <(git ls-files '*.md' 'CLAUDE.md' 2>/dev/null | sort -u)

if [[ ${#FILES[@]} -eq 0 ]]; then
  echo "No tracked .md files found." >&2
  exit 1
fi

CHANGED=0
for f in "${FILES[@]}"; do
  if grep -q "](${OLD}" "$f" 2>/dev/null; then
    if [[ $DRY_RUN -eq 1 ]]; then
      echo "WOULD UPDATE: $f"
      grep -n "](${OLD}" "$f" | head -3 | sed 's/^/    /'
    else
      sed -i.bak "s|](${OLD_ESC}|](${NEW_ESC}|g" "$f"
      rm -f "${f}.bak"
      echo "UPDATED: $f"
    fi
    CHANGED=$((CHANGED + 1))
  fi
done

echo ""
if [[ $DRY_RUN -eq 1 ]]; then
  echo "Dry run complete. $CHANGED file(s) would be modified."
else
  echo "Done. $CHANGED file(s) modified."
  echo "Review with: git diff --stat"
fi
