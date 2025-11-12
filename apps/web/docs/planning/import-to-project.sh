#!/bin/bash

PROJECT_NUMBER=2
OWNER="DegrassiAaron"
REPO="DegrassiAaron/meepleai-monorepo"

echo "Fetching project ID..."
PROJECT_ID=$(gh api graphql -f query='
  query {
    user(login: "'$OWNER'") {
      projectV2(number: '$PROJECT_NUMBER') {
        id
      }
    }
  }
' --jq '.data.user.projectV2.id')

echo "Project ID: $PROJECT_ID"
echo "Fetching open issues..."

# Get all open issues
gh issue list --repo $REPO --limit 200 --state open --json number,title,labels,milestone | \
jq -r '.[] | @json' | while read -r issue; do
  ISSUE_NUM=$(echo "$issue" | jq -r '.number')
  TITLE=$(echo "$issue" | jq -r '.title')
  MILESTONE=$(echo "$issue" | jq -r '.milestone.title // "No Milestone"')

  # Determine track
  LABELS=$(echo "$issue" | jq -r '.labels[].name' | tr '\n' ',')

  if echo "$LABELS" | grep -q "frontend"; then
    TRACK="Frontend"
  elif echo "$LABELS" | grep -q "backend"; then
    TRACK="Backend"
  elif echo "$LABELS" | grep -q "testing"; then
    TRACK="Testing"
  else
    TRACK="Other"
  fi

  # Determine priority
  if echo "$TITLE" | grep -qi "\[p1\]"; then
    PRIORITY="P1-Critical"
  elif echo "$LABELS" | grep -q "bug"; then
    PRIORITY="P2-High"
  else
    PRIORITY="P3-Normal"
  fi

  echo "Adding issue #$ISSUE_NUM: $TITLE"
  echo "  Milestone: $MILESTONE | Track: $TRACK | Priority: $PRIORITY"

  # Add to project (this requires project scope)
  gh api graphql -f query='
    mutation {
      addProjectV2ItemById(input: {
        projectId: "'$PROJECT_ID'"
        contentId: "issue_node_id_here"
      }) {
        item {
          id
        }
      }
    }
  ' || echo "  [ERROR] Failed to add issue #$ISSUE_NUM"

  sleep 0.5  # Rate limiting
done

echo "Import complete!"