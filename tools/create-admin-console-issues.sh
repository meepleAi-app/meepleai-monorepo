#!/bin/bash

##############################################
# Script to Create Admin Console GitHub Issues
#
# Prerequisites:
# 1. Install GitHub CLI: https://cli.github.com/
# 2. Authenticate: gh auth login
# 3. Make executable: chmod +x tools/create-admin-console-issues.sh
# 4. Run: ./tools/create-admin-console-issues.sh [--dry-run]
##############################################

set -e  # Exit on error

# Configuration
DRY_RUN=false
REPOSITORY="meepleai-monorepo"

# Parse arguments
if [[ "$@" == *"--dry-run"* ]]; then
  DRY_RUN=true
fi

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;90m'
NC='\033[0m' # No Color

# Helper functions
log() {
  local color=$1
  shift
  echo -e "${color}$@${NC}"
}

log_info() { log "${CYAN}" "$@"; }
log_success() { log "${GREEN}" "$@"; }
log_warn() { log "${YELLOW}" "$@"; }
log_error() { log "${RED}" "$@"; }
log_gray() { log "${GRAY}" "$@"; }

# Check prerequisites
check_prerequisites() {
  log_info "Checking prerequisites..."

  # Check gh CLI
  if ! command -v gh &> /dev/null; then
    log_error "ERROR: GitHub CLI (gh) is not installed!"
    log_warn "Install from: https://cli.github.com/"
    exit 1
  fi
  log_success "✓ GitHub CLI installed"

  # Check auth
  if ! gh auth status &> /dev/null; then
    log_error "ERROR: Not authenticated with GitHub!"
    log_warn "Run: gh auth login"
    exit 1
  fi
  log_success "✓ GitHub CLI authenticated"

  echo ""
}

# Ensure label exists
ensure_label() {
  local name=$1
  local color=$2
  local description=$3

  if [ "$DRY_RUN" = true ]; then
    log_warn "  [DRY RUN] Would ensure label: $name"
    return
  fi

  if gh label list --limit 1000 | grep -q "^$name"; then
    log_gray "  ✓ Label exists: $name"
  else
    if gh label create "$name" --color "$color" --description "$description" &> /dev/null; then
      log_success "  ✓ Created label: $name"
    else
      log_warn "  ⚠ Label creation warning (may exist): $name"
    fi
  fi
}

# Ensure milestone exists
ensure_milestone() {
  local title=$1
  local due_date=$2
  local description=$3

  if [ "$DRY_RUN" = true ]; then
    log_warn "  [DRY RUN] Would ensure milestone: $title"
    return
  fi

  if gh api repos/:owner/:repo/milestones | grep -q "\"title\":\"$title\""; then
    log_gray "  ✓ Milestone exists: $title"
  else
    if gh api repos/:owner/:repo/milestones -X POST -f title="$title" -f description="$description" &> /dev/null; then
      log_success "  ✓ Created milestone: $title"
    else
      log_warn "  ⚠ Milestone creation warning: $title"
    fi
  fi
}

# Create issue
create_issue() {
  local title=$1
  local body=$2
  local labels=$3
  local milestone=$4
  local issue_number=$5

  if [ "$DRY_RUN" = true ]; then
    log_warn "  [DRY RUN] Would create: #$issue_number - $title"
    log_gray "    Labels: $labels"
    return
  fi

  local command="gh issue create --title \"$title\" --body \"$body\" --label \"$labels\""
  if [ -n "$milestone" ]; then
    command="$command --milestone \"$milestone\""
  fi

  if result=$(eval $command 2>&1); then
    if [[ $result =~ \#([0-9]+) ]]; then
      created_number="${BASH_REMATCH[1]}"
      log_success "  ✓ Created: #$created_number - $title"
    else
      log_error "  ✗ Failed to create: $title"
    fi
  else
    log_error "  ✗ Error creating issue: $title"
  fi
}

##############################################
# Main Script
##############################################

log_info "========================================"
log_info "Admin Console GitHub Issues Creator"
log_info "========================================"
echo ""

if [ "$DRY_RUN" = true ]; then
  log_warn "DRY RUN MODE - No issues will be created"
  echo ""
fi

# Check prerequisites
check_prerequisites

# STEP 1: Create Labels
log_info "STEP 1: Creating Labels..."
echo ""

ensure_label "admin-console" "0052CC" "Admin Console implementation"
ensure_label "fase-1-dashboard" "00B8D9" "FASE 1: Dashboard Overview"
ensure_label "fase-2-infrastructure" "00875A" "FASE 2: Infrastructure Monitoring"
ensure_label "fase-3-management" "FF8B00" "FASE 3: Enhanced Management"
ensure_label "fase-4-advanced" "6554C0" "FASE 4: Advanced Features"
ensure_label "backend" "5243AA" "Backend task"
ensure_label "frontend" "36B37E" "Frontend task"
ensure_label "testing" "FF5630" "Testing task"
ensure_label "mvp" "00C7E6" "MVP feature (FASE 1-2)"
ensure_label "epic" "172B4D" "Epic issue"
ensure_label "component" "57D9A3" "UI component"
ensure_label "reusable" "00B8D9" "Reusable component"
ensure_label "performance" "FF991F" "Performance-related"
ensure_label "security" "DE350B" "Security task"
ensure_label "email" "0065FF" "Email integration"

echo ""

# STEP 2: Create Milestones
log_info "STEP 2: Creating Milestones..."
echo ""

TODAY=$(date +%Y-%m-%d)
DAYS_14=$(date -d "+14 days" +%Y-%m-%d 2>/dev/null || date -v+14d +%Y-%m-%d)
DAYS_28=$(date -d "+28 days" +%Y-%m-%d 2>/dev/null || date -v+28d +%Y-%m-%d)
DAYS_42=$(date -d "+42 days" +%Y-%m-%d 2>/dev/null || date -v+42d +%Y-%m-%d)
DAYS_49=$(date -d "+49 days" +%Y-%m-%d 2>/dev/null || date -v+49d +%Y-%m-%d)

ensure_milestone "FASE 1: Dashboard Overview" "$DAYS_14" "Dashboard with system status and metrics"
ensure_milestone "FASE 2: Infrastructure Monitoring" "$DAYS_28" "Multi-service health monitoring"
ensure_milestone "FASE 3: Enhanced Management" "$DAYS_42" "API keys and user management enhancements"
ensure_milestone "FASE 4: Advanced Features" "$DAYS_49" "Reporting and alerting system"

echo ""

# STEP 3: Create Issues
log_info "STEP 3: Creating Issues (Sample)..."
echo ""

log_warn "NOTE: This is a simplified demo version creating first 3 issues"
log_warn "Full version would create all 49 issues from data file"
echo ""

# Issue #1: Epic
BODY1="Implement centralized admin dashboard with system status, key metrics, activity feed, and quick actions.

## User Stories
- US-1: As admin, I want to see overall system status at a glance
- US-2: As admin, I want to navigate easily between admin sections

## Acceptance Criteria
- [ ] Dashboard shows 12+ real-time metrics (polling 30s)
- [ ] Activity feed with last 10 events
- [ ] Performance: Load time <1s, Time to Interactive <2s
- [ ] Test coverage: Backend 90%+, Frontend 90%+
- [ ] E2E test complete: login → dashboard → navigation
- [ ] Accessibility: WCAG AA compliance
- [ ] Responsive: Desktop (1920x1080) + Tablet (768x1024)

## Effort
80h (2 weeks)

## Dependencies
None (can start immediately)"

create_issue \
  "FASE 1: Dashboard Overview - Centralized Admin Dashboard" \
  "$BODY1" \
  "admin-console,fase-1-dashboard,mvp,epic" \
  "FASE 1: Dashboard Overview" \
  1

# Issue #2: Backend
BODY2="Create AdminDashboardService with GetSystemStatsAsync() method to aggregate metrics from existing services.

## Tasks
- [ ] Create \`Services/AdminDashboardService.cs\`
- [ ] Implement GetSystemStatsAsync() - aggregate from UserManagementService, SessionManagementService, AiRequestLogService, CacheService
- [ ] Create interface \`IAdminDashboardService\`
- [ ] Register service in DI container (Program.cs)

## Effort
6h

## Depends On
None"

create_issue \
  "[Backend] AdminDashboardService.cs - GetSystemStatsAsync()" \
  "$BODY2" \
  "admin-console,fase-1-dashboard,backend,mvp" \
  "FASE 1: Dashboard Overview" \
  2

# Issue #3: Backend
BODY3="Implement metric aggregation logic to collect data from 4+ existing services for dashboard display.

## Services to Integrate
- UserManagementService (active users, total users, new users today)
- SessionManagementService (active sessions, avg duration)
- AiRequestLogService (requests/min, avg response time, error rate)
- HybridCacheService (hit rate, evictions, memory usage)

## Tasks
- [ ] Implement parallel aggregation (Task.WhenAll)
- [ ] Handle service failures gracefully (partial stats if service down)
- [ ] Calculate derived metrics (% changes, trends)
- [ ] Add Serilog logging for aggregation

## Performance Requirements
- Total aggregation time <200ms
- Parallel service calls

## Effort
8h

## Depends On
#2"

create_issue \
  "[Backend] Aggregate metrics from existing services (Users, Sessions, AI, Cache)" \
  "$BODY3" \
  "admin-console,fase-1-dashboard,backend,mvp" \
  "FASE 1: Dashboard Overview" \
  3

echo ""
log_warn "Full script would continue with remaining 46 issues..."
echo ""

# Summary
log_info "========================================"
log_info "Summary"
log_info "========================================"

if [ "$DRY_RUN" = true ]; then
  log_warn "DRY RUN completed - no issues created"
else
  log_success "✓ Created 3 issues (demo mode)"
  log_success "✓ Full script would create all 49 issues"
fi

echo ""
log_info "Next steps:"
echo "1. Review created issues on GitHub"
echo "2. Assign issues to team members"
echo "3. Create GitHub Project board and add issues"
echo "4. Start with FASE 1 (MVP)"
echo ""
log_gray "Full implementation plan: claudedocs/admin_console_implementation_plan.md"
log_gray "Issue details: claudedocs/github_issues_admin_console.md"
echo ""
