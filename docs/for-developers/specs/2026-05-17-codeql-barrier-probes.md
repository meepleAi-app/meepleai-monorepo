# CodeQL Barrier Verification — Synthetic Probes Design

**Spec date**: 2026-05-17
**Issue**: [#1196](https://github.com/meepleAi-app/meepleai-monorepo/issues/1196)
**Parent ADR**: [ADR-058](../../for-claude/architecture/adr/adr-058-canonical-log-sanitizers.md) §Acceptance bullet 5
**Spec-panel review**: 2026-05-16 (Wiegers, Adzic, Crispin, Fowler, Nygard)
**Status**: Accepted

## Problem

ADR-058 §Acceptance bullet 5 requires empirical, periodic validation that the CodeQL barrier system is **alive on both sides**:

- **Positive**: a sanitized log statement (`LogSanitizer.Sanitize(DataMasking.MaskEmail(...))`) produces **zero** `cs/exposure-of-sensitive-information` alerts → barrier recognition works
- **Negative**: an unsanitized log statement (`_logger.LogInformation("{Email}", rawEmail)`) produces **at least one** alert → the rule is still active and not globally disabled

Without continuous validation we cannot empirically distinguish between:
1. The barrier is working perfectly, and
2. The rule is silently inert (e.g. `paths-ignore` drift, MaD pack load failure, query-suite mismatch).

The barrier infrastructure went through 8 empirical iterations in PR #1189 before pack-loading stabilised; nothing today prevents a similar silent regression.

## Decision

**Option C** (per spec-panel recommendation 2026-05-16): hybrid synthetic probes inside a dedicated test project, exercised by a separate workflow on a monthly cadence + on-demand `workflow_dispatch`.

### Rejected alternatives

| Option | Why rejected |
|--------|--------------|
| A — persistent synthetic file with `[SuppressMessage]` in production assembly | Synthetic code in production assembly is fragile (future refactor may delete it) and `[SuppressMessage]` hides the very signal the probe is meant to assert. |
| B — documentation-only manual procedure | Empirically never executed in practice; relies on human memory which is the failure mode the barrier infrastructure already exhibited (#1188). |

## Architecture

### 1. Probe project

A new test-tier C# project, never shipped to production:

```
apps/api/tests/CodeqlBarrierSmoke/
├── CodeqlBarrierSmoke.csproj       # netstandard / net9.0, ProjectReference to Api.csproj
└── Probes/
    ├── Sanitized.cs                 # SHOULD produce 0 alerts (barrier verification)
    └── Unsanitized.cs               # MUST produce ≥1 alert (rule alive verification)
```

Two-file split (vs single file with line-range markers) chosen because SARIF assertions filter by `physicalLocation.artifactLocation.uri` — robust against line shifts caused by future edits, comments, or formatting.

### 2. CodeQL configuration split

| File | Used by | Includes probes? |
|------|---------|-------------------|
| `.github/codeql/codeql-config.yml` | `security-scan.yml` (every PR + Monday cron) | **No** — adds explicit exclude `apps/api/tests/CodeqlBarrierSmoke/**` to `paths-ignore`. The pre-existing `**/Tests/**` exclude is case-sensitive on Linux runners and does not match the lowercase `tests/` directory. |
| `.github/codeql/codeql-config-with-probes.yml` (new) | `barrier-verification.yml` only | **Yes** — clone of the default config without the probe exclude. |

This avoids polluting normal PR scans with probe-induced alerts while letting the barrier-verification workflow surface them deliberately.

### 3. Workflow `barrier-verification.yml`

```yaml
on:
  schedule:
    - cron: '0 4 1 * *'   # First day of month at 04:00 UTC (after the Monday 02:00 security-scan cron)
  workflow_dispatch:       # Manual run for ad-hoc verification

permissions:
  contents: read
  security-events: write
  actions: read
```

#### Job steps

1. **Checkout**.
2. **Regression guard for the regression guard**: assert both probe files still exist on disk. If a refactor accidentally deletes either, fail loudly here rather than silently producing a green run.
3. **MaD pack rowCount verify** — same step as `security-scan.yml` (`EXPECTED_PACK_ROWS=14`).
4. **Setup .NET 9.0** + `dotnet build apps/api/tests/CodeqlBarrierSmoke/`.
5. **CodeQL init** with `config-file: ./.github/codeql/codeql-config-with-probes.yml`.
6. **CodeQL analyze** with `CODEQL_ACTION_EXTRA_OPTIONS` injecting the MaD pack (same pattern as `security-scan.yml`).
7. **Assertion step** — SARIF-based:

```bash
sarif="${{ runner.temp }}/codeql-sarif/csharp.sarif"
rule_id="cs/exposure-of-sensitive-information"

sanitized=$(jq --arg r "$rule_id" \
  '[.runs[].results[]? | select(.ruleId == $r)
   | select(.locations[0].physicalLocation.artifactLocation.uri
            | contains("CodeqlBarrierSmoke/Probes/Sanitized.cs"))] | length' "$sarif")

unsanitized=$(jq --arg r "$rule_id" \
  '[.runs[].results[]? | select(.ruleId == $r)
   | select(.locations[0].physicalLocation.artifactLocation.uri
            | contains("CodeqlBarrierSmoke/Probes/Unsanitized.cs"))] | length' "$sarif")

[[ "$sanitized" == "0" ]] || { echo "::error::Sanitized probe raised $sanitized alert(s) — barrier broken"; exit 1; }
[[ "$unsanitized" -ge 1 ]] || { echo "::error::Unsanitized probe raised 0 alerts — rule appears disabled"; exit 1; }
echo "::notice::Barrier verification OK: sanitized=0, unsanitized=$unsanitized"
```

8. **Slack notify on failure** — reuse `notify-slack.yml` pattern from `security-scan.yml`.

### 4. Probe content

#### Sanitized.cs (expected: 0 alerts)

```csharp
// SYNTHETIC barrier verification probe — see ADR-058 §5 + issue #1196.
// Removing this file breaks the monthly barrier-verification workflow.
namespace Api.Tests.CodeqlBarrierSmoke.Probes;

public static class Sanitized
{
    public static void Probe(ILogger logger, string email)
    {
        // BOTH masking (file-content-store barrier) AND log-forging stripping
        // applied → CodeQL must recognize this as safe.
        var safe = LogSanitizer.Sanitize(DataMasking.MaskEmail(email));
        logger.LogInformation("user email (masked): {Email}", safe);
    }
}
```

#### Unsanitized.cs (expected: ≥1 alert)

```csharp
// SYNTHETIC negative probe — raw user input flows into log statement.
// MUST raise `cs/exposure-of-sensitive-information` for the rule to be considered alive.
// Removing this file breaks the monthly barrier-verification workflow.
namespace Api.Tests.CodeqlBarrierSmoke.Probes;

public static class Unsanitized
{
    public static void Probe(ILogger logger, string email)
    {
        // INTENTIONAL violation — do not "fix" this. See ADR-058 §5.
        logger.LogInformation("user email (raw): {Email}", email);
    }
}
```

## Acceptance mapping (issue #1196)

| Acceptance bullet | Met by |
|-------------------|--------|
| Design doc with chosen option + paths-ignore flip mechanism | This document (Option C, config-file split). |
| Synthetic probe file at appropriate location with comments | `Probes/Sanitized.cs` + `Probes/Unsanitized.cs` with header comments referencing ADR-058 §5 and #1196. |
| Workflow `barrier-verification.yml` that runs probes and validates expected counts | New file, SARIF-based assertion logic above. |
| Schedule: monthly cron | `cron: '0 4 1 * *'` — first day of month, 04:00 UTC. |
| Failure case: probe file deleted → workflow fails | Step 2 (regression guard for the regression guard). |

## Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Future refactor "cleans up" the unsanitized probe (looks like a security bug) | Medium | Workflow turns green falsely | Header comment in file + regression-guard step that fails when file is missing |
| Lowercase `tests/` collides with new directory containing real `Tests` content | Low | Probe accidentally scanned in default config | Exclude path is fully qualified `apps/api/tests/CodeqlBarrierSmoke/**`, not a glob |
| MaD pack row count changes legitimately, breaking the rowCount gate | Low | Workflow fails | Bump `EXPECTED_PACK_ROWS` in this workflow when pack grows (same convention as `security-scan.yml`) |
| Probe project builds break Api solution build | Low | Solution-wide build red | Project is standalone — only `ProjectReference` is to `Api.csproj`. Not added to default `dotnet test` discovery (no test classes). |
| CodeQL CLI rule ID changes between versions | Low | Assertion silently misses | jq `select(.ruleId == "cs/exposure-of-sensitive-information")` is explicit; CodeQL CLI version is pinned via `codeql-action/init@v4` |

## Out of scope

- **`cs/log-forging` probes**: deferred to a follow-up. The MaD pack registers both `log-injection` and `file-content-store` barriers; the latter is the empirically validated kind (PR #1191). A `log-injection` probe can be added later as a 3rd/4th file under `Probes/`.
- **Option D — Roslyn analyzer**: tracked separately in #1197 (marked Optional).
- **Replacing the Monday `security-scan.yml`**: the barrier verification is a complement, not a replacement.

## Implementation plan

1. Spec doc (this file).
2. `apps/api/tests/CodeqlBarrierSmoke/CodeqlBarrierSmoke.csproj` — minimal compile-only project.
3. `Probes/Sanitized.cs` + `Probes/Unsanitized.cs`.
4. Update `.github/codeql/codeql-config.yml` (add probe exclude).
5. Add `.github/codeql/codeql-config-with-probes.yml` (clone without exclude).
6. Add `.github/workflows/barrier-verification.yml`.
7. Manual `workflow_dispatch` run on the PR branch to validate end-to-end before merge.
8. Update ADR-058 references + `pii-safe-logging.md` with link to the new workflow.

## References

- [ADR-058](../../for-claude/architecture/adr/adr-058-canonical-log-sanitizers.md) — canonical log sanitizer strategy
- [Issue #1196](https://github.com/meepleAi-app/meepleai-monorepo/issues/1196) — this work item
- [Issue #1181](https://github.com/meepleAi-app/meepleai-monorepo/issues/1181) — closed parent (CodeQL high alerts cluster)
- [PR #1189](https://github.com/meepleAi-app/meepleai-monorepo/pull/1189) — empirical MaD pack-load fix (8 iterations)
- [PR #1191](https://github.com/meepleAi-app/meepleai-monorepo/pull/1191) — `file-content-store` barriers extended
- `.github/workflows/security-scan.yml` — reference workflow for MaD pack load + SARIF gating patterns
- `.github/codeql/meepleai/sanitizers-extensions/models/sanitizers.model.yml` — 14 barrier registrations
