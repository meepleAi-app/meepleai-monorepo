# Documentation Tools

Scripts for generating, validating, and maintaining project documentation.

## Scripts

### 📚 **generate-api-docs.js**
**Purpose:** Generate API documentation from OpenAPI/Swagger spec
**Usage:** `node tools/docs/generate-api-docs.js`
**Output:** `docs/03-api/generated/` (HTML + Markdown API reference)
**Who:** Backend developers after API changes
**When:** After adding/modifying API endpoints

### 🔍 **search-docs.js**
**Purpose:** Full-text search across all documentation files
**Usage:** `node tools/docs/search-docs.js "authentication"`
**Output:** List of matching files with context snippets
**Who:** Anyone needing to find documentation quickly
**When:** Looking for specific documentation topics

### 📝 **standardize-markdown.js**
**Purpose:** Enforce consistent Markdown formatting across docs/
**What it does:** Fixes heading levels, list formatting, code blocks, links
**Usage:** `node tools/docs/standardize-markdown.js docs/`
**Who:** Documentation maintainers
**When:** Before major documentation releases

### ✅ **validate-docs.ps1**
**Purpose:** Validate documentation structure and links
**What it does:** Checks for broken links, missing files, correct structure
**Usage:** `.\tools\docs\validate-docs.ps1`
**Who:** CI/CD pipeline, documentation reviewers
**When:** In PR checks, before releases

**Requirements:** Node.js 20+, PowerShell 5.1+ (for validate-docs)

---

**Last Updated:** 2025-11-22
