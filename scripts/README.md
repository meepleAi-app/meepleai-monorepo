# Scripts Directory

PowerShell scripts for local development workflows. This directory contains Windows-focused automation scripts for common development tasks.

## Directory Structure

### 🚀 **dev/** - Development Workflow
Scripts for starting/stopping development services:
- **`dev-up.ps1`** - Start development environment (Docker Compose)
- **`dev-down.ps1`** - Stop development environment
- **`dev-logs.ps1`** - View development logs

**Usage:**
```powershell
# Start all services
.\scripts\dev\dev-up.ps1

# View logs
.\scripts\dev\dev-logs.ps1

# Stop all services
.\scripts\dev\dev-down.ps1
```

---

### 🐙 **github/** - GitHub Workflow Utilities
Scripts for GitHub-related workflows:
- **`create-label.ps1`** - Create GitHub labels
- **`create-new-feature.ps1`** - Scaffold new feature branch

**Usage:**
```powershell
# Create a new feature branch
.\scripts\github\create-new-feature.ps1 -FeatureName "my-feature"

# Create GitHub labels
.\scripts\github\create-label.ps1 -Label "bug" -Color "d73a4a"
```

---

### 🚀 **deployment/** - Production Deployment
Scripts for deploying MeepleAI to production:
- **`deploy-meepleai.ps1`** - Production deployment automation

**Usage:**
```powershell
# Start all production services
.\scripts\deployment\deploy-meepleai.ps1 up

# Show service status
.\scripts\deployment\deploy-meepleai.ps1 status

# View logs
.\scripts\deployment\deploy-meepleai.ps1 logs

# Backup databases
.\scripts\deployment\deploy-meepleai.ps1 backup

# Update to latest images
.\scripts\deployment\deploy-meepleai.ps1 update

# Stop all services
.\scripts\deployment\deploy-meepleai.ps1 down
```

---

### 📊 **monitoring/** - Resource Monitoring
Scripts for monitoring Docker container resources:
- **`docker-resource-monitor.ps1`** - Container resource usage monitoring

**Usage:**
```powershell
# Watch resources in real-time (2-second intervals)
.\scripts\monitoring\docker-resource-monitor.ps1 -Watch

# Watch with custom interval
.\scripts\monitoring\docker-resource-monitor.ps1 -Watch -WatchInterval 5

# Capture baseline snapshot
.\scripts\monitoring\docker-resource-monitor.ps1 -Baseline

# Export to CSV
.\scripts\monitoring\docker-resource-monitor.ps1 -Export stats.csv

# Analyze saved baseline
.\scripts\monitoring\docker-resource-monitor.ps1 -Analyze resource-baseline.txt

# Custom alert threshold
.\scripts\monitoring\docker-resource-monitor.ps1 -Watch -Threshold 90
```

---

### ✅ **quality/** - Code Quality Checks
Scripts for validating code quality and documentation:
- **`validate-doc-links.ps1`** - Validate markdown links in documentation
- **`validate-csharp-namespaces.ps1`** - Validate C# namespace consistency

**Usage:**
```powershell
# Validate all documentation links
.\scripts\quality\validate-doc-links.ps1

# Validate C# namespaces
.\scripts\quality\validate-csharp-namespaces.ps1
```

---

### 🧪 **testing/** - Integration Testing
Scripts for testing services and runbooks:
- **`test-services.ps1`** - Health check all MeepleAI services
- **`test-oauth-health.ps1`** - Test OAuth configuration
- **`test-runbooks.ps1`** - Test operational runbooks

**Usage:**
```powershell
# Test all services health
.\scripts\testing\test-services.ps1

# Test OAuth configuration
.\scripts\testing\test-oauth-health.ps1

# Test OAuth with verbose output
.\scripts\testing\test-oauth-health.ps1 -Verbose

# Test high-error-rate runbook
.\scripts\testing\test-runbooks.ps1 high-error-rate

# Test error-spike runbook
.\scripts\testing\test-runbooks.ps1 error-spike

# Test all runbooks
.\scripts\testing\test-runbooks.ps1 all
```

---

### 🛠️ **utils/** - Shared Utilities
Common functions and prerequisites checking:
- **`common.ps1`** - Shared functions for all scripts
- **`check-prerequisites.ps1`** - Verify required tools are installed

**Usage:**
```powershell
# Check if all required tools are installed
.\scripts\utils\check-prerequisites.ps1

# Source common functions in other scripts
. .\scripts\utils\common.ps1
```

---

### 🧹 **Cleanup Scripts** - Maintenance Utilities
Scripts for cleaning up temporary files and maintaining repository hygiene:

#### **cleanup-tmpclaude**
Removes temporary `tmpclaude-*` files created by Claude Code during operations.

**Problem**: Claude Code occasionally creates temporary files during directory verification, file context operations, and session management that may not be automatically cleaned up.

**Solution**: Run cleanup scripts periodically to remove orphaned files.

**Usage:**
```powershell
# Windows PowerShell
.\scripts\cleanup-tmpclaude.ps1

# Linux/macOS Bash
./scripts/cleanup-tmpclaude.sh
```

**Why These Files Are Created**:
- Directory verification operations
- File context operations
- Tool execution requiring temporary state
- Session management operations

**Safety**: These files are safe to delete and do not contain important data.

**Automatic Prevention**: The `.gitignore` file includes `tmpclaude*` pattern to prevent accidental commits.

**Maintenance Recommendations**:
1. **Weekly**: Run cleanup script to remove orphaned files
2. **Before Commits**: Verify no tmpclaude files are staged (`git status`)
3. **CI/CD**: Pre-commit hooks verify no tmpclaude files are committed

---

## Platform Notes

- **Windows:** All scripts are PowerShell-based and work natively
- **Linux/macOS:** Use the equivalent bash scripts in `tools/` directory
  - For dev workflow: Use Docker Compose directly or `docker/docker-compose.yml`
  - For setup: Use `tools/setup/setup-test-environment.sh`

## Cross-Platform Alternative

For a cross-platform quick start, use the bash script at project root:
```bash
bash quick-start.sh
```

This internally calls `tools/setup/setup-test-environment.sh`.

---

## Contributing

When adding new PowerShell scripts:
1. Place in appropriate subdirectory based on purpose
2. Add script documentation header with description and parameters
3. Import common functions: `. $PSScriptRoot\..\utils\common.ps1`
4. Follow PowerShell naming conventions: `Verb-Noun.ps1`
5. Provide bash alternative in `tools/` when possible for cross-platform support

---

**Last Updated:** 2026-01-24
