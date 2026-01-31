# Setup & Installation Scripts

Scripts for setting up development environment, installing dependencies, and configuring tools.

## Scripts

### ⚙️ **setup-test-environment.sh**
**Purpose:** Comprehensive development environment setup
**What it does:**
1. Checks prerequisites (Docker, .NET, Node.js, pnpm)
2. Installs dependencies (pnpm install, dotnet restore)
3. Creates .env files from templates
4. Starts Docker Compose services
5. Runs database migrations
6. Optionally runs tests

**Usage:**
```bash
# Full setup
bash tools/setup/setup-test-environment.sh

# Skip tests (faster startup)
bash tools/setup/setup-test-environment.sh --skip-tests

# Help
bash tools/setup/setup-test-environment.sh --help
```

**Who:** New developers, CI/CD pipelines
**When:** First-time setup, after git clone
**Also used by:** `quick-start.sh` in project root

### 🏷️ **setup-github-labels.sh**
**Purpose:** Configure GitHub labels for issue categorization
**Labels created:** Priorities, types, epics, areas, status labels
**Usage:** `bash tools/setup/setup-github-labels.sh`
**Who:** Repository admin (run once)
**When:** Repository initialization

### 🤖 **setup-ollama.ps1**
**Purpose:** Install and configure Ollama for local LLM
**Usage:** `.\tools\setup\setup-ollama.ps1`
**Who:** Developers wanting local AI models
**When:** Optional - for offline AI development

### 🔄 **setup-n8n-service-account.ps1**
**Purpose:** Create service account for n8n workflow automation
**Usage:** `.\tools\setup\setup-n8n-service-account.ps1`
**Who:** DevOps team
**When:** n8n integration setup

### 📦 **.dotnet-install.sh**
**Purpose:** Official Microsoft .NET SDK installer script
**Usage:** `bash tools/setup/.dotnet-install.sh --version 9.0`
**Who:** CI/CD environments without .NET pre-installed
**When:** Automated in CI pipelines
**Source:** https://dot.net/v1/dotnet-install.sh

**Requirements:** Varies by script (see individual help)

---

**Last Updated:** 2025-11-22
**See also:** `README-setup-script.md` in tools/
