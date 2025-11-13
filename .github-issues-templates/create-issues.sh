#!/bin/bash
#
# create-issues.sh - Create GitHub issues from templates
#
# Usage:
#   ./create-issues.sh [--sprint N] [--dry-run]
#
# Options:
#   --sprint N    Create only Sprint N issues (1, 2, or 3)
#   --dry-run     Show what would be created without actually creating
#   --help        Show this help message
#

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default options
DRY_RUN=false
SPRINT_FILTER=""

# Parse arguments
while [[ $# -gt 0 ]]; do
  case $1 in
    --sprint)
      SPRINT_FILTER="$2"
      shift 2
      ;;
    --dry-run)
      DRY_RUN=true
      shift
      ;;
    --help)
      head -n 15 "$0" | tail -n 13
      exit 0
      ;;
    *)
      echo -e "${RED}Unknown option: $1${NC}"
      echo "Use --help for usage information"
      exit 1
      ;;
  esac
done

# Check if gh CLI is installed
if ! command -v gh &> /dev/null; then
  echo -e "${RED}Error: GitHub CLI (gh) is not installed${NC}"
  echo "Install it from: https://cli.github.com/"
  exit 1
fi

# Check if authenticated
if ! gh auth status &> /dev/null; then
  echo -e "${RED}Error: Not authenticated with GitHub CLI${NC}"
  echo "Run: gh auth login"
  exit 1
fi

# Function to extract title from markdown file
extract_title() {
  local file="$1"
  # Extract first h1 heading (line starting with #)
  grep -m 1 "^# " "$file" | sed 's/^# //'
}

# Function to create issue from file
create_issue() {
  local file="$1"
  local sprint="$2"
  local priority="$3"

  local title=$(extract_title "$file")
  local filename=$(basename "$file")

  echo -e "${BLUE}Processing: $filename${NC}"
  echo "  Title: $title"

  if [ "$DRY_RUN" = true ]; then
    echo -e "  ${YELLOW}[DRY RUN] Would create issue with labels: frontend, refactor, $sprint, $priority${NC}"
  else
    # Create the issue
    gh issue create \
      --title "$title" \
      --body-file "$file" \
      --label "frontend,refactor,$sprint,$priority" \
      --assignee "@me"

    if [ $? -eq 0 ]; then
      echo -e "  ${GREEN}✓ Created successfully${NC}"
    else
      echo -e "  ${RED}✗ Failed to create${NC}"
    fi
  fi

  echo ""
}

# Function to process sprint directory
process_sprint() {
  local sprint_dir="$1"
  local sprint_label="$2"
  local priority_label="$3"

  if [ ! -d "$sprint_dir" ]; then
    echo -e "${YELLOW}Warning: Directory $sprint_dir not found${NC}"
    return
  fi

  local count=$(find "$sprint_dir" -name "*.md" -type f | wc -l)

  echo -e "${GREEN}=== $sprint_label ($count issues) ===${NC}"
  echo ""

  for file in "$sprint_dir"/*.md; do
    if [ -f "$file" ]; then
      create_issue "$file" "$sprint_label" "$priority_label"
    fi
  done
}

# Main execution
echo -e "${BLUE}╔════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║   MeepleAI Frontend Refactor Issue Creator    ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════════╝${NC}"
echo ""

if [ "$DRY_RUN" = true ]; then
  echo -e "${YELLOW}DRY RUN MODE - No issues will be created${NC}"
  echo ""
fi

# Change to script directory
cd "$(dirname "$0")"

# Process sprints based on filter
if [ -z "$SPRINT_FILTER" ] || [ "$SPRINT_FILTER" = "1" ]; then
  process_sprint "sprint-1-critical" "sprint-1" "priority-critical"
fi

if [ -z "$SPRINT_FILTER" ] || [ "$SPRINT_FILTER" = "2" ]; then
  process_sprint "sprint-2-important" "sprint-2" "priority-high"
fi

if [ -z "$SPRINT_FILTER" ] || [ "$SPRINT_FILTER" = "3" ]; then
  process_sprint "sprint-3-nice-to-have" "sprint-3" "priority-medium"
fi

# Summary
echo -e "${GREEN}╔════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║                 Summary                        ║${NC}"
echo -e "${GREEN}╚════════════════════════════════════════════════╝${NC}"
echo ""

if [ "$DRY_RUN" = true ]; then
  echo -e "${YELLOW}DRY RUN COMPLETE - No issues were created${NC}"
  echo ""
  echo "To actually create the issues, run without --dry-run:"
  echo "  ./create-issues.sh"
else
  echo -e "${GREEN}Issues created successfully!${NC}"
  echo ""
  echo "View all issues:"
  echo "  gh issue list --label frontend,refactor"
  echo ""
  echo "Create a project board:"
  echo "  gh project create --title 'Frontend Refactor' --body 'Frontend refactoring roadmap'"
fi

echo ""
echo -e "${BLUE}Next steps:${NC}"
echo "  1. Review created issues on GitHub"
echo "  2. Create a project board to track progress"
echo "  3. Assign issues to team members"
echo "  4. Start with Sprint 1 (critical) issues"
echo ""
