# Tools Directory Organization

The `tools/` directory contains utility scripts organized by purpose. This organization improves discoverability and maintenance.

## Directory Structure

### 📊 **coverage/** - Testing & Coverage Scripts
Scripts for running tests and measuring code coverage:
- `coverage-trends.{sh,ps1}` - Track coverage trends over time
- `run-backend-coverage.sh` - Run backend tests with coverage
- `run-backend-coverage-docker.sh` - Run backend tests in Docker
- `run-frontend-coverage.sh` - Run frontend tests with coverage
- `measure-coverage.ps1` - PowerShell coverage measurement
- `refactor-test-isolation.sh` - Refactoring utility for test isolation

**Usage**: See [README-COVERAGE.md](./README-COVERAGE.md) and [README-FRONTEND-COVERAGE.md](./README-FRONTEND-COVERAGE.md)

---

### 🧹 **cleanup/** - Maintenance & Cleanup Scripts
Scripts for cleaning up caches, processes, and duplicate resources:
- `cleanup-caches.{sh,ps1}` - Clean build caches and temp files
- `cleanup-test-processes.ps1` - Kill hanging test processes
- `cleanup-duplicate-issues.sh` - Remove duplicate GitHub issues

**Usage**: Run monthly with `bash tools/cleanup/cleanup-caches.sh --dry-run`

---

### 🐙 **github/** - GitHub Issue & Label Management
Scripts for creating and managing GitHub issues, labels, and milestones:
- `create-*-issues.{sh,ps1,js}` - Issue creation scripts for various epics
- `lock-closed-issues.{sh,ps1}` - Lock closed issues to prevent spam
- `assign-infrastructure-milestones.sh` - Assign milestones to infrastructure issues
- `phase-2-issue-labels.sh` - Apply phase-specific labels
- `run-issue-triage.sh` - Run issue triage analysis

**Usage**: See [README-admin-console-issues.md](./README-admin-console-issues.md) and [TRIAGE_QUICKREF.md](./TRIAGE_QUICKREF.md)

---

### 📚 **docs/** - Documentation Tools
Scripts for generating and maintaining documentation:
- `generate-api-docs.js` - Generate API documentation from OpenAPI spec
- `search-docs.js` - Search documentation quickly
- `standardize-markdown.js` - Standardize markdown formatting
- `validate-docs.ps1` - Validate documentation structure

**Usage**: `node tools/docs/generate-api-docs.js`

---

### ⚙️ **setup/** - Installation & Setup Scripts
Scripts for setting up the development environment:
- `setup-test-environment.sh` - Main setup script (used by quick-start.sh)
- `setup-github-labels.sh` - Configure GitHub labels
- `setup-ollama.ps1` - Setup Ollama for local LLM
- `setup-n8n-service-account.ps1` - Configure n8n service account
- `.dotnet-install.sh` - Official .NET SDK installer

**Usage**: See [README-setup-script.md](./README-setup-script.md)

---

### 📋 **project/** - Project Management Tools
Scripts for project planning and roadmap management:
- `apply-project-roadmap.py` - Apply roadmap to GitHub Project board
- `generate-project-import.py` - Generate CSV for GitHub Project import

**Usage**: `python tools/project/apply-project-roadmap.py`

---

### 🛠️ **development/** - Developer Utilities
Tools for improving the development experience:
- `open-dual-vscode.{sh,ps1}` - Open VS Code with backend/frontend workspaces
- `analyze-complexity.ps1` - Analyze code complexity metrics

**Usage**: `bash tools/development/open-dual-vscode.sh`

---

### 🔄 **n8n/** - Workflow Automation
Scripts for n8n workflow integration:
- `register-n8n-webhook.ps1` - Register webhooks with n8n

---

### 🗄️ **database/** - Database Management
Scripts for database operations:
- `seed-demo-data.sql` - Seed demo data
- `delete-qdrant-collection.ps1` - Delete Qdrant vector collections

---

### 🧪 **testing/** - Testing Utilities
Scripts for testing integrations and quality:
- `test-ollama-italian.sh` - Test Ollama Italian language support
- `compare-llm-quality.ps1` - Compare LLM output quality

---

### 🗃️ **archive/** - Legacy & Migration Scripts
Historical scripts no longer in active use:
- `migration-scripts/` - One-time migration scripts (FE/BE refactoring)
- `migrate-to-private.ps1` - Repository migration script

See [archive/migration-scripts/README.md](./archive/migration-scripts/README.md)

---

### 🔐 **secrets/** - Secrets Management
Scripts for managing secrets (API keys, credentials):
- `init-secrets.sh` - Initialize secrets structure
- `list-secrets.sh` - List configured secrets
- `rotate-secret.sh` - Rotate a secret

---

### 📊 **sql/** - SQL Utilities
(Existing directory for SQL-related tools)

---

### 🤖 **mcp/** - MCP (Model Context Protocol)
Scripts for MCP integration:
- `mcp_cli.py` - MCP command-line interface

---

## Quick Reference

**Run tests with coverage:**
```bash
bash tools/coverage/run-backend-coverage.sh
bash tools/coverage/run-frontend-coverage.sh
```

**Clean caches:**
```bash
bash tools/cleanup/cleanup-caches.sh --dry-run
```

**Create GitHub issues:**
```bash
bash tools/github/create-frontend-issues.sh
bash tools/github/create-bgai-issues.sh
```

**Setup development environment:**
```bash
bash tools/setup/setup-test-environment.sh
# or use quick-start.sh at project root
```

**Generate documentation:**
```bash
node tools/docs/generate-api-docs.js
```

---

## Contributing

When adding new scripts:
1. Place in the appropriate subdirectory based on purpose
2. Use consistent naming: `{verb}-{noun}.{ext}` (e.g., `run-tests.sh`, `generate-docs.js`)
3. Add execution permissions: `chmod +x script.sh`
4. Document usage in script header or relevant README
5. For PowerShell, provide cross-platform bash alternative when possible

---

**Last Updated:** 2025-11-22
