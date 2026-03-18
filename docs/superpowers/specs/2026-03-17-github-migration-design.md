# GitHub Migration: MeepleAI → meepleAi-app

**Date**: 2026-03-17
**Status**: Approved
**Approach**: B — Mirror Git + Export/Import Issues via API

## Context

The `MeepleAI` GitHub organization has bugs preventing normal usage. All code, issues, secrets, variables, environments, workflows, and self-hosted runner configuration must be transferred to the personal account `meepleAi-app` with zero references to `DegrassiAaron`.

## Source & Destination

| | Source | Destination |
|---|---|---|
| **Owner** | `MeepleAI` (org) | `meepleAi-app` (personal) |
| **Repo** | `meepleai-monorepo` | `meepleai-monorepo` |
| **Visibility** | Private | Public |
| **URL** | `github.com/MeepleAI/meepleai-monorepo` | `github.com/meepleAi-app/meepleai-monorepo` |
| **GHCR** | `ghcr.io/meepleai/...` | `ghcr.io/meepleai-app/...` (lowercase) |

## Migration Inventory

### Git Assets
- **Branches**: `main`, `main-dev`, `frontend-dev` + feature branches
- **Tags**: All existing tags
- **History**: Full git history (all commits)
- **Method**: `git clone --mirror` + `git push --mirror` (bare clone for safety)

### GitHub Configuration
| Asset | Count | Exportable via API? |
|-------|-------|---------------------|
| Workflows | 19 (+ 1 disabled) | In repo (git) |
| Custom Actions | 3 | In repo (git) |
| Issue Templates | 4 yml + 4 md | In repo (git) |
| CODEOWNERS | 1 | In repo (git) |
| Dependabot | 1 | In repo (git) |
| PR Template | 1 | In repo (git) |
| Labels | ~30+ | Yes (`gh label list`) |
| Issues | ~500+ (open+closed) | Yes (`gh issue list`) |
| Secrets | 8 | No (masked, re-create manually) |
| Variables | 2 | Yes (simple to re-create) |
| Environments | 3 | Re-create via API |
| Branch Protection | main, main-dev | Re-create via API |
| Self-hosted Runner | 1 (Hetzner ARM64) | Re-register |
| GHCR Images | api + web | Rebuild from new repo |

### Secrets (re-create manually)

| Secret | Source of Value |
|--------|---------------|
| `STAGING_HOST` | `204.168.135.69` |
| `STAGING_USER` | `deploy` |
| `STAGING_SSH_KEY` | `~/.ssh/meepleai-staging` |
| `PRODUCTION_HOST` | Production server IP |
| `PRODUCTION_USER` | Production SSH user |
| `PRODUCTION_SSH_KEY` | Production SSH key |
| `CODECOV_TOKEN` | Codecov dashboard |
| `SLACK_WEBHOOK_URL` | Slack app config |

### Variables (re-create)

| Variable | Value |
|----------|-------|
| `RUNNER` | `self-hosted,linux,arm64` (after runner registration) |
| `DEPLOY_METHOD` | `ssh` |

> Note: `SLACK_WEBHOOK_URL` is a **secret** only (contains sensitive webhook URL). Workflows check `vars.SLACK_WEBHOOK_URL` but should be updated to `secrets.SLACK_WEBHOOK_URL`, or a non-sensitive variable `SLACK_ENABLED=true` can gate notifications.

### Environments

| Environment | Protection Rules |
|-------------|-----------------|
| `staging` | None |
| `production` | Required reviewer: `meepleAi-app` |
| `production-approval` | Manual approval gate |

## Execution Plan

> **Critical ordering**: Export issues/labels FIRST (Phase 2), then git mirror (Phase 3). This ensures issue data is captured even if old repo access is lost during migration.

### Phase 1: Pre-requisites

1. Verify `meepleAi-app` GitHub account is active with verified email
2. Verify read access to `MeepleAI/meepleai-monorepo` (`gh repo view`)
3. Authenticate `gh` CLI with `meepleAi-app` (PAT scopes: `repo`, `workflow`, `write:packages`, `delete_repo`)
4. Create new repo:
   ```bash
   gh repo create meepleAi-app/meepleai-monorepo --public \
     --description "MeepleAI - AI board game assistant"
   ```
5. Configure Actions permissions:
   ```bash
   gh api repos/meepleAi-app/meepleai-monorepo/actions/permissions -X PUT \
     -f default_workflow_permissions=write \
     -F can_approve_pull_request_reviews=true
   ```

### Phase 2: Export Issues & Labels (BEFORE git mirror)

**Labels**:
```bash
# Export
gh label list --repo MeepleAI/meepleai-monorepo --limit 200 \
  --json name,color,description > labels-export.json

# Import
jq -c '.[]' labels-export.json | while read label; do
  gh label create \
    --repo meepleAi-app/meepleai-monorepo \
    "$(echo $label | jq -r '.name')" \
    --color "$(echo $label | jq -r '.color')" \
    --description "$(echo $label | jq -r '.description // empty')" \
    --force
done
```

**Milestones** (verify if used):
```bash
gh api repos/MeepleAI/meepleai-monorepo/milestones --paginate | jq '.[].title'
# If empty, skip. If present, create in new repo before importing issues.
```

**Issues** (sorted by number, using --body-file to handle special characters):
```bash
# Export all issues
gh issue list --repo MeepleAI/meepleai-monorepo \
  --state all --limit 5000 \
  --json number,title,body,state,labels,createdAt,closedAt \
  > issues-export.json

# Import in order (using temp file for body to avoid shell quoting issues)
jq -c 'sort_by(.number) | .[]' issues-export.json | while IFS= read -r issue; do
  OLD_NUM=$(echo "$issue" | jq -r '.number')
  TITLE=$(echo "$issue" | jq -r '.title')
  STATE=$(echo "$issue" | jq -r '.state')
  LABELS=$(echo "$issue" | jq -r '[.labels[].name] | join(",")')

  # Write body to temp file (avoids shell metacharacter issues)
  printf '> Migrated from MeepleAI/meepleai-monorepo#%s\n\n' "$OLD_NUM" > /tmp/issue-body.md
  echo "$issue" | jq -r '.body // empty' >> /tmp/issue-body.md

  # Create issue using --body-file
  NEW_URL=$(gh issue create \
    --repo meepleAi-app/meepleai-monorepo \
    --title "$TITLE" \
    --body-file /tmp/issue-body.md \
    ${LABELS:+--label "$LABELS"})

  # Close if was closed
  if [ "$STATE" = "CLOSED" ]; then
    gh issue close "$NEW_URL"
  fi

  echo "Migrated #$OLD_NUM → $NEW_URL ($STATE)"
done

rm -f /tmp/issue-body.md
```

**Accepted compromises**:
- Issue numbers will change (new repo starts at #1)
- Cross-references in body (e.g., "see #4920") point to old numbers — traceable via "Migrated from" header
- Assignees not migrated (DegrassiAaron removed from context)
- Issue comments not migrated (body only)

### Phase 3: Git Mirror (bare clone for safety)

Using a bare clone ensures an exact copy of the remote state, not local-only branches:

```bash
# Bare clone from source (exact remote state)
git clone --mirror https://github.com/MeepleAI/meepleai-monorepo.git /tmp/mirror-temp
cd /tmp/mirror-temp

# Push to new repo
git push --mirror https://github.com/meepleAi-app/meepleai-monorepo.git

# Clean up
cd -
rm -rf /tmp/mirror-temp

# Set default branch
gh api repos/meepleAi-app/meepleai-monorepo -X PATCH -f default_branch=main-dev

# Update local working copy
cd D:/Repositories/meepleai-monorepo-dev
git remote set-url origin https://github.com/meepleAi-app/meepleai-monorepo.git
git fetch origin

# Re-establish tracking
git branch -u origin/main-dev main-dev
git branch -u origin/frontend-dev frontend-dev
git branch -u origin/main main
```

### Phase 4: Secrets, Variables & Environments

```bash
# Secrets (interactive — will prompt for values)
gh secret set STAGING_HOST --repo meepleAi-app/meepleai-monorepo
gh secret set STAGING_USER --repo meepleAi-app/meepleai-monorepo
gh secret set STAGING_SSH_KEY --repo meepleAi-app/meepleai-monorepo < ~/.ssh/meepleai-staging
gh secret set PRODUCTION_HOST --repo meepleAi-app/meepleai-monorepo
gh secret set PRODUCTION_USER --repo meepleAi-app/meepleai-monorepo
gh secret set PRODUCTION_SSH_KEY --repo meepleAi-app/meepleai-monorepo
gh secret set CODECOV_TOKEN --repo meepleAi-app/meepleai-monorepo
gh secret set SLACK_WEBHOOK_URL --repo meepleAi-app/meepleai-monorepo

# Variables
gh variable set RUNNER --repo meepleAi-app/meepleai-monorepo --body "ubuntu-latest"
gh variable set DEPLOY_METHOD --repo meepleAi-app/meepleai-monorepo --body "ssh"

# Environments with protection rules
gh api repos/meepleAi-app/meepleai-monorepo/environments/staging -X PUT

# Production: requires reviewer approval
MEEPLEAI_USER_ID=$(gh api users/meepleAi-app --jq '.id')
gh api repos/meepleAi-app/meepleai-monorepo/environments/production -X PUT \
  --input - <<EOF
{
  "reviewers": [{"type": "User", "id": $MEEPLEAI_USER_ID}],
  "deployment_branch_policy": null
}
EOF

gh api repos/meepleAi-app/meepleai-monorepo/environments/production-approval -X PUT \
  --input - <<EOF
{
  "reviewers": [{"type": "User", "id": $MEEPLEAI_USER_ID}],
  "deployment_branch_policy": null
}
EOF
```

### Phase 5: File Updates in Repo

**Files to modify** (62 files contain `DegrassiAaron` references):

| Category | Files | Change |
|----------|-------|--------|
| **GitHub Config** | `.github/CODEOWNERS` | `@DegrassiAaron` → `@meepleAi-app` (30+ entries) |
| **GitHub Config** | `.github/dependabot.yml` | `reviewers: ["DegrassiAaron"]` → `["meepleAi-app"]` (4x) |
| **Deployment scripts** | `tools/deployment/deploy-staging.sh` | `ghcr.io/degrassiaaron` → `ghcr.io/meepleai-app` |
| **Deployment scripts** | `tools/deployment/deploy-production.sh` | `ghcr.io/degrassiaaron` → `ghcr.io/meepleai-app` |
| **Deployment scripts** | `tools/deployment/rollback.sh` | `ghcr.io/degrassiaaron` → `ghcr.io/meepleai-app` |
| **Deployment scripts** | `tools/deployment/README.md` | `ghcr.io/degrassiaaron` → `ghcr.io/meepleai-app` |
| **Infra scripts** | `infra/scripts/reset-staging-beta0.sh` | `ghcr.io/degrassiaaron` → `ghcr.io/meepleai-app` |
| **Infra scripts** | `infra/runner/setup-runner.sh` | Update GitHub org reference |
| **Security scripts** | `scripts/setup-branch-protection.sh` | Update repo owner |
| **Security scripts** | `scripts/security-check-local.ps1` | Update repo reference |
| **Tools** | `tools/dismiss-codeql-false-positives.sh` | Update repo reference |
| **Tools** | `tools/publish-wiki.sh` | Update repo reference |
| **App code** | `apps/api/src/Api/Services/BggApiService.cs` | Update UserAgent or repo reference |
| **App code** | `apps/api/src/Api/Services/IBggApiService.cs` | Update reference |
| **App code** | `apps/web/src/components/agent/AgentModeSelector.tsx` | Update reference |
| **App code** | `apps/web/src/components/modals/BggSearchModal.tsx` | Update reference |
| **App code** | `apps/web/src/lib/api/core/REQUEST_CACHE.md` | Update reference |
| **Project docs** | `CLAUDE.md` | Update remote URL |
| **Docs (30+ files)** | `docs/**/*.md`, `claudedocs/**` | Update DegrassiAaron references |

**Strategy**: Use `grep -rl` + `sed` or IDE find-and-replace for bulk updates:
- `DegrassiAaron` → `meepleAi-app` (case-sensitive)
- `degrassiaaron` → `meepleai-app` (lowercase, for GHCR paths)
- `ghcr.io/degrassiaaron` → `ghcr.io/meepleai-app`
- `MeepleAI/meepleai-monorepo` → `meepleAi-app/meepleai-monorepo` (in URLs only, not prose)

**Commit**:
```
chore: migrate all repo references from MeepleAI/DegrassiAaron to meepleAi-app
```

### Phase 6: Branch Protection Rules

```bash
# Protect main branch
gh api repos/meepleAi-app/meepleai-monorepo/branches/main/protection -X PUT \
  --input - <<'EOF'
{
  "required_status_checks": null,
  "enforce_admins": true,
  "required_pull_request_reviews": {
    "required_approving_review_count": 1
  },
  "restrictions": null
}
EOF

# Protect main-dev branch
gh api repos/meepleAi-app/meepleai-monorepo/branches/main-dev/protection -X PUT \
  --input - <<'EOF'
{
  "required_status_checks": null,
  "enforce_admins": false,
  "required_pull_request_reviews": null,
  "restrictions": null
}
EOF
```

### Phase 7: Self-Hosted Runner Re-registration

```bash
# On server: remove old registration
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69
cd /opt/actions-runner
sudo ./svc.sh stop
./config.sh remove --token <OLD_REMOVE_TOKEN>

# Generate new registration token (from local PC)
gh api repos/meepleAi-app/meepleai-monorepo/actions/runners/registration-token \
  -X POST --jq '.token'

# On server: register under new repo
./config.sh \
  --url https://github.com/meepleAi-app/meepleai-monorepo \
  --token <NEW_REG_TOKEN> \
  --name meepleai-staging \
  --labels self-hosted,linux,arm64 \
  --unattended
sudo ./svc.sh install
sudo ./svc.sh start

# Update variable to use self-hosted
gh variable set RUNNER --repo meepleAi-app/meepleai-monorepo \
  --body "self-hosted,linux,arm64"
```

### Phase 8: GHCR Images & Verification

**GHCR**: No manual migration. First CI build or deploy creates images at `ghcr.io/meepleai-app/meepleai-monorepo/{api,web}` automatically (GHCR normalizes paths to lowercase; workflows use `${{ github.repository }}`).

**Verification checklist**:

| Check | Command | Expected |
|-------|---------|----------|
| Branches | `gh api repos/meepleAi-app/meepleai-monorepo/branches --paginate --jq '.[].name'` | main, main-dev, frontend-dev + feature |
| Default branch | `gh repo view meepleAi-app/meepleai-monorepo --json defaultBranchRef` | main-dev |
| Issues count | `gh issue list --repo meepleAi-app/meepleai-monorepo --state all --limit 5000 --json number \| jq length` | Matches source count |
| Labels | `gh label list --repo meepleAi-app/meepleai-monorepo` | All labels present |
| Secrets | `gh secret list --repo meepleAi-app/meepleai-monorepo` | 8 secrets listed |
| Variables | `gh variable list --repo meepleAi-app/meepleai-monorepo` | 2 variables listed |
| Environments | `gh api repos/meepleAi-app/meepleai-monorepo/environments --jq '.environments[].name'` | staging, production, production-approval |
| Branch protection | `gh api repos/meepleAi-app/meepleai-monorepo/branches/main/protection` | Protection rules active |
| Runner | `gh api repos/meepleAi-app/meepleai-monorepo/actions/runners --jq '.runners[] \| {name,status}'` | meepleai-staging: online |
| CI trigger | Push test commit | Workflow runs successfully |
| GHCR | First build completes | Images at ghcr.io/meepleai-app/... |

### Post-Migration

1. Re-link Codecov to new repo
2. Update Slack webhook if GitHub-specific integration
3. Update project memory (`MEMORY.md`) with new remote URL
4. Optional: archive old repo (`gh repo archive MeepleAI/meepleai-monorepo`)

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Old repo inaccessible mid-migration | Can't export issues | Phase 2 exports issues FIRST, before git mirror |
| Issue import fails midway | Partial issues | Script logs progress; export JSON is backup; can resume from last number |
| Issue body corruption (special chars) | Garbled issues | Using `--body-file` instead of shell variable expansion |
| Self-hosted runner registration fails | No CI on self-hosted | `vars.RUNNER` defaults to `ubuntu-latest` in workflows |
| GHCR permissions | Can't push images | PAT includes `write:packages`; Actions permissions set to write |
| Branch protection rules lost | Unprotected main | Phase 6 explicitly recreates protection rules |
| Hardcoded GHCR paths in scripts | Deploy pulls from wrong registry | Phase 5 updates all 62 files with DegrassiAaron/degrassiaaron references |

## Out of Scope

- PR history and review comments (not migrated)
- Issue comments (only body migrated)
- GitHub Stars/Watchers (not transferable)
- GitHub wiki (not used)
- GitHub Pages (not used)
- Milestones (verified if used; migrated only if present)
