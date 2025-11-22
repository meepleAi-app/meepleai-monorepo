<#
.SYNOPSIS
    Automates migration of MeepleAI repository from public to private visibility.

.DESCRIPTION
    This script automates the following tasks:
    - Creates backup of repository
    - Updates CI/CD workflows (disables CodeQL, enables Semgrep)
    - Updates documentation
    - Creates migration PR
    - Optionally changes repository visibility (requires confirmation)

.PARAMETER DryRun
    Run in dry-run mode (show changes without applying them)

.PARAMETER BackupPath
    Path to store repository backup (default: ../meepleai-backup)

.PARAMETER ChangeVisibility
    Actually change repository visibility on GitHub (requires gh CLI and confirmation)

.PARAMETER Strategy
    Security scan strategy: "semgrep" (default), "codeql" (requires Advanced Security), or "hybrid"

.EXAMPLE
    # Dry run to see what changes would be made
    pwsh tools/migrate-to-private.ps1 -DryRun

.EXAMPLE
    # Perform migration with Semgrep (free alternative to CodeQL)
    pwsh tools/migrate-to-private.ps1 -Strategy semgrep

.EXAMPLE
    # Perform migration and change visibility immediately (requires confirmation)
    pwsh tools/migrate-to-private.ps1 -Strategy semgrep -ChangeVisibility

.NOTES
    Author: DevOps Team
    Version: 1.0
    Requires: Git, GitHub CLI (gh), PowerShell 7+
    See: docs/guide/repository-visibility-migration.md for full guide
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$DryRun,

    [Parameter()]
    [string]$BackupPath = "../meepleai-backup",

    [Parameter()]
    [switch]$ChangeVisibility,

    [Parameter()]
    [ValidateSet("semgrep", "codeql", "hybrid")]
    [string]$Strategy = "semgrep"
)

$ErrorActionPreference = "Stop"

# ============================================================================
# Helper Functions
# ============================================================================

function Write-Header {
    param([string]$Text)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host " $Text" -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Text)
    Write-Host "✅ $Text" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Text)
    Write-Host "⚠️  $Text" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Text)
    Write-Host "❌ $Text" -ForegroundColor Red
}

function Write-Info {
    param([string]$Text)
    Write-Host "ℹ️  $Text" -ForegroundColor Blue
}

function Test-CommandExists {
    param([string]$Command)
    $null = Get-Command $Command -ErrorAction SilentlyContinue
    return $?
}

# ============================================================================
# Pre-flight Checks
# ============================================================================

Write-Header "Pre-flight Checks"

# Check Git
if (-not (Test-CommandExists "git")) {
    Write-Error "Git is not installed or not in PATH"
    exit 1
}
Write-Success "Git: $(git --version)"

# Check GitHub CLI (optional for visibility change)
if ($ChangeVisibility) {
    if (-not (Test-CommandExists "gh")) {
        Write-Error "GitHub CLI (gh) is required for -ChangeVisibility flag"
        Write-Info "Install: https://cli.github.com/"
        exit 1
    }
    Write-Success "GitHub CLI: $(gh --version | Select-Object -First 1)"
}

# Check we're in repository root
if (-not (Test-Path ".git")) {
    Write-Error "Not in repository root. Run from: D:\Repositories\meepleai-monorepo"
    exit 1
}
Write-Success "Repository root: $(Get-Location)"

# Check for uncommitted changes
$gitStatus = git status --porcelain
if ($gitStatus) {
    Write-Warning "Uncommitted changes detected. Please commit or stash before continuing."
    Write-Info "Run: git status"
    if (-not $DryRun) {
        exit 1
    }
}

# ============================================================================
# Backup Repository
# ============================================================================

Write-Header "Backup Repository"

$backupTimestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupBundlePath = "$BackupPath/meepleai-backup-$backupTimestamp.bundle"

if (-not $DryRun) {
    Write-Info "Creating Git bundle backup: $backupBundlePath"
    New-Item -ItemType Directory -Path $BackupPath -Force | Out-Null

    git bundle create $backupBundlePath --all
    if ($LASTEXITCODE -eq 0) {
        $bundleSize = (Get-Item $backupBundlePath).Length / 1MB
        Write-Success "Backup created: $backupBundlePath ($([math]::Round($bundleSize, 2)) MB)"
    } else {
        Write-Error "Backup failed"
        exit 1
    }
} else {
    Write-Info "[DRY RUN] Would create backup: $backupBundlePath"
}

# ============================================================================
# Update CI/CD Workflows
# ============================================================================

Write-Header "Update CI/CD Workflows (Strategy: $Strategy)"

$securityWorkflowPath = ".github/workflows/security-scan.yml"
$semgrepWorkflowPath = ".github/workflows/semgrep.yml"

switch ($Strategy) {
    "semgrep" {
        Write-Info "Configuring Semgrep (free CodeQL alternative)"

        # Check if Semgrep workflow exists
        if (-not (Test-Path $semgrepWorkflowPath)) {
            Write-Error "Semgrep workflow not found: $semgrepWorkflowPath"
            Write-Info "Expected to find pre-created semgrep.yml file"
            exit 1
        }
        Write-Success "Semgrep workflow exists: $semgrepWorkflowPath"

        # Check if .semgrep.yml exists
        if (-not (Test-Path ".semgrep.yml")) {
            Write-Warning "Custom Semgrep rules not found: .semgrep.yml"
            Write-Info "Will use default Semgrep rulesets only"
        } else {
            Write-Success "Custom Semgrep rules found: .semgrep.yml"
        }

        # Disable CodeQL in security-scan.yml
        if (-not $DryRun) {
            Write-Info "Disabling CodeQL job in $securityWorkflowPath"

            $content = Get-Content $securityWorkflowPath -Raw
            $updatedContent = $content -replace '(?ms)(  codeql:.*?(?=  \w+:|$))', '  # CodeQL disabled for private repository (replaced with Semgrep)
  # Uncomment if you enable GitHub Advanced Security
  # $1'

            Set-Content -Path $securityWorkflowPath -Value $updatedContent
            Write-Success "CodeQL job disabled in $securityWorkflowPath"
        } else {
            Write-Info "[DRY RUN] Would disable CodeQL in $securityWorkflowPath"
        }
    }

    "codeql" {
        Write-Info "Keeping CodeQL (requires GitHub Advanced Security)"
        Write-Warning "This will cost $49/month per active committer"
        Write-Info "No workflow changes needed (CodeQL remains enabled)"
    }

    "hybrid" {
        Write-Info "Configuring hybrid CodeQL + Semgrep"
        Write-Warning "CodeQL will run only on main branch (reduce minutes)"
        Write-Info "Semgrep will run on all PRs"

        if (-not $DryRun) {
            # Modify CodeQL to run only on main
            $content = Get-Content $securityWorkflowPath -Raw
            $updatedContent = $content -replace '(  codeql:\s+name:.*?\s+runs-on: ubuntu-latest)', '$1
    # Only run on main branch (save minutes)
    if: github.ref == ''refs/heads/main'''

            Set-Content -Path $securityWorkflowPath -Value $updatedContent
            Write-Success "CodeQL limited to main branch only"
        } else {
            Write-Info "[DRY RUN] Would limit CodeQL to main branch only"
        }
    }
}

# ============================================================================
# Update Documentation
# ============================================================================

Write-Header "Update Documentation"

$filesToUpdate = @(
    @{Path = "README.md"; Old = "public repository"; New = "private repository"},
    @{Path = "README.md"; Old = "Fork this repository"; New = "Request collaborator access"},
    @{Path = "CLAUDE.md"; Old = "(?m)^(## Troubleshooting)"; New = "## Troubleshooting`n`n- **Private repository**: CodeQL replaced with Semgrep (see docs/guide/repository-visibility-migration.md)`n"}
)

foreach ($file in $filesToUpdate) {
    if (Test-Path $file.Path) {
        if (-not $DryRun) {
            $content = Get-Content $file.Path -Raw
            if ($content -match $file.Old) {
                $updatedContent = $content -replace $file.Old, $file.New
                Set-Content -Path $file.Path -Value $updatedContent
                Write-Success "Updated: $($file.Path)"
            } else {
                Write-Info "No changes needed in $($file.Path)"
            }
        } else {
            Write-Info "[DRY RUN] Would update: $($file.Path)"
        }
    } else {
        Write-Warning "File not found (skipping): $($file.Path)"
    }
}

# ============================================================================
# Create Migration Branch and PR
# ============================================================================

Write-Header "Create Migration Branch and PR"

$branchName = "chore/migrate-to-private-repo"

if (-not $DryRun) {
    # Check if branch already exists
    $branchExists = git branch --list $branchName
    if ($branchExists) {
        Write-Warning "Branch '$branchName' already exists"
        Write-Info "Switching to existing branch"
        git checkout $branchName
    } else {
        Write-Info "Creating new branch: $branchName"
        git checkout -b $branchName
    }

    # Stage changes
    git add .github/workflows/*.yml README.md CLAUDE.md .semgrep.yml -ErrorAction SilentlyContinue

    $gitStatus = git status --porcelain
    if ($gitStatus) {
        Write-Info "Staging changes..."
        Write-Host $gitStatus

        # Commit changes
        $commitMessage = @"
chore: migrate repository to private visibility

## Changes
- Security scan strategy: $Strategy
- CodeQL: $(if ($Strategy -eq "semgrep") { "Disabled (replaced with Semgrep)" } elseif ($Strategy -eq "codeql") { "Enabled (requires Advanced Security)" } else { "Hybrid (main branch only)" })
- Documentation updated for private repository

## Migration Guide
See: docs/guide/repository-visibility-migration.md

Related: #issue-number (update with actual issue number)
"@

        git commit -m $commitMessage

        Write-Success "Changes committed to branch: $branchName"

        # Optionally push and create PR (requires gh CLI)
        if (Test-CommandExists "gh") {
            Write-Info "Pushing branch to remote..."
            git push origin $branchName --set-upstream

            Write-Info "Creating pull request..."
            gh pr create `
                --title "Migrate repository to private visibility" `
                --body "## Summary`n`nMigrates repository from public to private visibility.`n`n**Security Strategy**: $Strategy`n`nSee migration guide: docs/guide/repository-visibility-migration.md`n`n## Checklist`n- [ ] Review workflow changes`n- [ ] Test CI/CD workflows`n- [ ] Update collaborator access list`n- [ ] Change repository visibility (after PR merge)`n`n**Cost Impact**: $(if ($Strategy -eq "semgrep") { "$0/month (Semgrep is free)" } elseif ($Strategy -eq "codeql") { "$49/month per committer (Advanced Security)" } else { "$49/month (hybrid)" })" `
                --base main `
                --label "chore" `
                --label "infrastructure"

            Write-Success "Pull request created!"
        } else {
            Write-Info "Push branch manually: git push origin $branchName"
            Write-Info "Create PR via GitHub web UI"
        }
    } else {
        Write-Warning "No changes to commit (files may already be updated)"
    }
} else {
    Write-Info "[DRY RUN] Would create branch: $branchName"
    Write-Info "[DRY RUN] Would commit changes"
    Write-Info "[DRY RUN] Would create pull request"
}

# ============================================================================
# Change Repository Visibility (Optional)
# ============================================================================

if ($ChangeVisibility) {
    Write-Header "Change Repository Visibility"

    Write-Warning "⚠️  IMPORTANT: This will make the repository PRIVATE immediately!"
    Write-Warning "⚠️  Effects:"
    Write-Warning "   - Public users will no longer have access"
    Write-Warning "   - Forks will become detached"
    Write-Warning "   - GitHub Pages will be disabled (if used)"
    Write-Warning "   - CodeQL will require Advanced Security (if enabled)"

    $confirmation = Read-Host "`nType 'MAKE PRIVATE' to confirm (or anything else to cancel)"

    if ($confirmation -eq "MAKE PRIVATE") {
        if (-not $DryRun) {
            Write-Info "Changing repository visibility to private..."

            # Use GitHub CLI to change visibility
            $repo = "DegrassiAaron/meepleai-monorepo"
            gh repo edit $repo --visibility private

            if ($LASTEXITCODE -eq 0) {
                Write-Success "Repository is now PRIVATE: $repo"
            } else {
                Write-Error "Failed to change repository visibility"
                Write-Info "You may need to change it manually via GitHub Settings"
                exit 1
            }
        } else {
            Write-Info "[DRY RUN] Would change repository visibility to private"
        }
    } else {
        Write-Info "Repository visibility change cancelled"
        Write-Info "You can change it manually later via: Settings → Danger Zone → Change visibility"
    }
} else {
    Write-Info "Skipping repository visibility change (use -ChangeVisibility flag to enable)"
}

# ============================================================================
# Summary
# ============================================================================

Write-Header "Migration Summary"

Write-Host "Backup:" -ForegroundColor White
Write-Host "  Location: $backupBundlePath" -ForegroundColor Gray
Write-Host "  Status: $(if ($DryRun) { 'Dry Run' } else { 'Created ✅' })" -ForegroundColor Gray

Write-Host "`nSecurity Strategy:" -ForegroundColor White
Write-Host "  Strategy: $Strategy" -ForegroundColor Gray
Write-Host "  Cost: $(if ($Strategy -eq "semgrep") { "$0/month" } elseif ($Strategy -eq "codeql") { "$49/month per committer" } else { "$49/month" })" -ForegroundColor Gray

Write-Host "`nNext Steps:" -ForegroundColor White
if (-not $DryRun) {
    Write-Host "  1. Review and merge PR: https://github.com/DegrassiAaron/meepleai-monorepo/pulls" -ForegroundColor Gray
    Write-Host "  2. Test CI/CD workflows (create test PR)" -ForegroundColor Gray
    if (-not $ChangeVisibility) {
        Write-Host "  3. Change repository visibility: Settings → Danger Zone → Change visibility" -ForegroundColor Gray
    }
    Write-Host "  4. Add collaborators (if needed): Settings → Collaborators" -ForegroundColor Gray
    Write-Host "  5. Monitor GitHub Actions minutes: Settings → Billing → Usage" -ForegroundColor Gray
} else {
    Write-Host "  Run without -DryRun to perform actual migration" -ForegroundColor Gray
}

Write-Host "`nDocumentation:" -ForegroundColor White
Write-Host "  Full Guide: docs/guide/repository-visibility-migration.md" -ForegroundColor Gray
Write-Host "  Rollback: See Section 7 in migration guide" -ForegroundColor Gray

Write-Success "`nMigration script completed!"
