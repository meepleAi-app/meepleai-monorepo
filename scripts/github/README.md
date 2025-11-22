# GitHub Workflow Scripts

PowerShell and Bash scripts for GitHub repository management and feature development workflow.

## Scripts

### 🏷️ **create-label.ps1** ⚠️ DEPRECATED
**Status:** ⚠️ **Obsolete** - Use `tools/setup/setup-github-labels.sh` instead

**Original Purpose:** Create GitHub labels for issue categorization

**Why deprecated:**
- Single-label creation is inefficient
- Replaced by batch label creation in `tools/setup/setup-github-labels.sh`
- That script creates complete label taxonomy (priorities, types, epics, areas, status)

**Migration:**
```bash
# Instead of:
# .\scripts\github\create-label.ps1 -Label "bug" -Color "d73a4a"

# Use:
bash tools/setup/setup-github-labels.sh
```

**Recommendation:** 🗑️ **DELETE** this file - functionality moved to tools/setup/

---

### 🌿 **create-new-feature.ps1**
**Purpose:** Scaffold a new feature branch with spec-driven development structure

**What it does:**
1. Creates feature directory in `specs/NNN-feature-name/`
2. Generates boilerplate files:
   - `feature-spec.md` - Feature specification template
   - `plan.md` - Implementation plan
   - `tasks.md` - Task breakdown (optional)
3. Creates Git branch matching feature directory name
4. Sets up Spec-Driven Development workflow structure

**Usage:**
```powershell
# Create new feature with automatic numbering
.\scripts\github\create-new-feature.ps1 "User authentication with OAuth"

# JSON output (for scripting)
.\scripts\github\create-new-feature.ps1 -Json "Add dark mode support"
```

**Output:**
```
Created feature directory: specs/042-user-authentication-with-oauth/
Created branch: 042-user-authentication-with-oauth
Files created:
  - feature-spec.md (specification template)
  - plan.md (implementation plan template)
```

**Who uses it:** Developers starting new features using spec-driven workflow
**When:** Beginning of new feature development sprint
**Requirements:** Git repository, PowerShell 5.1+

**Related:**
- `scripts/utils/check-prerequisites.ps1` - Validates feature structure
- `scripts/utils/common.ps1` - Shared utility functions
- See `docs/02-development/spec-driven-development.md` for workflow guide

---

## Spec-Driven Development Workflow

This directory supports the **Spec-Driven Development** workflow used in MeepleAI:

1. **Specification Phase:**
   - Run `create-new-feature.ps1` to scaffold feature
   - Fill out `feature-spec.md` with requirements
   - Define acceptance criteria

2. **Planning Phase:**
   - Complete `plan.md` with implementation approach
   - Break down into tasks in `tasks.md`
   - Review with team

3. **Implementation Phase:**
   - Use `check-prerequisites.ps1` to validate structure
   - Implement according to plan
   - Track tasks in `tasks.md`

4. **Review Phase:**
   - Create PR with spec + plan as context
   - Reviewers reference specification

---

## Recommended Actions

### Immediate
- ✅ **KEEP:** `create-new-feature.ps1` (actively used for spec-driven development)
- 🗑️ **DELETE:** `create-label.ps1` (functionality moved to `tools/setup/setup-github-labels.sh`)

### Future Enhancements
- Add bash version of `create-new-feature.ps1` for Linux/macOS users
- Integrate with GitHub Projects API for automatic issue creation
- Add templates for different feature types (UI, API, Infrastructure)

---

**Last Updated:** 2025-11-22
**Maintained by:** Development team
