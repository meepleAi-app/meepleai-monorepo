#!/bin/bash

# GitHub Project Import Script
# Usage: ./import-to-project.sh
# Prerequisites: gh auth refresh -s project -h github.com

set -e

PROJECT_NUMBER=2
OWNER="DegrassiAaron"
REPO="DegrassiAaron/meepleai-monorepo"

echo "=========================================="
echo "GitHub Project Import Script"
echo "=========================================="
echo ""
echo "Project: https://github.com/users/$OWNER/projects/$PROJECT_NUMBER"
echo "Repository: $REPO"
echo ""

# Step 1: Get Project ID
echo "[1/4] Fetching project ID..."
PROJECT_ID=$(gh api graphql -f query='
  query {
    user(login: "'$OWNER'") {
      projectV2(number: '$PROJECT_NUMBER') {
        id
        title
      }
    }
  }
' --jq '.data.user.projectV2.id')

if [ -z "$PROJECT_ID" ]; then
  echo "[ERROR] Could not fetch project ID. Check permissions:"
  echo "  gh auth refresh -s project -h github.com"
  exit 1
fi

echo "✓ Project ID: $PROJECT_ID"
echo ""

# Step 2: Get Project Field IDs
echo "[2/4] Fetching project fields..."
gh api graphql -f query='
  query {
    node(id: "'$PROJECT_ID'") {
      ... on ProjectV2 {
        fields(first: 20) {
          nodes {
            ... on ProjectV2Field {
              id
              name
            }
            ... on ProjectV2SingleSelectField {
              id
              name
              options {
                id
                name
              }
            }
          }
        }
      }
    }
  }
' > /tmp/project_fields.json

echo "✓ Fields retrieved"
echo ""

# Step 3: Import issues by milestone
echo "[3/4] Adding issues to project..."

# Define milestones to import
MILESTONES=(
  "MVP Sprint 1"
  "MVP Sprint 2"
  "MVP Sprint 3"
  "MVP Sprint 4"
  "MVP Sprint 5"
  "Month 6: Italian UI"
  "Month 5: Golden Dataset"
  "Month 4: Quality Framework"
  "FASE 1: Dashboard Overview"
  "FASE 2: Infrastructure Monitoring"
)

TOTAL_ADDED=0
TOTAL_FAILED=0

for MILESTONE in "${MILESTONES[@]}"; do
  echo ""
  echo "Processing: $MILESTONE"
  echo "---"

  # Get issues for this milestone
  gh issue list --repo $REPO \
    --limit 50 \
    --state open \
    --milestone "$MILESTONE" \
    --json number,title,id | \
  jq -r '.[] | @json' | while read -r issue_json; do

    ISSUE_NUM=$(echo "$issue_json" | jq -r '.number')
    ISSUE_TITLE=$(echo "$issue_json" | jq -r '.title')
    ISSUE_ID=$(echo "$issue_json" | jq -r '.id')

    echo -n "  #$ISSUE_NUM: $ISSUE_TITLE ... "

    # Add to project
    RESULT=$(gh api graphql -f query='
      mutation {
        addProjectV2ItemById(input: {
          projectId: "'$PROJECT_ID'"
          contentId: "'$ISSUE_ID'"
        }) {
          item {
            id
          }
        }
      }
    ' 2>&1)

    if echo "$RESULT" | grep -q '"item"'; then
      echo "✓"
      ((TOTAL_ADDED++))
    else
      echo "✗ (may already exist)"
      ((TOTAL_FAILED++))
    fi

    # Rate limiting
    sleep 0.3
  done
done

echo ""
echo "[4/4] Import Summary"
echo "=========================================="
echo "Total added: $TOTAL_ADDED"
echo "Skipped/Failed: $TOTAL_FAILED"
echo ""
echo "✓ Import complete!"
echo ""
echo "Next steps:"
echo "1. Visit https://github.com/users/$OWNER/projects/$PROJECT_NUMBER"
echo "2. Configure custom fields (Priority, Track, Size)"
echo "3. Set up project views (Board, Timeline, Team Tracks)"
echo "4. Bulk assign priorities and tracks"
echo ""
