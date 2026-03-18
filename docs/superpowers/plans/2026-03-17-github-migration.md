# GitHub Migration: MeepleAI → meepleAi-app — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Transfer the entire `MeepleAI/meepleai-monorepo` repository to `meepleAi-app/meepleai-monorepo`, including code, issues, secrets, environments, runner, and all file references.

**Architecture:** Bare git mirror for code, `gh` CLI API for issues/labels/config export-import, manual secrets re-creation, bulk find-and-replace for 62 files with hardcoded references. Phases ordered to minimize data loss risk: export first, then mirror, then configure.

**Tech Stack:** `gh` CLI, `git`, `jq`, `bash`, SSH

**Spec:** `docs/superpowers/specs/2026-03-17-github-migration-design.md`

---

## Task 1: Pre-requisites & New Repo Creation

**Context:** Before anything else, verify access to both old and new accounts, create the destination repo, and configure its permissions. This is all done from the local machine.

**Files:** None (all API/CLI operations)

- [ ] **Step 1: Verify old repo access**

```bash
gh repo view MeepleAI/meepleai-monorepo --json name,owner
```

Expected: JSON with `{"name":"meepleai-monorepo","owner":{"login":"MeepleAI"}}`. If this fails, the old org is inaccessible and the issue export in Task 2 must be skipped.

- [ ] **Step 2: Authenticate as meepleAi-app**

```bash
gh auth login
```

Select GitHub.com, HTTPS, paste PAT with scopes: `repo`, `workflow`, `write:packages`, `delete_repo`. Verify:

```bash
gh auth status
```

Expected: `Logged in to github.com account meepleAi-app`

- [ ] **Step 3: Create the new repo**

```bash
gh repo create meepleAi-app/meepleai-monorepo --public --description "MeepleAI - AI board game assistant"
```

Expected: `https://github.com/meepleAi-app/meepleai-monorepo`

- [ ] **Step 4: Configure Actions permissions**

```bash
gh api repos/meepleAi-app/meepleai-monorepo/actions/permissions -X PUT \
  -f default_workflow_permissions=write \
  -F can_approve_pull_request_reviews=true
```

Expected: HTTP 204 No Content

---

## Task 2: Export Labels & Issues from Old Repo

**Context:** This MUST run BEFORE the git mirror (Task 3). If old repo access is lost after the mirror, the issue data would be unrecoverable. All exports go to temp files in the working directory.

**Files:** Creates temp files: `labels-export.json`, `issues-export.json`

- [ ] **Step 1: Export labels**

```bash
gh label list --repo MeepleAI/meepleai-monorepo --limit 200 \
  --json name,color,description > labels-export.json
```

Verify: `jq length labels-export.json` — should return a number (e.g., 30+)

- [ ] **Step 2: Check milestones**

```bash
gh api repos/MeepleAI/meepleai-monorepo/milestones --paginate | jq '.[].title'
```

Expected: Empty array `[]` or list of titles. If milestones exist, create them in new repo before issue import.

- [ ] **Step 3: Export all issues**

```bash
gh issue list --repo MeepleAI/meepleai-monorepo \
  --state all --limit 5000 \
  --json number,title,body,state,labels,createdAt,closedAt \
  > issues-export.json
```

Verify: `jq length issues-export.json` — note this number, it's the target count for verification.

- [ ] **Step 4: Import labels into new repo**

```bash
jq -c '.[]' labels-export.json | while IFS= read -r label; do
  NAME=$(echo "$label" | jq -r '.name')
  COLOR=$(echo "$label" | jq -r '.color')
  DESC=$(echo "$label" | jq -r '.description // empty')
  gh label create --repo meepleAi-app/meepleai-monorepo "$NAME" \
    --color "$COLOR" \
    ${DESC:+--description "$DESC"} \
    --force
done
```

Verify: `gh label list --repo meepleAi-app/meepleai-monorepo --limit 200 | wc -l`

- [ ] **Step 5: Import issues (in number order, using --body-file)**

```bash
jq -c 'sort_by(.number) | .[]' issues-export.json | while IFS= read -r issue; do
  OLD_NUM=$(echo "$issue" | jq -r '.number')
  TITLE=$(echo "$issue" | jq -r '.title')
  STATE=$(echo "$issue" | jq -r '.state')
  LABELS=$(echo "$issue" | jq -r '[.labels[].name] | join(",")')

  # Write body to temp file to avoid shell quoting issues
  printf '> Migrated from MeepleAI/meepleai-monorepo#%s\n\n' "$OLD_NUM" > /tmp/issue-body.md
  echo "$issue" | jq -r '.body // empty' >> /tmp/issue-body.md

  NEW_URL=$(gh issue create \
    --repo meepleAi-app/meepleai-monorepo \
    --title "$TITLE" \
    --body-file /tmp/issue-body.md \
    ${LABELS:+--label "$LABELS"})

  if [ "$STATE" = "CLOSED" ]; then
    gh issue close "$NEW_URL" 2>/dev/null
  fi

  echo "Migrated #$OLD_NUM → $NEW_URL ($STATE)"
done

rm -f /tmp/issue-body.md
```

This will take a while (~500+ issues). Monitor progress via the echo output. If it fails midway, note the last migrated number and adjust the `jq` filter to skip already-migrated issues:
```bash
jq -c '[sort_by(.number) | .[] | select(.number > LAST_MIGRATED_NUM)] | .[]' issues-export.json
```

- [ ] **Step 6: Verify issue count**

```bash
gh issue list --repo meepleAi-app/meepleai-monorepo --state all --limit 5000 --json number | jq length
```

Expected: Same count as source (from Step 3).

---

## Task 3: Git Mirror

**Context:** Use a bare clone to get an exact copy of the remote state (not local working tree, which may have divergent branches). After mirroring, switch the local working copy to point at the new repo.

**Files:** None (git operations only)

- [ ] **Step 1: Bare clone from source**

```bash
git clone --mirror https://github.com/MeepleAI/meepleai-monorepo.git /tmp/mirror-temp
```

Expected: Cloning into bare repository '/tmp/mirror-temp'...

- [ ] **Step 2: Push mirror to new repo**

```bash
cd /tmp/mirror-temp && git push --mirror https://github.com/meepleAi-app/meepleai-monorepo.git
```

Expected: All branches and tags pushed.

- [ ] **Step 3: Clean up temp clone**

```bash
cd D:/Repositories/meepleai-monorepo-dev && rm -rf /tmp/mirror-temp
```

- [ ] **Step 4: Set default branch**

```bash
gh api repos/meepleAi-app/meepleai-monorepo -X PATCH -f default_branch=main-dev
```

Expected: JSON with `"default_branch": "main-dev"`

- [ ] **Step 5: Update local working copy remote**

```bash
cd D:/Repositories/meepleai-monorepo-dev
git remote set-url origin https://github.com/meepleAi-app/meepleai-monorepo.git
git fetch origin
```

Verify: `git remote -v` shows `meepleAi-app/meepleai-monorepo.git`

- [ ] **Step 6: Re-establish branch tracking**

```bash
git branch -u origin/main-dev main-dev
git branch -u origin/frontend-dev frontend-dev
git branch -u origin/main main
```

Verify: `git branch -vv` shows all three tracking origin.

- [ ] **Step 7: Verify branches on remote**

```bash
gh api repos/meepleAi-app/meepleai-monorepo/branches --paginate --jq '.[].name'
```

Expected: `main`, `main-dev`, `frontend-dev`, plus any feature branches.

---

## Task 4: Secrets, Variables & Environments

**Context:** Secrets cannot be exported from GitHub (they're masked). Re-create them using values from local files. Variables and environments are created via API. This task requires interactive input for secret values.

**Files:** None (API operations, reads local `~/.ssh/meepleai-staging`)

- [ ] **Step 1: Set deployment secrets**

```bash
gh secret set STAGING_HOST --repo meepleAi-app/meepleai-monorepo --body "204.168.135.69"
gh secret set STAGING_USER --repo meepleAi-app/meepleai-monorepo --body "deploy"
gh secret set STAGING_SSH_KEY --repo meepleAi-app/meepleai-monorepo < ~/.ssh/meepleai-staging
```

Expected: `✓ Set secret STAGING_HOST for meepleAi-app/meepleai-monorepo` (×3)

- [ ] **Step 2: Set production secrets (interactive — user provides values)**

```bash
gh secret set PRODUCTION_HOST --repo meepleAi-app/meepleai-monorepo
gh secret set PRODUCTION_USER --repo meepleAi-app/meepleai-monorepo
gh secret set PRODUCTION_SSH_KEY --repo meepleAi-app/meepleai-monorepo
```

Each will prompt for the value. Paste the production server IP, user, and SSH key.

- [ ] **Step 3: Set integration secrets (interactive — user provides values)**

```bash
gh secret set CODECOV_TOKEN --repo meepleAi-app/meepleai-monorepo
gh secret set SLACK_WEBHOOK_URL --repo meepleAi-app/meepleai-monorepo
```

Get CODECOV_TOKEN from https://app.codecov.io. Get SLACK_WEBHOOK_URL from Slack app config.

- [ ] **Step 4: Set variables**

```bash
gh variable set RUNNER --repo meepleAi-app/meepleai-monorepo --body "ubuntu-latest"
gh variable set DEPLOY_METHOD --repo meepleAi-app/meepleai-monorepo --body "ssh"
```

Note: RUNNER starts as `ubuntu-latest`; updated to `self-hosted,linux,arm64` after runner registration in Task 6.

- [ ] **Step 5: Create environments with protection rules**

```bash
# Staging — no protection
gh api repos/meepleAi-app/meepleai-monorepo/environments/staging -X PUT

# Get user ID for reviewer config
MEEPLEAI_USER_ID=$(gh api users/meepleAi-app --jq '.id')
echo "User ID: $MEEPLEAI_USER_ID"

# Production — requires reviewer
gh api repos/meepleAi-app/meepleai-monorepo/environments/production -X PUT \
  --input - <<EOF
{
  "reviewers": [{"type": "User", "id": $MEEPLEAI_USER_ID}],
  "deployment_branch_policy": null
}
EOF

# Production-approval — requires reviewer
gh api repos/meepleAi-app/meepleai-monorepo/environments/production-approval -X PUT \
  --input - <<EOF
{
  "reviewers": [{"type": "User", "id": $MEEPLEAI_USER_ID}],
  "deployment_branch_policy": null
}
EOF
```

- [ ] **Step 6: Verify all config**

```bash
echo "=== Secrets ==="
gh secret list --repo meepleAi-app/meepleai-monorepo
echo "=== Variables ==="
gh variable list --repo meepleAi-app/meepleai-monorepo
echo "=== Environments ==="
gh api repos/meepleAi-app/meepleai-monorepo/environments --jq '.environments[].name'
```

Expected: 8 secrets, 2 variables, 3 environments (staging, production, production-approval).

---

## Task 5: File Updates — Bulk Reference Replacement

**Context:** 62 files contain `DegrassiAaron` or `degrassiaaron` references that must be updated to `meepleAi-app` or `meepleai-app`. This is a bulk find-and-replace operation split into categories. Run on `main-dev` branch.

**Files:** 62 files across `.github/`, `tools/`, `infra/`, `scripts/`, `apps/`, `docs/`, `claudedocs/`, `CLAUDE.md`

- [ ] **Step 1: Create feature branch**

```bash
git checkout main-dev
git pull
git checkout -b chore/migrate-repo-references
```

- [ ] **Step 2: Replace `DegrassiAaron` → `meepleAi-app` (case-sensitive)**

This covers CODEOWNERS, dependabot, docs, app code references. Use `sed` with backup:

```bash
grep -rl 'DegrassiAaron' --include='*.md' --include='*.yml' --include='*.yaml' --include='*.tsx' --include='*.ts' --include='*.cs' --include='*.sh' --include='*.ps1' --include='*.py' --include='*.json' --include='*.html' . | while IFS= read -r file; do
  sed -i 's/DegrassiAaron/meepleAi-app/g' "$file"
  echo "Updated: $file"
done
```

- [ ] **Step 3: Replace `degrassiaaron` → `meepleai-app` (lowercase — GHCR paths)**

```bash
grep -rl 'degrassiaaron' --include='*.md' --include='*.sh' --include='*.ps1' . | while IFS= read -r file; do
  sed -i 's/degrassiaaron/meepleai-app/g' "$file"
  echo "Updated: $file"
done
```

- [ ] **Step 4: Replace `MeepleAI/meepleai-monorepo` → `meepleAi-app/meepleai-monorepo` (in URLs)**

Only replace when it's a GitHub URL pattern (not prose references to "MeepleAI"):

```bash
grep -rl 'MeepleAI/meepleai-monorepo' --include='*.md' --include='*.yml' --include='*.sh' --include='*.ps1' --include='*.json' . | while IFS= read -r file; do
  sed -i 's|MeepleAI/meepleai-monorepo|meepleAi-app/meepleai-monorepo|g' "$file"
  echo "Updated: $file"
done
```

- [ ] **Step 5: Verify no remaining references**

```bash
echo "=== DegrassiAaron ==="
grep -ri 'DegrassiAaron' --include='*.md' --include='*.yml' --include='*.yaml' --include='*.tsx' --include='*.ts' --include='*.cs' --include='*.sh' --include='*.ps1' --include='*.py' --include='*.json' --include='*.html' . || echo "None found ✓"

echo "=== degrassiaaron ==="
grep -ri 'degrassiaaron' --include='*.md' --include='*.sh' --include='*.ps1' . || echo "None found ✓"

echo "=== MeepleAI/meepleai-monorepo ==="
grep -r 'MeepleAI/meepleai-monorepo' --include='*.md' --include='*.yml' --include='*.sh' --include='*.ps1' --include='*.json' . || echo "None found ✓"
```

Expected: "None found ✓" for all three. If any remain, fix manually.

- [ ] **Step 6: Review key files manually**

Quick-check that these critical files look correct:

```bash
head -5 .github/CODEOWNERS
grep 'reviewers' .github/dependabot.yml
grep 'DOCKER_REGISTRY' tools/deployment/deploy-staging.sh
grep 'ghcr.io' infra/scripts/reset-staging-beta0.sh
```

Expected: All show `meepleAi-app` or `meepleai-app`, zero `DegrassiAaron`.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "chore: migrate all repo references from MeepleAI/DegrassiAaron to meepleAi-app"
```

- [ ] **Step 8: Push and merge to main-dev**

```bash
git push -u origin chore/migrate-repo-references
```

Then either merge directly (since this is a mechanical replacement) or create a PR:

```bash
gh pr create --repo meepleAi-app/meepleai-monorepo \
  --title "chore: migrate repo references to meepleAi-app" \
  --body "Bulk replace DegrassiAaron → meepleAi-app and degrassiaaron → meepleai-app across 62 files." \
  --base main-dev
```

---

## Task 6: Branch Protection Rules

**Context:** The old repo had branch protection on `main` and `main-dev`. Recreate them on the new repo.

**Files:** None (API operations)

- [ ] **Step 1: Protect main branch**

```bash
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
```

Expected: JSON with protection rules.

- [ ] **Step 2: Protect main-dev branch**

```bash
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

- [ ] **Step 3: Verify**

```bash
gh api repos/meepleAi-app/meepleai-monorepo/branches/main/protection --jq '.enforce_admins.enabled'
```

Expected: `true`

---

## Task 7: Self-Hosted Runner Re-registration

**Context:** The Hetzner CAX21 ARM64 server at `204.168.135.69` runs a GitHub Actions self-hosted runner. It must be unregistered from the old repo and registered under the new one. This task requires SSH access to the server.

**Files:** None (SSH + API operations)

- [ ] **Step 1: Generate removal token for old registration (if accessible)**

```bash
gh api repos/MeepleAI/meepleai-monorepo/actions/runners/remove-token -X POST --jq '.token'
```

If the old org is inaccessible, the runner may need to be force-removed on the server.

- [ ] **Step 2: SSH to server and remove old runner**

```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69
# On server:
cd /opt/actions-runner
sudo ./svc.sh stop
./config.sh remove --token <REMOVAL_TOKEN>
# If token doesn't work: ./config.sh remove --force
```

- [ ] **Step 3: Generate new registration token**

Back on local PC:

```bash
gh api repos/meepleAi-app/meepleai-monorepo/actions/runners/registration-token \
  -X POST --jq '.token'
```

Copy the token output.

- [ ] **Step 4: Register runner under new repo**

SSH back to server:

```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69
cd /opt/actions-runner
./config.sh \
  --url https://github.com/meepleAi-app/meepleai-monorepo \
  --token <NEW_REG_TOKEN> \
  --name meepleai-staging \
  --labels self-hosted,linux,arm64 \
  --unattended
sudo ./svc.sh install
sudo ./svc.sh start
```

Expected: `Settings Saved.` then `Successfully installed` then `Successfully started`

- [ ] **Step 5: Update RUNNER variable to use self-hosted**

```bash
gh variable set RUNNER --repo meepleAi-app/meepleai-monorepo --body "self-hosted,linux,arm64"
```

- [ ] **Step 6: Verify runner is online**

```bash
gh api repos/meepleAi-app/meepleai-monorepo/actions/runners --jq '.runners[] | {name, status, labels: [.labels[].name]}'
```

Expected: `{"name": "meepleai-staging", "status": "online", "labels": ["self-hosted", "linux", "arm64"]}`

---

## Task 8: Full Verification & Post-Migration

**Context:** Run the complete verification checklist to confirm everything migrated correctly. Then clean up temp files and update project memory.

**Files:**
- Modify: `MEMORY.md` (update remote URL reference)
- Delete: `labels-export.json`, `issues-export.json` (temp migration files)

- [ ] **Step 1: Run full verification checklist**

```bash
echo "=== 1. Branches ==="
gh api repos/meepleAi-app/meepleai-monorepo/branches --paginate --jq '.[].name'

echo "=== 2. Default branch ==="
gh repo view meepleAi-app/meepleai-monorepo --json defaultBranchRef --jq '.defaultBranchRef.name'

echo "=== 3. Issue count ==="
gh issue list --repo meepleAi-app/meepleai-monorepo --state all --limit 5000 --json number | jq length

echo "=== 4. Labels ==="
gh label list --repo meepleAi-app/meepleai-monorepo --limit 200 --json name | jq length

echo "=== 5. Secrets ==="
gh secret list --repo meepleAi-app/meepleai-monorepo

echo "=== 6. Variables ==="
gh variable list --repo meepleAi-app/meepleai-monorepo

echo "=== 7. Environments ==="
gh api repos/meepleAi-app/meepleai-monorepo/environments --jq '.environments[].name'

echo "=== 8. Branch protection ==="
gh api repos/meepleAi-app/meepleai-monorepo/branches/main/protection --jq '.enforce_admins.enabled' 2>/dev/null || echo "Not set"

echo "=== 9. Runner ==="
gh api repos/meepleAi-app/meepleai-monorepo/actions/runners --jq '.runners[] | {name, status}'

echo "=== 10. No old references in code ==="
grep -ri 'DegrassiAaron\|degrassiaaron' --include='*.md' --include='*.yml' --include='*.sh' --include='*.tsx' --include='*.ts' --include='*.cs' . | head -5 || echo "Clean ✓"
```

Expected results:
1. main, main-dev, frontend-dev + feature branches
2. main-dev
3. Same as source export count
4. Same as source label count
5. 8 secrets
6. 2 variables
7. staging, production, production-approval
8. true
9. meepleai-staging: online
10. Clean ✓

- [ ] **Step 2: Trigger CI to verify workflows**

```bash
# Push a small test change (e.g., whitespace in README) to trigger CI
git checkout main-dev
echo "" >> README.md
git add README.md
git commit -m "ci: verify workflows on new repo"
git push
```

Check: `gh run list --repo meepleAi-app/meepleai-monorepo --limit 5`

Expected: CI workflow triggered and running.

- [ ] **Step 3: Clean up temp files**

```bash
rm -f labels-export.json issues-export.json
```

- [ ] **Step 4: Update project memory**

Update `MEMORY.md` to replace:
```
**Remote URL**: `https://github.com/MeepleAI/meepleai-monorepo.git` (migrated from MeepleAi-Dev)
```
With:
```
**Remote URL**: `https://github.com/meepleAi-app/meepleai-monorepo.git` (migrated from MeepleAI on 2026-03-17)
```

- [ ] **Step 5: Commit memory update**

```bash
git add -A
git commit -m "docs: update project memory with new repo URL"
git push
```

- [ ] **Step 6: Optional — archive old repo**

Only after everything is verified working:

```bash
gh repo archive MeepleAI/meepleai-monorepo --yes
```

---

## Post-Migration External Services

These require manual browser actions (not automatable via CLI):

- [ ] **Codecov**: Go to https://app.codecov.io → Settings → Re-link to `meepleAi-app/meepleai-monorepo`
- [ ] **Slack integration**: If using GitHub-Slack app, re-authorize for new repo
- [ ] **Domain DNS**: No changes needed (meepleai.app and meepleai.com point to server, not GitHub)
