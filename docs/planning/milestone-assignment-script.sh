#!/bin/bash
# Milestone Triage Bulk Assignment Script
# Date: 2025-11-13
# Repository: DegrassiAaron/meepleai-monorepo
# Total issues to assign: 15

set -e  # Exit on error

echo "🎯 Starting milestone triage assignment..."
echo "Repository: DegrassiAaron/meepleai-monorepo"
echo ""

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# ============================================================================
# Group A: Frontend Refactor Issues → MVP Sprint 1-3 (13 issues)
# ============================================================================

echo "${BLUE}📦 Assigning Frontend Refactor Issues to MVP Sprint 1-3...${NC}"
echo ""

# MVP Sprint 1 (2 issues) - Critical Priority
echo "${GREEN}→ MVP Sprint 1 (Critical Priority)${NC}"
gh issue edit 1090 --milestone "MVP Sprint 1" --repo DegrassiAaron/meepleai-monorepo
echo "  ✓ #1090: Split ChatProvider into Multiple Contexts"

gh issue edit 1091 --milestone "MVP Sprint 1" --repo DegrassiAaron/meepleai-monorepo
echo "  ✓ #1091: Eliminate Inline Styles and Standardize with Design System"
echo ""

# MVP Sprint 2 (5 issues) - High Priority
echo "${GREEN}→ MVP Sprint 2 (High Priority)${NC}"
gh issue edit 1092 --milestone "MVP Sprint 2" --repo DegrassiAaron/meepleai-monorepo
echo "  ✓ #1092: Mobile-First Responsive Improvements"

gh issue edit 1093 --milestone "MVP Sprint 2" --repo DegrassiAaron/meepleai-monorepo
echo "  ✓ #1093: Optimize Re-renders and Bundle Size"

gh issue edit 1094 --milestone "MVP Sprint 2" --repo DegrassiAaron/meepleai-monorepo
echo "  ✓ #1094: Accessibility Audit and Fixes"

gh issue edit 1095 --milestone "MVP Sprint 2" --repo DegrassiAaron/meepleai-monorepo
echo "  ✓ #1095: Unified Error Handling System"

gh issue edit 1096 --milestone "MVP Sprint 2" --repo DegrassiAaron/meepleai-monorepo
echo "  ✓ #1096: Standardize Loading States"
echo ""

# MVP Sprint 3 (6 issues) - Medium Priority
echo "${GREEN}→ MVP Sprint 3 (Medium Priority)${NC}"
gh issue edit 1097 --milestone "MVP Sprint 3" --repo DegrassiAaron/meepleai-monorepo
echo "  ✓ #1097: Set Up Storybook for Component Documentation"

gh issue edit 1098 --milestone "MVP Sprint 3" --repo DegrassiAaron/meepleai-monorepo
echo "  ✓ #1098: Comprehensive Component Unit Tests"

gh issue edit 1099 --milestone "MVP Sprint 3" --repo DegrassiAaron/meepleai-monorepo
echo "  ✓ #1099: Landing Page Performance and UX"

gh issue edit 1100 --milestone "MVP Sprint 3" --repo DegrassiAaron/meepleai-monorepo
echo "  ✓ #1100: Keyboard Shortcuts System"

gh issue edit 1101 --milestone "MVP Sprint 3" --repo DegrassiAaron/meepleai-monorepo
echo "  ✓ #1101: Advanced Search and Filters"

gh issue edit 1102 --milestone "MVP Sprint 3" --repo DegrassiAaron/meepleai-monorepo
echo "  ✓ #1102: Theme Customization System"
echo ""

# ============================================================================
# Group D: Security/Testing Features → Month 2: LLM Integration (2 issues)
# ============================================================================

echo "${BLUE}🔒 Assigning Testing Infrastructure to Month 2...${NC}"
echo ""

echo "${GREEN}→ Month 2: LLM Integration${NC}"
gh issue edit 841 --milestone "Month 2: LLM Integration" --repo DegrassiAaron/meepleai-monorepo
echo "  ✓ #841: Implement automated accessibility testing with axe-core"

gh issue edit 842 --milestone "Month 2: LLM Integration" --repo DegrassiAaron/meepleai-monorepo
echo "  ✓ #842: Implement automated performance testing with Lighthouse CI"
echo ""

# ============================================================================
# Summary
# ============================================================================

echo ""
echo "✅ ${GREEN}Milestone assignment complete!${NC}"
echo ""
echo "📊 Summary:"
echo "  • MVP Sprint 1: 2 issues assigned"
echo "  • MVP Sprint 2: 5 issues assigned"
echo "  • MVP Sprint 3: 6 issues assigned"
echo "  • Month 2: 2 issues assigned"
echo "  • Total: 15 issues assigned"
echo ""
echo "📋 Remaining without milestone: 19 issues"
echo "  • Infrastructure: 10 issues (#701-709, #936)"
echo "  • Deferred Epics: 7 issues (#844, #926, #931-#935)"
echo "  • Security: 2 issues (#575, #576)"
echo ""
echo "🔍 View milestone triage analysis:"
echo "  cat docs/planning/milestone-triage-analysis.md"
echo ""
