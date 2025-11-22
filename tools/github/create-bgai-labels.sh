#!/bin/bash

# Board Game AI - Labels Creation Script
# Creates GitHub labels for Board Game AI issue tracking
#
# Usage: bash tools/create-bgai-labels.sh

set -e

echo "🏷️  Creating Board Game AI Labels..."
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Main category
gh label create "board-game-ai" \
  --description "Board Game AI features" \
  --color "0E8A16" \
  --force

# Monthly phases
gh label create "month-1" \
  --description "Month 1: PDF Processing Pipeline" \
  --color "D4C5F9" \
  --force

gh label create "month-2" \
  --description "Month 2: LLM Integration" \
  --color "BFD4F2" \
  --force

gh label create "month-3" \
  --description "Month 3: Multi-Model Validation" \
  --color "C5DEF5" \
  --force

gh label create "month-4" \
  --description "Month 4: Quality Framework + Frontend" \
  --color "F9D0C4" \
  --force

gh label create "month-5" \
  --description "Month 5: Golden Dataset + Q&A Interface" \
  --color "FEF2C0" \
  --force

gh label create "month-6" \
  --description "Month 6: Italian UI + Completion" \
  --color "BFDADC" \
  --force

# Technical areas
gh label create "pdf" \
  --description "PDF processing related" \
  --color "0075CA" \
  --force

gh label create "llm" \
  --description "LLM integration related" \
  --color "7057FF" \
  --force

gh label create "validation" \
  --description "Multi-model validation" \
  --color "E99695" \
  --force

gh label create "quality" \
  --description "Quality framework and metrics" \
  --color "FBCA04" \
  --force

gh label create "i18n" \
  --description "Internationalization (Italian)" \
  --color "C2E0C6" \
  --force

gh label create "orchestration" \
  --description "Service orchestration" \
  --color "D93F0B" \
  --force

gh label create "microservice" \
  --description "Microservice implementation" \
  --color "5319E7" \
  --force

gh label create "python" \
  --description "Python code" \
  --color "3572A5" \
  --force

gh label create "e2e" \
  --description "End-to-end testing" \
  --color "D4C5F9" \
  --force

gh label create "code-review" \
  --description "Code review needed" \
  --color "FFA500" \
  --force

echo ""
echo "✅ Labels created successfully"
echo ""
echo "View labels: https://github.com/$(git config --get remote.origin.url | sed 's/.*github.com[:/]\(.*\)\.git/\1/')/labels"
