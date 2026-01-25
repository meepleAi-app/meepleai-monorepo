#!/bin/bash
# MeepleAI Git Workflow Helper
# Quick workflow commands for solo developer

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Helper functions
print_success() {
    echo -e "${GREEN}✅ $1${NC}"
}

print_error() {
    echo -e "${RED}❌ $1${NC}"
}

print_info() {
    echo -e "${BLUE}ℹ️  $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

# Ensure git repo
if ! git rev-parse --is-inside-work-tree > /dev/null 2>&1; then
    print_error "Not inside a git repository"
    exit 1
fi

case "$1" in
    "feature")
        if [ -z "$2" ]; then
            print_error "Usage: $0 feature <feature-name>"
            echo "Example: $0 feature add-game-search"
            exit 1
        fi

        print_info "Creating feature branch from main-dev..."
        git checkout main-dev
        git pull origin main-dev
        git checkout -b "feature/$2"
        print_success "Created feature branch: feature/$2"
        print_info "Start working! When done, run: $0 merge $2"
        ;;

    "merge")
        if [ -z "$2" ]; then
            print_error "Usage: $0 merge <feature-name>"
            echo "Example: $0 merge add-game-search"
            exit 1
        fi

        BRANCH="feature/$2"
        CURRENT=$(git branch --show-current)

        if [ "$CURRENT" != "$BRANCH" ]; then
            print_warning "Not on branch $BRANCH, switching..."
            git checkout "$BRANCH" || exit 1
        fi

        print_info "Pushing feature branch..."
        git push -u origin "$BRANCH" || exit 1

        print_info "Merging to main-dev..."
        git checkout main-dev
        git pull origin main-dev
        git merge "$BRANCH" --no-ff -m "feat: merge $BRANCH"
        git push origin main-dev

        print_info "Cleaning up feature branch..."
        git branch -D "$BRANCH"
        git push origin --delete "$BRANCH"

        print_success "Feature $2 merged to main-dev and cleaned up!"
        ;;

    "staging")
        print_info "Promoting main-dev to staging..."

        # Confirm action
        read -p "Run full test suite first? (y/n) " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            print_info "Running backend tests..."
            cd apps/api/src/Api && dotnet test || exit 1
            cd ../../../../

            print_info "Running frontend tests..."
            cd apps/web
            pnpm test || exit 1
            pnpm test:e2e || exit 1
            cd ../../

            print_success "All tests passed!"
        fi

        git checkout main-dev && git pull origin main-dev
        git checkout main-staging && git pull origin main-staging
        git merge main-dev --no-ff -m "chore(release): promote main-dev to staging"
        git push origin main-staging

        print_success "Merged main-dev → main-staging"
        print_info "CI/CD pipeline triggered. Monitor at GitHub Actions."
        print_warning "Validate staging environment before releasing to production!"
        ;;

    "release")
        if [ -z "$2" ]; then
            print_error "Usage: $0 release <version> [title]"
            echo "Example: $0 release v1.2.0 \"Game Search Feature\""
            exit 1
        fi

        VERSION="$2"
        TITLE="${3:-Release $VERSION}"

        print_info "Creating release PR: main-staging → main"

        # Check if gh CLI is installed
        if ! command -v gh &> /dev/null; then
            print_error "GitHub CLI (gh) not found. Install from: https://cli.github.com/"
            exit 1
        fi

        # Create PR
        gh pr create --base main --head main-staging \
            --title "$TITLE" \
            --body "## 🚀 Release $VERSION

**Pre-Release Checklist:**
- [ ] All CI/CD checks passed on staging
- [ ] Staging environment validated
- [ ] Database migrations tested
- [ ] Secrets rotated (if needed)
- [ ] Rollback plan documented

**Test Coverage:**
- Backend: Check coverage report
- Frontend: Check coverage report

**Generated:** $(date)
**Author:** $(git config user.name)

🤖 Generated with git-workflow.sh"

        print_success "Created release PR: main-staging → main"
        print_info "Review PR and merge when ready"
        print_warning "After merge, don't forget to tag the release!"
        ;;

    "tag")
        if [ -z "$2" ]; then
            print_error "Usage: $0 tag <version> [message]"
            echo "Example: $0 tag v1.2.0 \"Game Search Release\""
            exit 1
        fi

        VERSION="$2"
        MESSAGE="${3:-Release $VERSION}"

        print_info "Tagging release on main..."
        git checkout main
        git pull origin main
        git tag -a "$VERSION" -m "$MESSAGE"
        git push origin "$VERSION"

        print_success "Tagged release: $VERSION"
        print_info "Now syncing main-dev..."

        git checkout main-dev
        git merge main --no-ff -m "chore: sync main-dev with production release $VERSION"
        git push origin main-dev

        print_success "main-dev synced with production!"
        ;;

    "sync")
        print_info "Syncing main-dev with production..."
        git checkout main && git pull origin main
        git checkout main-dev
        git merge main --no-ff -m "chore: sync main-dev with production"
        git push origin main-dev
        print_success "main-dev synced with main"
        ;;

    "hotfix")
        if [ -z "$2" ]; then
            print_error "Usage: $0 hotfix <description>"
            echo "Example: $0 hotfix critical-auth-bug"
            exit 1
        fi

        BRANCH="hotfix/$2"

        print_warning "Creating HOTFIX branch from production (main)..."
        git checkout main
        git pull origin main
        git checkout -b "$BRANCH"

        print_success "Created hotfix branch: $BRANCH"
        print_warning "After fixing, run: $0 hotfix-merge $2"
        ;;

    "hotfix-merge")
        if [ -z "$2" ]; then
            print_error "Usage: $0 hotfix-merge <description>"
            exit 1
        fi

        BRANCH="hotfix/$2"
        CURRENT=$(git branch --show-current)

        if [ "$CURRENT" != "$BRANCH" ]; then
            print_error "Not on hotfix branch $BRANCH"
            exit 1
        fi

        print_info "Testing hotfix..."
        cd apps/api/src/Api && dotnet test || exit 1
        cd ../../../../
        cd apps/web && pnpm test || exit 1
        cd ../../

        print_info "Pushing hotfix branch..."
        git push -u origin "$BRANCH"

        print_info "Creating PR to main-staging first..."
        gh pr create --base main-staging --head "$BRANCH" \
            --title "Hotfix: $2" \
            --body "## 🚨 Hotfix

**Issue:** $2

**Testing:**
- [x] Unit tests passed
- [ ] Staging validated

**Urgency:** High

Generated: $(date)" || exit 1

        print_success "Hotfix PR created: $BRANCH → main-staging"
        print_warning "After staging validation, create PR: main-staging → main"
        print_info "Then backport to main-dev with: git cherry-pick <commit-sha>"
        ;;

    "status")
        print_info "Git Workflow Status Report"
        echo ""

        CURRENT_BRANCH=$(git branch --show-current)
        echo "📍 Current Branch: $CURRENT_BRANCH"
        echo ""

        echo "🌿 Branch Status:"
        git fetch origin --quiet

        for branch in main main-staging main-dev frontend-dev backend-dev; do
            if git show-ref --verify --quiet "refs/heads/$branch" || git show-ref --verify --quiet "refs/remotes/origin/$branch"; then
                LOCAL_COMMIT=$(git rev-parse "$branch" 2>/dev/null || echo "N/A")
                REMOTE_COMMIT=$(git rev-parse "origin/$branch" 2>/dev/null || echo "N/A")

                if [ "$LOCAL_COMMIT" = "$REMOTE_COMMIT" ]; then
                    STATUS="✅ Synced"
                elif [ "$LOCAL_COMMIT" = "N/A" ]; then
                    STATUS="⚠️  Local missing"
                elif [ "$REMOTE_COMMIT" = "N/A" ]; then
                    STATUS="⚠️  Remote missing"
                else
                    STATUS="⚠️  Out of sync"
                fi

                echo "  $branch: $STATUS"
            else
                echo "  $branch: ❌ Not found"
            fi
        done

        echo ""
        echo "🔀 Recent Commits (main-dev):"
        git log origin/main-dev --oneline -n 5 2>/dev/null || echo "  No commits"

        echo ""
        echo "📦 Uncommitted Changes:"
        if git diff --quiet && git diff --cached --quiet; then
            print_success "Working directory clean"
        else
            print_warning "You have uncommitted changes"
            git status --short
        fi
        ;;

    "help"|"--help"|"-h"|"")
        echo "MeepleAI Git Workflow Helper"
        echo ""
        echo "Usage: $0 <command> [arguments]"
        echo ""
        echo "Commands:"
        echo "  feature <name>         - Create feature branch from main-dev"
        echo "  merge <name>           - Merge feature to main-dev and cleanup"
        echo "  staging                - Promote main-dev to staging (with tests)"
        echo "  release <ver> [title]  - Create release PR to production"
        echo "  tag <ver> [msg]        - Tag release and sync branches"
        echo "  sync                   - Sync main-dev with production"
        echo "  hotfix <desc>          - Create hotfix branch from main"
        echo "  hotfix-merge <desc>    - Merge hotfix to staging and create PR"
        echo "  status                 - Show branch status and recent commits"
        echo "  help                   - Show this help message"
        echo ""
        echo "Examples:"
        echo "  $0 feature add-game-search"
        echo "  $0 merge add-game-search"
        echo "  $0 staging"
        echo "  $0 release v1.2.0 \"Game Search Feature\""
        echo "  $0 tag v1.2.0"
        echo "  $0 sync"
        echo "  $0 hotfix critical-auth-bug"
        echo "  $0 status"
        echo ""
        echo "Workflow:"
        echo "  1. feature <name>  → work → commit → merge <name>"
        echo "  2. staging         → validate → release <ver>"
        echo "  3. tag <ver>       → production deployed!"
        echo ""
        exit 0
        ;;

    *)
        print_error "Unknown command: $1"
        echo "Run '$0 help' for usage information"
        exit 1
        ;;
esac
