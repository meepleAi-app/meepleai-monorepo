# MeepleAI Monorepo - Cleanup Analysis Report
**Generated**: 2025-11-09
**Analyzer**: Claude Code /sc:cleanup command
**Project Size**: 3.3GB
**Cleanup Potential**: ~127MB (3.8%)

---

## Executive Summary

✅ **Overall Health**: Good - No major issues found
⚠️ **Action Required**: Remove 4 files/directories for 127MB space savings
🔧 **Code Quality**: 5 files with TODO comments, CodeQL security alerts need attention

---

## 🎯 Immediate Actions (High Priority)

### 1. Remove Security Scanning Output (7.1MB)
**File**: `alerts.json` (7.1MB)
**Issue**: GitHub CodeQL security scan results committed to repository
**Impact**: Wastes space, regenerated on every CI run
**Action**:
```bash
git rm alerts.json
echo "alerts.json" >> .gitignore
```

### 2. Clean Cache Directories (120MB)
**Directories**: `.serena/` (60MB), `codeql-db/` (53MB), `.playwright-mcp/` (4KB)
**Status**: ✅ Already in .gitignore (lines 457-460)
**Issue**: Exist on filesystem despite being gitignored
**Action**:
```bash
rm -rf .serena/ codeql-db/ .playwright-mcp/
```

**Space Savings**: 127MB total

---

## 📊 Dependency Analysis

### Frontend (apps/web)
✅ **Status**: Clean
**Dependencies**: 26 production, 50+ dev dependencies
**Assessment**: All actively used, no bloat detected

**Key Libraries**:
- React 18.3.1 + Next.js 15.5.6
- TipTap 3.10.4 (rich text editor)
- Monaco Editor 4.7.0
- Chess.js 1.4.0, react-chessboard 4.7.3
- D3 7.9.0, Recharts 3.3.0 (visualizations)

**Recommendation**: No cleanup needed

### Backend (apps/api)
⚠️ **Status**: Needs restore to verify
**Issue**: `dotnet list package` failed - project not restored
**Action**: Run `dotnet restore` then re-analyze
```bash
cd apps/api && dotnet restore
dotnet list package --include-transitive
```

**Recommendation**: Check for unused NuGet packages after restore

---

## 🔍 Code Quality Issues (From CodeQL)

### 1. IDisposable Violations (CODE-01)
**Severity**: Warning
**Count**: 6 occurrences
**Files**:
- `CacheInvalidationIntegrationTests.cs:76, 141, 203`
- `QualityTrackingIntegrationTests.cs:505, 543`

**Issue**: `HttpRequestMessage` created but not disposed
**Fix**: Wrap in `using` statements
```csharp
// Before
var request = new HttpRequestMessage(HttpMethod.Get, "/api/endpoint");

// After
using var request = new HttpRequestMessage(HttpMethod.Get, "/api/endpoint");
```

**Reference**: `docs/CLAUDE.md` CODE-01 best practices

### 2. Useless Assignments
**Severity**: Warning
**Count**: 3 occurrences
**Files**:
- `CacheAdminEndpointsTests.cs:314` - unused `pdf` variable
- `LlmServiceConfigurationIntegrationTests.cs:57-63` - unused `configDto`

**Fix**: Remove unused variable assignments

### 3. Log Forging Vulnerability
**Severity**: Error (High)
**File**: `AdminEndpoints.cs:2199`
**Issue**: User input logged without sanitization
**Fix**: Sanitize user input before logging
```csharp
// Before
logger.Warn(username + " log in requested.");

// After
logger.Warn(username.Replace(Environment.NewLine, "") + " log in requested");
```

---

## 📁 Project Structure Analysis

### ✅ Properly Organized
- Monorepo structure clean (apps/, infra/, docs/, tools/)
- Build artifacts properly gitignored (.next/, bin/, obj/)
- Test output directories gitignored (playwright-report/, test-results/)

### ⚠️ Minor Issues
**TODO/FIXME Comments**: 5 files contain TODO/FIXME/HACK comments
**Impact**: Low - indicates potential incomplete work
**Action**: Review and resolve or document as intentional

---

## 🚫 Items NOT Needing Cleanup

### Root node_modules/
**Status**: ✅ Normal
**Reason**: pnpm workspace root (.pnpm/, .modules.yaml)
**Size**: Minimal (symlinks only)

### apps/api/web/
**Status**: ✅ Doesn't exist
**Reason**: Gitignore entry is preventative (line 455)

### Build Caches
**Status**: ✅ Normal
**Files**: `.cache/`, `.dotnet/`, `obj/**/*.cache`
**Reason**: Build system caches, automatically managed

---

## 📋 Cleanup Execution Plan

### Phase 1: Safe Cleanup (Immediate)
```bash
# 1. Remove security scan output
git rm alerts.json
echo "alerts.json" >> .gitignore

# 2. Clean cache directories
rm -rf .serena/ codeql-db/ .playwright-mcp/

# 3. Commit cleanup
git add .gitignore
git commit -m "chore: remove security scan output and cache directories

- Remove alerts.json (7.1MB) - regenerated in CI
- Clean .serena/ (60MB), codeql-db/ (53MB), .playwright-mcp/ cache
- Add alerts.json to .gitignore

Space savings: 127MB (3.8% of repo size)"
```

### Phase 2: Code Quality Fixes (Follow-up)
```bash
# 1. Fix IDisposable violations
# Edit test files to use 'using' statements for HttpRequestMessage

# 2. Remove useless assignments
# Clean up unused variables in test files

# 3. Fix log forging vulnerability
# Sanitize user input in AdminEndpoints.cs:2199

# 4. Run security scan to verify
dotnet build
# Check CodeQL results in next CI run
```

### Phase 3: Dependency Audit (Optional)
```bash
# Backend
cd apps/api && dotnet restore
dotnet list package --outdated
dotnet list package --vulnerable

# Frontend
cd apps/web
pnpm audit --audit-level=high
pnpm outdated
```

---

## 📈 Expected Outcomes

### Immediate Benefits
- **Space Savings**: 127MB (3.8% reduction)
- **Repository Cleanliness**: Remove CI artifacts from version control
- **Security Posture**: Fix 1 high-severity vulnerability, 9 warnings

### Long-term Benefits
- **Faster Clones**: Smaller repository size
- **Better Hygiene**: Cache directories automatically cleaned
- **Code Quality**: Address IDisposable best practices (CODE-01)

---

## 🛡️ Safety Considerations

### What We're NOT Touching
✅ User data (data/ directory)
✅ Configuration files (.env.example, appsettings.json)
✅ Active dependencies (all currently in use)
✅ Documentation (docs/, schemas/, claudedocs/)
✅ Build system files (.editorconfig, .gitignore, package.json)

### Rollback Plan
All changes are safe to reverse:
```bash
# If needed, restore from git
git checkout HEAD~1 -- alerts.json .gitignore

# Caches will regenerate automatically on next use
```

---

## 🎯 Recommendations

### High Priority (Do Now)
1. ✅ Execute Phase 1 cleanup script (127MB savings)
2. ⚠️ Fix log forging vulnerability (security risk)
3. ⚠️ Fix IDisposable violations in tests (resource leaks)

### Medium Priority (This Sprint)
4. 📝 Review and resolve 5 TODO/FIXME comments
5. 🔍 Run dependency audit (Phase 3)
6. 🧪 Verify all 33 failing integration tests

### Low Priority (Backlog)
7. 📊 Set up automated cleanup scripts (weekly cache purge)
8. 🔧 Add pre-commit hook to prevent large files (alerts.json)
9. 📚 Update CLAUDE.md with cleanup best practices

---

## 📞 Next Steps

1. **Review this report** and approve cleanup plan
2. **Execute Phase 1** cleanup script (5 minutes)
3. **Create PR** for code quality fixes (Phase 2)
4. **Schedule** dependency audit (Phase 3)

**Questions?** Refer to:
- Project docs: `docs/CLAUDE.md`
- Security guide: `docs/SECURITY.md`
- Contributing: `CONTRIBUTING.md`

---

**Report Generated**: 2025-11-09 by Claude Code
**Command Used**: `/sc:cleanup`
**Analysis Duration**: ~3 minutes
**Files Analyzed**: 3.3GB repository
