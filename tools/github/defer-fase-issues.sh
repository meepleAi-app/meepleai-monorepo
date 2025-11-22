#!/bin/bash
# Defer FASE 1-4 issues to Aug 2026+ due to Board Game AI priority

echo "Creating labels..."
gh label create "deferred" --description "Deferred to future phase" --color "d4c5f9" 2>/dev/null || true
gh label create "board-game-ai" --description "Board Game AI features" --color "0e8a16" 2>/dev/null || true

echo "Adding comments to FASE issues..."
FASE_ISSUES="890 891 892 893 894 895 896 897 898 899 900 901 902 903 904 905 906 907 908 909 910 911 912 913 914 915 916 917 918 919 920 921 922"

for issue in $FASE_ISSUES; do
  echo "Processing issue #$issue..."
  gh issue comment $issue --body "⏸️ **DEFERRED to Aug 2026+**: Strategic priority shift to Board Game AI (Italian market first-mover advantage, €120K ARR potential).

**Rationale**: Board Game AI requires full team focus (Jan-Jun 2025) for 6-month MVP timeline. FASE features remain important but not time-critical.

**New Timeline**: Post-Board Game AI Phase 2 (August 2026 or later)

**See**: \`docs/org/project-prioritization-2025.md\` for full decision rationale.

**Questions?**: Contact Product Lead or reply in #product-strategy channel." 2>/dev/null
done

echo "Deferral comments added to $( echo $FASE_ISSUES | wc -w ) issues."
echo "✅ FASE issues deferred successfully!"
