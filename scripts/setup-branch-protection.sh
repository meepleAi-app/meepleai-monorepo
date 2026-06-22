#!/bin/bash
# Setup Branch Protection Rules for MeepleAI Monorepo
# Requires: gh CLI authenticated (gh auth login)

set -e

REPO="meepleAi-app/meepleai-monorepo"

echo "🔧 Setting up branch protection rules for $REPO..."
echo ""

# Check if gh CLI is installed
if ! command -v gh &> /dev/null; then
    echo "❌ GitHub CLI (gh) not found"
    echo "Install from: https://cli.github.com/"
    echo ""
    echo "Or configure manually at:"
    echo "https://github.com/$REPO/settings/branches"
    exit 1
fi

# Check authentication
if ! gh auth status &> /dev/null; then
    echo "❌ Not authenticated with GitHub"
    echo "Run: gh auth login"
    exit 1
fi

echo "✅ GitHub CLI authenticated"
echo ""

# Function to create/update branch protection
#
# Required-status-check contexts (2026-05-25, PR #1530):
# - `ci-success` is the single aggregate gate for ci.yml (PRs into main /
#   main-staging). It rolls up every ci.yml job, so individual job contexts
#   must NOT be listed here — they churn on refactor (e.g. the 'frontend' job
#   was split into frontend-lint/typecheck/test) and a stale context becomes a
#   check that never reports, permanently blocking merges.
# - The previously-listed `frontend / Frontend - Build & Test` and
#   `backend / Backend - Build & Test` contexts were already non-existent
#   (the jobs are 'frontend'/'backend-unit', and ci.yml does not run on main-dev
#   PRs at all — dev-fast.yml does). Removed to prevent merge deadlock on re-run.
# - main-dev (development) gates on `validate-source-branch` only here; its
#   functional gates (Frontend Fast / Backend Fast from dev-fast.yml) plus
#   GitGuardian are managed in GitHub settings, not provisioned by this script.
setup_protection() {
    local BRANCH=$1
    local LEVEL=$2

    echo "🔧 Configuring $BRANCH ($LEVEL)..."

    case $LEVEL in
        "production")
            # Main branch - Maximum protection
            gh api -X PUT "repos/$REPO/branches/$BRANCH/protection" \
                --input - <<EOF
{
  "required_status_checks": {
    "strict": true,
    "contexts": [
      "validate-source-branch",
      "ci-success"
    ]
  },
  "enforce_admins": true,
  "required_pull_request_reviews": {
    "dismiss_stale_reviews": true,
    "require_code_owner_reviews": false,
    "required_approving_review_count": 1
  },
  "restrictions": null,
  "required_linear_history": true,
  "allow_force_pushes": false,
  "allow_deletions": false,
  "block_creations": false,
  "required_conversation_resolution": true
}
EOF
            ;;

        "staging")
            # Main-staging - Medium protection
            gh api -X PUT "repos/$REPO/branches/$BRANCH/protection" \
                --input - <<EOF
{
  "required_status_checks": {
    "strict": false,
    "contexts": [
      "validate-source-branch",
      "ci-success"
    ]
  },
  "enforce_admins": false,
  "required_pull_request_reviews": null,
  "restrictions": null,
  "required_linear_history": false,
  "allow_force_pushes": true,
  "allow_deletions": false,
  "block_creations": false,
  "required_conversation_resolution": false
}
EOF
            ;;

        "development")
            # Main-dev - Minimal protection
            gh api -X PUT "repos/$REPO/branches/$BRANCH/protection" \
                --input - <<EOF
{
  "required_status_checks": {
    "strict": false,
    "contexts": [
      "validate-source-branch"
    ]
  },
  "enforce_admins": false,
  "required_pull_request_reviews": null,
  "restrictions": null,
  "required_linear_history": false,
  "allow_force_pushes": true,
  "allow_deletions": false,
  "block_creations": false,
  "required_conversation_resolution": false
}
EOF
            ;;
    esac

    if [ $? -eq 0 ]; then
        echo "✅ $BRANCH protection configured"
    else
        echo "❌ Failed to configure $BRANCH"
    fi
    echo ""
}

# Setup all branches
setup_protection "main" "production"
setup_protection "main-staging" "staging"
setup_protection "main-dev" "development"

echo "✅ Branch protection setup complete!"
echo ""
echo "Verify at: https://github.com/$REPO/settings/branches"
