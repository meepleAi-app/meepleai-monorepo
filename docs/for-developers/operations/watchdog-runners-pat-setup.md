# Watchdog Runners PAT Setup

**Workflow**: `.github/workflows/monitor-runner-queue.yml` (queue-time watchdog, issue #1573)

The watchdog detects self-hosted runner jobs stuck in queue and cancels the
containing run. To **list self-hosted runners** (`GET /repos/{owner}/{repo}/actions/runners`)
and **cancel runs** it needs a token with scopes that the default
`GITHUB_TOKEN` cannot carry (`administration` is not a valid workflow
permission — see the header comment in the workflow).

Until the PAT is configured, the watchdog runs in **fail-safe mode**:
detection + logging only, **no cancellations** (forced `DRY_RUN=true` on the
HTTP 403 from the runners endpoint). Configuring the PAT below upgrades it to
full cancel capability.

---

## 1. Create the fine-grained PAT

> Use a **fine-grained** PAT, NOT a classic token. Classic tokens grant the
> entire `repo` scope (too broad); fine-grained tokens scope to a single repo
> with minimal permissions.

1. GitHub → your avatar → **Settings** → **Developer settings** →
   **Personal access tokens** → **Fine-grained tokens** → **Generate new token**
2. **Token name**: `meepleai-watchdog-runners` (or similar — identifiable)
3. **Resource owner**: `meepleAi-app` (the org that owns the repo)
4. **Expiration**: **90 days** (see Rotation below — do not pick "No expiration")
5. **Repository access** → **Only select repositories** → `meepleai-monorepo`
6. **Permissions** → **Repository permissions**, set exactly these (leave all others "No access"):

   | Permission | Access | Why |
   |------------|--------|-----|
   | **Administration** | **Read-only** | `GET /actions/runners` (list self-hosted runners) |
   | **Actions** | **Read and write** | list runs/jobs + `POST /actions/runs/{id}/cancel` (and `/force-cancel`, same scope) |
   | Metadata | Read-only | auto-selected (mandatory) |

7. **Generate token** → copy the `github_pat_…` value (shown once).

## 2. Add the token as a repository secret

Either via UI or CLI.

**UI**: repo → **Settings** → **Secrets and variables** → **Actions** →
**New repository secret** → Name `WATCHDOG_RUNNERS_PAT`, Value = the token.

**CLI** (run it yourself — do NOT paste the token into a shared session):

```bash
# You will be prompted to paste the token value (it is not echoed)
gh secret set WATCHDOG_RUNNERS_PAT --repo meepleAi-app/meepleai-monorepo
```

The workflow picks it up automatically via
`GH_TOKEN: ${{ secrets.WATCHDOG_RUNNERS_PAT || secrets.GITHUB_TOKEN }}` — no
workflow edit needed.

## 3. Validate

Trigger a dry-run dispatch and confirm the runners list resolves (no 403):

```bash
gh workflow run monitor-runner-queue.yml \
  --repo meepleAi-app/meepleai-monorepo \
  --ref main-dev \
  --field dry_run=true

# wait, then inspect the latest run's log:
gh run list --repo meepleAi-app/meepleai-monorepo \
  --workflow monitor-runner-queue.yml --limit 1
gh run view <run-id> --repo meepleAi-app/meepleai-monorepo --log \
  | grep -E "runners online|Cannot list|FAIL-SAFE"
```

**Acceptance criteria** (issue #1573 follow-up):
- [ ] `gh secret list` shows `WATCHDOG_RUNNERS_PAT`
- [ ] Watchdog log shows `✅ Self-hosted runners online:` with real runner names (NOT the `{"message":"Resource not accessible..."}` JSON error)
- [ ] No `🛡️ FAIL-SAFE` line in the log
- [ ] With a real stuck job (or a forced test), `DRY_RUN=false` run cancels it

## 4. Rotation & ownership (operational)

- **Expiry**: the token expires in 90 days. When it expires the watchdog
  silently reverts to fail-safe (no cancellations) — it does NOT break the
  workflow, but it stops protecting against stuck deploys. Set a calendar
  reminder to regenerate + update the secret before expiry.
- **Ownership**: a fine-grained PAT is tied to the personal account that
  created it. If that person rotates credentials or leaves, the watchdog loses
  cancel capability. Record the owner here:

  | Field | Value |
  |-------|-------|
  | Token owner | `meepleAi-app` (GitHub account authenticated for the repo at setup) |
  | Created | 2026-05-27 (secret `WATCHDOG_RUNNERS_PAT` set 09:30 UTC) |
  | Expires | 2026-08-25 (assumes the 90-day expiry from this guide — update if a different expiry was chosen at token creation) |
  | Validated | 2026-05-27 — watchdog run `26503048359` listed runner `meepleai-staging`, no fail-safe |

- **Future hardening**: migrate to a **GitHub App** installation token
  (org-owned, auto-refreshing, not tied to a personal account). Tracked as a
  potential follow-up; the PAT is the MVP.

## Security notes

- Scope is least-privilege: single repo, `Administration: Read` +
  `Actions: Read and write`, nothing else.
- The token is never logged: GH masks secret values, and the workflow only
  echoes runner *names*, never the token.
- If the token is ever leaked, revoke it immediately in Developer settings and
  regenerate — the fail-safe ensures the watchdog degrades safely meanwhile.
