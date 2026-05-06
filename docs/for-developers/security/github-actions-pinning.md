# GitHub Actions Pinning Policy

> **Origin**: Q2 2026 Security Review (#186) P1.2
> **Status**: Active 2026-05-06
> **Bounded Context**: CI/CD supply chain security

## Problem

GitHub Actions in our workflows pull code from upstream repositories at execution time. If a third-party action is referenced by a moving tag (e.g. `@v4`), an attacker who compromises the upstream repository can publish a malicious patch under the same tag, and our CI will execute it on the next run — with `GITHUB_TOKEN` access and any secrets the workflow exposes.

This is the **single most common supply chain attack vector** for GitHub-hosted projects (e.g. tj-actions/changed-files compromise, March 2025).

## Policy

Two tiers based on trust assumption:

### Tier 1 — May use major/minor version tags

Trusted by virtue of repository ownership:

- `actions/*` — owned by GitHub itself (e.g. `actions/checkout`, `actions/setup-node`)
- `github/*` — owned by GitHub (e.g. `github/codeql-action`)
- `meepleAi-app/*` — our own organization

Rationale: a compromise of these repositories implies a compromise of platform security or our own organization, in which case SHA-pinning provides no additional protection.

**Allowed**:
```yaml
- uses: actions/checkout@v6
- uses: github/codeql-action/init@v3
```

### Tier 2 — MUST be SHA-pinned with version comment

All other (third-party) actions:

```yaml
- uses: docker/build-push-action@10e90e3645eae34f1e60eeb005ba3a3d33f178e8  # v6
- uses: codecov/codecov-action@75cd11691c0faa626561e295848008c8a7dddffe  # v5
```

Rationale:
- **SHA**: immutable reference. Even if the upstream tag is force-moved or the repo is compromised, our workflow continues using the audited code.
- **Version comment**: human-readable. Helps reviewers + future bumpers identify the corresponding release.
- **Dependabot updates**: `dependabot.yml` includes `package-ecosystem: github-actions` (weekly Mondays 02:00 UTC). When a third-party releases a new version, Dependabot opens a PR with the new SHA + comment update — review it like any other dependency bump.

### Tier 3 — Local actions

Composite actions hosted in this repository under `.github/actions/` (e.g. `setup-backend`, `setup-frontend`, `setup-playwright`) are addressed via relative paths and are out of scope for pinning:

```yaml
- uses: ./.github/actions/setup-backend
```

## Enforcement

### CI gate

The `validate-workflows.yml` workflow runs on every PR touching `.github/workflows/**` and fails if it detects any unpinned third-party action. Regex used:

```bash
grep -rEh "^\s*-?\s*uses: " .github/workflows/*.yml | \
  grep -E "@v?[0-9]" | \
  grep -vE "@[a-f0-9]{40}" | \
  grep -vE "uses: (actions|github|meepleAi-app)/" | \
  grep -v "uses: \./"
# Expected: empty output (otherwise PR fails)
```

### Audit command

To audit current state at any time:

```bash
# All third-party action references with their pinning state
grep -rEh "^\s*-?\s*uses: " .github/workflows/*.yml | \
  grep -vE "uses: (actions|github|meepleAi-app)/|uses: \./" | sort -u
```

## Disabled workflow files

`.github/workflows/*.yml.disabled` files are NOT scanned by the CI gate (they don't run on GitHub Actions). When re-enabling such a file:
1. Rename to `.yml`
2. Run the audit command above
3. SHA-pin any unpinned third-party actions before merging the rename

## How to update a pinned action (Dependabot or manual)

When upstream releases a new version:

```bash
# Resolve the SHA for the new tag
gh api repos/<owner>/<repo>/git/refs/tags/<new-version> --jq '.object.sha'

# Update workflow file:
# Before: uses: docker/build-push-action@10e90e3645eae34f1e60eeb005ba3a3d33f178e8  # v6
# After:  uses: docker/build-push-action@<new-sha>  # v6.x.y

# Re-run audit to confirm no regressions
```

Dependabot does this automatically; just review the PR diff (especially the SHA changes against the upstream commit log).

## Token permissions (related, P1.2 scope-creep)

Beyond pinning, workflow `GITHUB_TOKEN` permissions should be set to `contents: read` by default and escalated per-job as needed. As of 2026-05-06:

- 18/32 workflows have explicit `permissions:` block
- 14/32 inherit default (write — too permissive)

Permissions audit + tightening is tracked separately as a follow-up to this P1.2 (creating a single permission block on each workflow is a mechanical change but spans 14 files; deferred to avoid scope creep on this PR).

## References

- Issue #186 P1.2 — Q2 2026 Security Review action item
- [GitHub Security Lab — pin third-party actions](https://github.blog/security/supply-chain-security/dependabot-just-got-better-at-finding-supply-chain-vulnerabilities/)
- [tj-actions/changed-files compromise (March 2025)](https://github.com/tj-actions/changed-files/issues/1) — historical incident motivating this policy
- `.github/dependabot.yml` — `github-actions` ecosystem registration (weekly updates)
- `.github/workflows/validate-workflows.yml` — CI enforcement
