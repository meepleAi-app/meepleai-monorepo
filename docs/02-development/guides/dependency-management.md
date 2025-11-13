# Dependency Management - Best Practices

**Source**: Dependency audits (Issue #815, Completed 2025-11-09)
**Package Managers**: pnpm (frontend), NuGet (backend)
**Status**: Production-ready maintenance patterns

---

## Dependency Audit Schedule

### Regular Audits (Recommended Frequency)

| Type | Frequency | Command | Priority |
|------|-----------|---------|----------|
| **Security Vulnerabilities** | Weekly | `pnpm audit` + `dotnet list package --vulnerable` | 🔴 Critical |
| **Outdated Packages** | Monthly | `pnpm outdated` + `dotnet list package --outdated` | 🟡 Medium |
| **Unused Dependencies** | Quarterly | `depcheck` (pnpm) + manual review (dotnet) | 🟢 Low |
| **License Compliance** | Quarterly | `license-checker` (pnpm) + `dotnet list package` | 🟡 Medium |

---

## Frontend Dependency Management (pnpm)

### Security Audit

**Command**:
```bash
cd apps/web
pnpm audit --audit-level=high
```

**Action on Vulnerabilities**:

```bash
# 1. Check severity
pnpm audit

# 2. Fix automatically (if safe)
pnpm audit --fix

# 3. Manual review for breaking changes
pnpm outdated | grep <vulnerable-package>
pnpm update <package>@latest

# 4. Test after update
pnpm test
pnpm build

# 5. Document in CHANGELOG
```

**Severity Levels**:
- **Critical/High**: Fix immediately (same day)
- **Medium**: Fix within 1 week
- **Low**: Fix in next sprint OR accept risk (document)

---

### Update Strategy

**Patch Updates** (e.g., 19.0.1 → 19.0.2):
```bash
# Safe to auto-update (bug fixes only)
pnpm update
```

**Minor Updates** (e.g., 19.0 → 19.1):
```bash
# Review changelog, test carefully
pnpm outdated
pnpm update <package>@^19.1.0
pnpm test && pnpm build
```

**Major Updates** (e.g., 18.x → 19.x):
```bash
# Plan as separate task/issue (breaking changes likely)
# Example: React 18 → 19 (Issue #823, 8h effort)

# 1. Research breaking changes
# 2. Update dependencies
# 3. Fix compilation errors
# 4. Update tests
# 5. Full QA regression testing
```

---

### Package.json Maintenance

**Best Practices**:

```json
{
  "dependencies": {
    // ✅ GOOD: Caret ranges (minor updates allowed)
    "react": "^19.0.0",
    "next": "^16.0.0",

    // ⚠️ CAUTION: Exact versions (no auto-updates)
    "critical-lib": "1.2.3",

    // ❌ BAD: Wildcard (unpredictable)
    "some-lib": "*"
  },
  "devDependencies": {
    // ✅ Dev tools can be more relaxed
    "@types/node": "^20.0.0",
    "prettier": "^3.0.0"
  }
}
```

**Version Range Guide**:
- `^19.0.0` - Allow minor + patch (19.0.0 → 19.9.9 OK, 20.0.0 NO)
- `~19.0.0` - Allow patch only (19.0.0 → 19.0.9 OK, 19.1.0 NO)
- `19.0.0` - Exact version (no updates)
- `*` - Any version (❌ NEVER use in production!)

---

## Backend Dependency Management (NuGet)

### Security Audit

**Command**:
```bash
cd apps/api
dotnet list package --vulnerable --include-transitive
```

**Action on Vulnerabilities**:

```bash
# 1. Identify vulnerable package
dotnet list package --vulnerable

# 2. Update to secure version
dotnet add package <PackageName> --version <SafeVersion>

# 3. Or update all packages
dotnet restore
dotnet build

# 4. Verify tests
dotnet test

# 5. Check for breaking changes
# Review package CHANGELOG
```

**Transitive Dependencies**:
```bash
# Find transitive dependency path
dotnet list package --include-transitive | grep <vulnerable-package>

# Update root dependency (pulls new transitive)
dotnet add package <RootPackage> --version <NewVersion>
```

---

### Dependency Locking

**Enable Central Package Management** (Recommended):

**Directory.Packages.props** (Repository root):
```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <!-- Centralized version management -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="9.0.0" />
    <PackageVersion Include="Serilog.AspNetCore" Version="8.0.0" />
    <PackageVersion Include="MediatR" Version="12.4.1" />
    <!-- ... -->
  </ItemGroup>
</Project>
```

**Project .csproj** (Reference without version):
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.EntityFrameworkCore" />
  <PackageReference Include="Serilog.AspNetCore" />
</ItemGroup>
```

**Benefits**:
- Single source of truth for versions
- Consistent across all projects
- Easier to update (change once, applies everywhere)

---

## Dependency Health Monitoring

### Automated Tools

**Dependabot** (`.github/dependabot.yml`):

```yaml
version: 2
updates:
  # Frontend (pnpm)
  - package-ecosystem: "npm"
    directory: "/apps/web"
    schedule:
      interval: "weekly"
      day: "monday"
    open-pull-requests-limit: 5
    reviewers:
      - "tech-lead"
    labels:
      - "dependencies"
      - "frontend"

  # Backend (NuGet)
  - package-ecosystem: "nuget"
    directory: "/apps/api"
    schedule:
      interval: "weekly"
      day: "monday"
    open-pull-requests-limit: 5
    reviewers:
      - "tech-lead"
    labels:
      - "dependencies"
      - "backend"
```

**Benefits**:
- Automatic PRs for dependency updates
- Security alerts for vulnerabilities
- Automated merge (if tests pass + low risk)

---

### Manual Review Process

**Monthly Dependency Review** (30-60 min):

1. **Check Outdated**:
   ```bash
   cd apps/web && pnpm outdated
   cd apps/api && dotnet list package --outdated
   ```

2. **Prioritize Updates**:
   - Security vulnerabilities (high/critical) → Immediate
   - Major versions with features we need → Plan as task
   - Minor/patch updates → Auto-update if safe

3. **Update Low-Risk**:
   ```bash
   pnpm update  # Patch updates only (safe)
   dotnet restore
   ```

4. **Test**:
   ```bash
   pnpm test && pnpm build
   dotnet test
   ```

5. **Commit**:
   ```bash
   git add package.json pnpm-lock.yaml
   git commit -m "chore(deps): Update dependencies (patch versions)"
   ```

---

## Vulnerable Dependency Response

### High/Critical Vulnerabilities (Immediate Action)

**Timeline**: Fix within 24 hours

**Process**:
1. **Assess Impact**: Which features affected?
2. **Check Fix Availability**: Is patched version released?
3. **Update**:
   ```bash
   # Frontend
   pnpm update <package>@<safe-version>

   # Backend
   dotnet add package <Package> --version <SafeVersion>
   ```
4. **Test Thoroughly**: Run full test suite
5. **Deploy Hotfix**: If production affected
6. **Document**: Add to security incident log

---

### Medium Vulnerabilities (1 Week)

**Timeline**: Fix in next sprint

**Process**:
1. Create GitHub issue (label: `security`, `dependencies`)
2. Schedule in sprint planning
3. Update + test
4. Deploy with regular release

---

### Low Vulnerabilities (Next Quarter)

**Timeline**: Fix in maintenance window OR accept risk

**Process**:
1. Document risk acceptance (if no fix available)
2. Add to backlog
3. Revisit quarterly

---

## Breaking Changes Management

### Major Version Updates (e.g., React 18 → 19)

**Process** (Lessons from Issue #823):

1. **Research Phase** (2-4h):
   - Read official migration guide
   - Check breaking changes list
   - Identify affected files (Grep/Search)

2. **Planning Phase** (1-2h):
   - Estimate effort (1-2 days for small libs, 1-2 weeks for frameworks)
   - Create GitHub issue with checklist
   - Assign milestone

3. **Implementation Phase** (varies):
   - Update package.json
   - Fix compilation errors
   - Update tests
   - Fix runtime errors
   - Regression testing

4. **Validation Phase** (2-4h):
   - Full test suite (unit + integration + E2E)
   - Manual QA on critical features
   - Performance baseline check

5. **Documentation Phase** (1h):
   - Document breaking changes encountered
   - Update kb/ with new patterns
   - Update CHANGELOG

**Effort Example**: React 18 → 19 + Next.js 15 → 16 = 8h total

---

## Dependency Best Practices Summary

### Do's ✅
- Run security audits weekly
- Use caret ranges for dependencies (`^19.0.0`)
- Lock file committed (pnpm-lock.yaml, packages.lock.json)
- Test after every update
- Document major version migrations
- Use Dependabot for automation

### Don'ts ❌
- Never use wildcard versions (`*`)
- Never skip testing after updates
- Never update all dependencies at once (risky)
- Never ignore security vulnerabilities
- Never commit node_modules OR bin/obj

### When in Doubt
- Check package changelog/release notes
- Test in separate branch first
- Ask for code review on major updates
- Prefer stable over latest (let others find bugs first)

---

**Knowledge extracted from**:
- dependency-audit-2025-11-09.md (206 lines)
- Package management patterns from issue #815

**Status**: Production-ready dependency management for continuous maintenance
