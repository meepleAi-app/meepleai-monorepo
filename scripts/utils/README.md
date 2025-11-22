# Utility Scripts

Shared PowerShell utility functions used across development workflow scripts.

## Scripts

### ✅ **check-prerequisites.ps1**
**Purpose:** Validate spec-driven development workflow prerequisites and feature structure

**What it does:**
1. Validates feature branch naming (must match specs directory)
2. Checks required files exist (`feature-spec.md`, `plan.md`, optionally `tasks.md`)
3. Resolves feature paths for spec-driven workflow
4. Outputs validation results in human-readable or JSON format

**Usage:**
```powershell
# Check task prerequisites (plan.md required)
.\scripts\utils\check-prerequisites.ps1 -Json

# Check implementation prerequisites (plan.md + tasks.md required)
.\scripts\utils\check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks

# Get feature paths only (no validation)
.\scripts\utils\check-prerequisites.ps1 -PathsOnly

# Show help
.\scripts\utils\check-prerequisites.ps1 -Help
```

**Output (JSON):**
```json
{
  "REPO_ROOT": "/home/user/meepleai-monorepo",
  "BRANCH": "042-user-authentication",
  "FEATURE_DIR": "specs/042-user-authentication",
  "FEATURE_SPEC": "specs/042-user-authentication/feature-spec.md",
  "IMPL_PLAN": "specs/042-user-authentication/plan.md",
  "TASKS": "specs/042-user-authentication/tasks.md",
  "AVAILABLE_DOCS": ["feature-spec.md", "plan.md"]
}
```

**Who uses it:**
- CI/CD pipelines validating feature branch structure
- Developers checking if spec files are ready
- Automated tools requiring feature metadata

**When:**
- Before starting implementation (verify plan exists)
- During PR creation (validate documentation complete)
- In pre-commit hooks (enforce workflow standards)

**Requirements:** PowerShell 5.1+, Git (optional for non-git repos)

---

### 🔧 **common.ps1**
**Purpose:** Shared utility functions for PowerShell scripts

**What it provides:**
- `Get-RepoRoot` - Find repository root directory
- `Get-CurrentBranch` - Get current Git branch or feature directory
- `Test-HasGit` - Check if running in Git repository
- `Test-FeatureBranch` - Validate feature branch naming convention
- `Get-FeaturePathsEnv` - Resolve all feature-related paths
- `Get-AvailableDocs` - List available documentation files

**Usage:**
```powershell
# Import in other scripts
. "$PSScriptRoot/../utils/common.ps1"

# Use functions
$repoRoot = Get-RepoRoot
$branch = Get-CurrentBranch
$paths = Get-FeaturePathsEnv

if (Test-FeatureBranch -Branch $branch) {
    Write-Host "Valid feature branch: $branch"
}
```

**Who uses it:**
- `check-prerequisites.ps1` for validation logic
- `create-new-feature.ps1` for feature scaffolding
- Future scripts needing repository information

**When:** Sourced at runtime by other PowerShell scripts
**Importance:** ⭐⭐⭐ Critical - shared dependency for all workflow scripts

**Equivalent:** Bash version exists in `tools/` for Linux/macOS

---

## Spec-Driven Development Support

These utilities enforce the **Spec-Driven Development** workflow:

### Feature Branch Convention
```
specs/NNN-feature-name/
├── feature-spec.md  (required)
├── plan.md          (required for implementation)
└── tasks.md         (optional, task breakdown)
```

Branch name must match: `NNN-feature-name`

### Environment Variable Override
Set `SPECIFY_FEATURE` environment variable to bypass Git branch detection:
```powershell
$env:SPECIFY_FEATURE = "042-user-authentication"
.\scripts\utils\check-prerequisites.ps1
```

Useful for non-Git workflows or CI environments.

---

## Integration with Other Scripts

```
scripts/
├── dev/
│   └── (uses common.ps1 indirectly via workflow)
├── github/
│   ├── create-new-feature.ps1 → uses common.ps1
│   └── create-label.ps1       → standalone (deprecated)
└── utils/
    ├── common.ps1             ← shared library
    └── check-prerequisites.ps1 → uses common.ps1
```

---

## Troubleshooting

**Error: "Not a valid feature branch"**
```powershell
# Check current branch
git rev-parse --abbrev-ref HEAD

# Branch must match specs directory
# Example: specs/042-auth/ → branch: 042-auth
```

**Error: "Could not determine repository root"**
```powershell
# Ensure you're inside repository
cd /path/to/meepleai-monorepo

# Or set SPECIFY_FEATURE manually
$env:SPECIFY_FEATURE = "042-my-feature"
```

**Functions not available:**
```powershell
# Ensure you're sourcing the file correctly
. "$PSScriptRoot/../utils/common.ps1"  # Relative path
. ".\scripts\utils\common.ps1"         # From repo root
```

---

## Recommended Actions

### Current Status
- ✅ **KEEP ALL** - Both scripts actively used in spec-driven workflow
- ✅ **Well-documented** - Functions have clear purposes
- ✅ **No redundancy** - Each serves distinct role

### Future Enhancements
- Add bash equivalents for `check-prerequisites.ps1`
- Add automated tests for utility functions
- Document in `docs/02-development/spec-driven-development.md`

---

**Last Updated:** 2025-11-22
**Maintained by:** Development team
**See also:** `docs/02-development/spec-driven-development.md` (workflow guide)
