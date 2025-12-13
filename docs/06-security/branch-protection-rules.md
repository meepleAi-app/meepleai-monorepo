# Branch Protection Rules

> **Security Classification**: Internal
> **Last Updated**: 2025-12-13
> **Owner**: Engineering Lead

## Overview

This document outlines the recommended branch protection rules for the MeepleAI monorepo. These rules help maintain code quality, prevent accidental pushes to production branches, and ensure proper review processes.

## Recommended Protection Rules for `main` Branch

### 1. Require Pull Request Reviews

| Setting | Recommended Value | Rationale |
|---------|-------------------|-----------|
| **Required approving reviews** | 1 | Ensures at least one team member reviews changes |
| **Dismiss stale reviews** | ✅ Enabled | Re-review required after new commits |
| **Require review from code owners** | ✅ Enabled | Ensures domain experts review relevant code |
| **Restrict dismissals** | Team leads only | Prevents unauthorized dismissal of reviews |

### 2. Require Status Checks

Enable these required status checks before merging:

```yaml
Required Status Checks:
  - ci-web-unit              # Frontend unit tests
  - ci-api-unit-integration  # Backend tests
  - validate-schemas         # Schema validation
  - validate-api-codegen     # API client generation
  - codeql-analysis          # Security scanning
  - dependency-scan          # Vulnerability scanning
```

| Setting | Recommended Value |
|---------|-------------------|
| **Require status checks to pass** | ✅ Enabled |
| **Require branches to be up to date** | ✅ Enabled |

### 3. Require Conversation Resolution

| Setting | Recommended Value | Rationale |
|---------|-------------------|-----------|
| **Require conversation resolution** | ✅ Enabled | All review comments must be addressed |

### 4. Require Signed Commits

| Setting | Recommended Value | Rationale |
|---------|-------------------|-----------|
| **Require signed commits** | ⚠️ Optional | See [Signed Commits Guide](./signed-commits-guide.md) |

### 5. Require Linear History

| Setting | Recommended Value | Rationale |
|---------|-------------------|-----------|
| **Require linear history** | ✅ Enabled | Clean, readable git history |
| **Merge method** | Squash and merge | Combines PR commits into single commit |

### 6. Include Administrators

| Setting | Recommended Value | Rationale |
|---------|-------------------|-----------|
| **Include administrators** | ✅ Enabled | Admins follow same rules as everyone |

### 7. Restrict Push Access

| Setting | Recommended Value |
|---------|-------------------|
| **Restrict pushes** | ✅ Enabled |
| **Allowed actors** | None (PRs only) |

### 8. Block Force Pushes

| Setting | Recommended Value | Rationale |
|---------|-------------------|-----------|
| **Block force pushes** | ✅ Enabled | Prevents history rewriting |

### 9. Block Deletions

| Setting | Recommended Value | Rationale |
|---------|-------------------|-----------|
| **Block deletions** | ✅ Enabled | Prevents accidental branch deletion |

## Implementation Guide

### GitHub UI Configuration

1. Navigate to **Repository Settings** → **Branches**
2. Click **Add rule** under "Branch protection rules"
3. Enter `main` as the branch name pattern
4. Configure each setting as documented above
5. Click **Create** / **Save changes**

### GitHub CLI Configuration

```bash
# Install GitHub CLI if not already installed
# https://cli.github.com/

# Apply branch protection (requires admin access)
gh api repos/{owner}/{repo}/branches/main/protection \
  --method PUT \
  --input - << 'EOF'
{
  "required_status_checks": {
    "strict": true,
    "contexts": [
      "ci-web-unit",
      "ci-api-unit-integration",
      "validate-schemas",
      "validate-api-codegen",
      "codeql-analysis",
      "dependency-scan"
    ]
  },
  "enforce_admins": true,
  "required_pull_request_reviews": {
    "dismiss_stale_reviews": true,
    "require_code_owner_reviews": true,
    "required_approving_review_count": 1
  },
  "restrictions": null,
  "required_linear_history": true,
  "allow_force_pushes": false,
  "allow_deletions": false,
  "required_conversation_resolution": true
}
EOF
```

### Terraform Configuration (Optional)

For infrastructure-as-code teams:

```hcl
resource "github_branch_protection" "main" {
  repository_id = github_repository.meepleai.node_id
  pattern       = "main"

  required_status_checks {
    strict   = true
    contexts = [
      "ci-web-unit",
      "ci-api-unit-integration",
      "validate-schemas",
      "validate-api-codegen",
      "codeql-analysis",
      "dependency-scan"
    ]
  }

  required_pull_request_reviews {
    dismiss_stale_reviews           = true
    require_code_owner_reviews      = true
    required_approving_review_count = 1
  }

  enforce_admins         = true
  require_signed_commits = false  # Optional - see signed commits guide

  allows_deletions    = false
  allows_force_pushes = false

  required_linear_history          = true
  require_conversation_resolution  = true
}
```

## Feature Branch Protection (Optional)

For teams that want lighter protection on feature branches:

| Branch Pattern | Protection Level |
|---------------|------------------|
| `main` | Full protection (as above) |
| `release/*` | Same as main |
| `feature/*` | CI required, no approval required |
| `fix/*` | CI required, no approval required |

## Audit and Compliance

### Regular Review Schedule

- **Monthly**: Review branch protection settings
- **Quarterly**: Audit bypass permissions
- **Annually**: Full security assessment

### Audit Log

Branch protection changes are logged in:
- GitHub Audit Log (Enterprise)
- Repository Settings → Security → Audit log

## Exceptions Process

If a branch protection rule needs to be bypassed:

1. **Document the reason** in a GitHub Issue
2. **Get approval** from Engineering Lead
3. **Temporary bypass** only (max 24 hours)
4. **Post-mortem** if bypass was for incident response

## Related Documentation

- [CONTRIBUTING.md](../../CONTRIBUTING.md) - Contribution guidelines
- [CODEOWNERS](.github/CODEOWNERS) - Code ownership definitions
- [Signed Commits Guide](./signed-commits-guide.md) - GPG signing setup
- [CI/CD Workflows](../.github/workflows/) - Automated checks

## References

- [GitHub Branch Protection Documentation](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches)
- [GitHub Status Checks](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/collaborating-on-repositories-with-code-quality-features/about-status-checks)
