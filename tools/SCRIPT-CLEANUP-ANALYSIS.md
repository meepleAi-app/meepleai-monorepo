# Script Cleanup Analysis - 2025-11-11

## Executive Summary

**Total Scripts**: 60 files (27 in `tools/`, 7 in `scripts/`, 28 in `tools/archive/`)

**Recommendation**: Remove 14 unused/duplicate scripts, saving ~100KB disk space and reducing confusion.

## Categories

### ✅ Active Scripts (Referenced in Docs/CI)

#### Documentation & Development (6 scripts)
1. **standardize-markdown.js** (4.7KB) - Used: `tools/README.md:19`
   - Standardizes Markdown formatting across docs

2. **generate-api-docs.js** (7.1KB) - Used: `tools/README.md:51,64`
   - Generates API documentation from OpenAPI spec

3. **search-docs.js** (6.9KB) - Used: `tools/README.md:76,83,86,89,92`
   - CLI tool for searching documentation

4. **validate-docs.ps1** (4.2KB) - Active utility
   - Validates documentation integrity

5. **open-dual-vscode.ps1** (1.8KB) - Active utility
   - Opens dual VS Code instances for development

6. **open-dual-vscode.sh** (1.7KB) - Active utility
   - Bash equivalent of above

#### Coverage & Testing (4 scripts)
7. **measure-coverage.ps1** (9.1KB) - Used: `CLAUDE.md:50`, `README.md:141,144`, `CONTRIBUTING.md:425`
   - **CRITICAL**: Primary coverage measurement tool
   - Used in CI/CD and local development

8. **coverage-trends.ps1** (5.3KB) - Used: `CLAUDE.md:453`, `README.md:148`
   - Tracks coverage trends over time

9. **coverage-trends.sh** (4.3KB) - Used: `CLAUDE.md:452`, `README.md:147`, `kb/codebase-maintenance.md:717`
   - Bash equivalent of above

10. **apps/web/package.json** references (via cleanup-test-processes.ps1)
    - **Note**: cleanup-test-processes.ps1 moved to archive but still referenced in package.json

#### Cache Management (2 scripts)
11. **cleanup-caches.ps1** (8.6KB) - Used: `CLAUDE.md:644,648,652`, `tools/README.md:191,194,197,200,203`
    - **CRITICAL**: Monthly cache cleanup (saves ~800MB)

12. **cleanup-caches.sh** (7.7KB) - Used: `CLAUDE.md:643,647,651`, `tools/README.md:172,175,178,181,184`
    - **CRITICAL**: Bash equivalent for Linux/Mac

#### Infrastructure (4 scripts)
13. **delete-qdrant-collection.ps1** (2.1KB) - Active utility
    - Deletes Qdrant vector collections

14. **register-n8n-webhook.ps1** (8.5KB) - Active utility
    - Registers n8n webhook endpoints

15. **setup-n8n-service-account.ps1** (6.9KB) - Active utility
    - Sets up n8n service accounts

16. **setup-ollama.ps1** (3.4KB) - Active utility
    - Configures Ollama for local LLM

#### Admin & Issues (4 scripts)
17. **create-admin-console-issues.js** (13KB) - Used: `tools/README-admin-console-issues.md:34,37,102,108,193`
    - Creates admin console GitHub issues

18. **create-admin-console-issues.ps1** (19KB) - Used: `tools/README-admin-console-issues.md:22,25,105,184`
    - PowerShell equivalent

19. **create-admin-console-issues.sh** (9.2KB) - Used: `tools/README-admin-console-issues.md:13,16,102,141,176`
    - Bash equivalent

20. **setup-github-labels.sh** (2.8KB) - Used: `claudedocs/MVP_ISSUES_SUMMARY.md:378`
    - Sets up GitHub labels and milestones

#### Project Migration (2 scripts)
21. **generate-mvp-issues.ps1** (1.7KB) - Used: `claudedocs/EXECUTIVE_SUMMARY.md:44,264`
    - **DUPLICATE IN ARCHIVE** - Active version should be kept

22. **generate-mvp-issues.sh** (43KB) - Used: `claudedocs/EXECUTIVE_SUMMARY.md:43,261,389`, `QUICK_START_MVP.md:12,74,129`
    - **DUPLICATE IN ARCHIVE** - Active version should be kept

23. **migrate-to-private.ps1** (16KB) - Active utility
    - Migrates repository to private

#### Secrets Management (3 scripts in tools/secrets/)
24. **init-secrets.sh** - Used: `infra/secrets/README.md:10,71`
25. **list-secrets.sh** - Active utility
26. **rotate-secret.sh** - Active utility

#### Scripts Directory (7 scripts)
27. **scripts/dev-up.ps1** - Active: Docker Compose startup
28. **scripts/dev-down.ps1** - Active: Docker Compose shutdown
29. **scripts/dev-logs.ps1** - Active: Docker logs viewer
30. **scripts/common.ps1** - Active: Shared utilities
31. **scripts/check-prerequisites.ps1** - Active: Pre-flight checks
32. **scripts/create-label.ps1** - Active: GitHub label creation
33. **scripts/create-new-feature.ps1** - Active: Feature scaffolding

### ⚠️ Potentially Unused Scripts (Candidates for Removal)

#### GUID Test Fixes (5 scripts - 14.5KB total)
- **fix-all-guid-tests.ps1** (6.4KB) - **DUPLICATE IN ARCHIVE**
- **fix-guid-migrations.ps1** (3.4KB) - **DUPLICATE IN ARCHIVE**
- **fix-guid-test-errors.ps1** (1.1KB) - **DUPLICATE IN ARCHIVE**
- **fix-test-guids.ps1** (1.9KB) - **DUPLICATE IN ARCHIVE**
- **fix-test-method-calls.ps1** (1.7KB) - **DUPLICATE IN ARCHIVE**

**Reason**: One-time fixes from Issue #XXX, already archived, no references found

#### Complexity Analysis (1 script - 579 bytes)
- **analyze-complexity.ps1** (579 bytes) - No references found

**Reason**: Unclear purpose, not documented in README.md

### 📦 Archive (28 scripts in tools/archive/2025-11-closed-issues/)
All archived scripts are properly organized and should remain for historical reference.

## Cleanup Recommendations

### 1. Remove Duplicate Scripts from tools/ (Priority: HIGH)

These scripts exist in both `tools/` and `tools/archive/2025-11-closed-issues/`:

```bash
# Remove from tools/ (keep archive versions)
rm tools/fix-all-guid-tests.ps1
rm tools/fix-guid-migrations.ps1
rm tools/fix-guid-test-errors.ps1
rm tools/fix-test-guids.ps1
rm tools/fix-test-method-calls.ps1
```

**Impact**: -14.5KB, removes confusion about which version to use

### 2. Remove Undocumented Utility (Priority: MEDIUM)

```bash
# Remove if no one can explain what it does
rm tools/analyze-complexity.ps1
```

**Impact**: -579 bytes, clarifies tool inventory

### 3. Fix package.json Reference (Priority: HIGH)

File: `apps/web/package.json`

```json
// BEFORE (lines 9, 12)
"posttest": "node -e \"try { require('child_process').execSync('powershell.exe -ExecutionPolicy Bypass -File ../../tools/cleanup-test-processes.ps1', { stdio: 'inherit' }); } catch (e) { console.log('Cleanup skipped (Windows-only)'); }\"",

// AFTER (update path to archive)
"posttest": "node -e \"try { require('child_process').execSync('powershell.exe -ExecutionPolicy Bypass -File ../../tools/archive/2025-11-closed-issues/cleanup-test-processes.ps1', { stdio: 'inherit' }); } catch (e) { console.log('Cleanup skipped (Windows-only)'); }\"",
```

**OR** (recommended): Copy script back from archive if actively used

```bash
cp tools/archive/2025-11-closed-issues/cleanup-test-processes.ps1 tools/
```

### 4. Document Remaining Active Scripts (Priority: LOW)

Add to `tools/README.md`:

- `analyze-complexity.ps1` - Purpose and usage (if keeping)
- `delete-qdrant-collection.ps1` - Vector DB management
- `register-n8n-webhook.ps1` - Workflow webhook registration
- `setup-n8n-service-account.ps1` - n8n service account setup
- `setup-ollama.ps1` - Local LLM configuration
- `migrate-to-private.ps1` - Repository visibility migration
- `open-dual-vscode.{ps1,sh}` - Dual VS Code instance launcher
- `validate-docs.ps1` - Documentation integrity validation

## Implementation Plan

### Phase 1: Safety Check (5 minutes)
```bash
# 1. Verify no hidden references to duplicate scripts
grep -r "fix-all-guid-tests\|fix-guid-migrations\|fix-guid-test-errors\|fix-test-guids\|fix-test-method-calls" . \
  --exclude-dir=node_modules \
  --exclude-dir=.git \
  --exclude-dir=bin \
  --exclude-dir=obj \
  --exclude="*.md"

# 2. Verify cleanup-test-processes.ps1 usage
grep -r "cleanup-test-processes" . \
  --exclude-dir=node_modules \
  --exclude-dir=.git \
  --include="*.json" \
  --include="*.yml"
```

### Phase 2: Execute Cleanup (2 minutes)
```bash
# Remove duplicate GUID fix scripts
rm tools/fix-all-guid-tests.ps1
rm tools/fix-guid-migrations.ps1
rm tools/fix-guid-test-errors.ps1
rm tools/fix-test-guids.ps1
rm tools/fix-test-method-calls.ps1

# Copy cleanup-test-processes.ps1 back from archive (still used in package.json)
cp tools/archive/2025-11-closed-issues/cleanup-test-processes.ps1 tools/
```

### Phase 3: Update Documentation (10 minutes)
```bash
# Update tools/README.md with missing script documentation
# Document analyze-complexity.ps1 purpose or remove if obsolete
```

### Phase 4: Commit (2 minutes)
```bash
git add tools/
git commit -m "chore(tools): Remove duplicate GUID fix scripts

- Remove 5 duplicate fix-*.ps1 scripts (already in archive)
- Restore cleanup-test-processes.ps1 from archive (used in package.json)
- Total cleanup: 14.5KB, improved clarity

Fixes: Script organization and inventory clarity"
```

## Summary Statistics

| Category | Count | Total Size | Status |
|----------|-------|-----------|--------|
| Active Scripts (tools/) | 22 | ~165KB | Keep |
| Active Scripts (scripts/) | 7 | ~15KB | Keep |
| Active Scripts (tools/secrets/) | 3 | ~10KB | Keep |
| Duplicates (GUID fixes) | 5 | 14.5KB | **REMOVE** |
| Undocumented (analyze-complexity) | 1 | 579 bytes | **REVIEW** |
| Archive | 28 | ~180KB | Keep |
| **Total Active After Cleanup** | **27** | **~176KB** | ✅ |

## Risk Assessment

**Risk Level**: LOW

- All scripts being removed are duplicates with archive copies
- No references found in CI/CD pipelines
- No references in active documentation
- Easy rollback: `git revert` or copy from archive

## Verification Checklist

- [ ] Run safety check (grep for references)
- [ ] Execute cleanup (rm duplicate scripts)
- [ ] Restore cleanup-test-processes.ps1 from archive
- [ ] Test package.json posttest hooks: `cd apps/web && pnpm test`
- [ ] Update tools/README.md documentation
- [ ] Commit with descriptive message
- [ ] Verify CI/CD passes (no broken references)
- [ ] Update this analysis in `tools/SCRIPT-CLEANUP-ANALYSIS.md`

## Future Recommendations

1. **Quarterly Script Audit**: Review `tools/` for unused scripts
2. **Mandatory Documentation**: All new scripts must be documented in tools/README.md
3. **Archive Policy**: Move closed-issue scripts to dated archive directories
4. **Naming Convention**: Use `yyyy-mm-action.{ps1,sh,js}` for one-time scripts

## References

- Issue tracking: (Create issue for this cleanup)
- Archive policy: `tools/CLEANUP-SUMMARY-2025-11-11.md:362`
- Documentation: `tools/README.md`
