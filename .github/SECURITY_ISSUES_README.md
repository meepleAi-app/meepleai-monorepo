# Security Issues - Creation Guide

This directory contains 3 security issue templates ready to be created on GitHub.

## 📋 Issues to Create

| Priority | Issue | Estimate | File |
|----------|-------|----------|------|
| **P1 (High)** | XSS Vulnerability in Rich Text Editor | 6-7h | `ISSUE_SECURITY_01_XSS.md` |
| **P2 (Medium)** | Hardcoded Database Credentials | 3h | `ISSUE_SECURITY_02_HARDCODED_CREDENTIALS.md` |
| **P3 (Low)** | Security Improvements (CORS, JSON, Logging) | 7-9h | `ISSUE_SECURITY_03_IMPROVEMENTS.md` |

**Total Remediation Time:** 16-19 hours

---

## 🚀 Quick Start - Create Issues

### Option 1: Automated Script (Bash + GitHub CLI)

```bash
# Prerequisites: Install GitHub CLI
# macOS:   brew install gh
# Linux:   sudo apt install gh
# Windows: winget install GitHub.cli

# Authenticate
gh auth login

# Run the script
./tools/create-security-issues.sh
```

### Option 2: Automated Script (Python + REST API)

```bash
# Prerequisites: Python 3.x (already installed)

# Set your GitHub token
export GITHUB_TOKEN='ghp_your_token_here'

# Or set GH_TOKEN
export GH_TOKEN='ghp_your_token_here'

# Run the script
python3 tools/create-security-issues.py

# Or without token in env (will prompt):
python3 tools/create-security-issues.py
```

**Create a GitHub token:**
1. Go to https://github.com/settings/tokens
2. Click "Generate new token" → "Generate new token (classic)"
3. Name it: "Security Issues Script"
4. Select scopes: ✅ **repo** (Full control of private repositories)
5. Click "Generate token"
6. Copy the token (starts with `ghp_`)

### Option 3: Manual Creation (Web UI)

Click these links to create issues with pre-filled templates:

#### Issue 1: XSS Vulnerability (P1 - High)
```
https://github.com/DegrassiAaron/meepleai-monorepo/issues/new?title=%5BSECURITY%5D%20XSS%20Vulnerability%20in%20Rich%20Text%20Editor&labels=security,xss,p1-high,frontend,editor
```

**Steps:**
1. Click the link above
2. Copy the content from `.github/ISSUE_SECURITY_01_XSS.md`
3. Paste into the issue body
4. Click "Submit new issue"

#### Issue 2: Hardcoded Credentials (P2 - Medium)
```
https://github.com/DegrassiAaron/meepleai-monorepo/issues/new?title=%5BSECURITY%5D%20Hardcoded%20Database%20Credentials&labels=security,credentials,p2-medium,backend,configuration
```

**Steps:**
1. Click the link above
2. Copy the content from `.github/ISSUE_SECURITY_02_HARDCODED_CREDENTIALS.md`
3. Paste into the issue body
4. Click "Submit new issue"

#### Issue 3: Security Improvements (P3 - Low)
```
https://github.com/DegrassiAaron/meepleai-monorepo/issues/new?title=%5BSECURITY%5D%20Security%20Improvements%3A%20CORS%20%26%20JSON%20Deserialization&labels=security,hardening,p3-low,backend
```

**Steps:**
1. Click the link above
2. Copy the content from `.github/ISSUE_SECURITY_03_IMPROVEMENTS.md`
3. Paste into the issue body
4. Click "Submit new issue"

---

## 📊 Issue Details

### Issue 1: XSS Vulnerability (P1)
- **Severity:** WARNING
- **CWE:** CWE-79 (Cross-site Scripting)
- **File:** `apps/web/src/pages/editor.tsx:530`
- **Risk:** Stored XSS via dangerouslySetInnerHTML
- **Fix:** Add DOMPurify sanitization
- **Estimate:** 6-7 hours
- **Labels:** `security`, `xss`, `p1-high`, `frontend`, `editor`

### Issue 2: Hardcoded Credentials (P2)
- **Severity:** WARNING
- **CWE:** CWE-798 (Hardcoded Credentials)
- **Files:**
  - `ObservabilityServiceExtensions.cs:91`
  - `MeepleAiDbContextFactory.cs:15`
- **Risk:** Fallback to postgres:postgres credentials
- **Fix:** Fail-fast with exception
- **Estimate:** 3 hours
- **Labels:** `security`, `credentials`, `p2-medium`, `backend`, `configuration`

### Issue 3: Security Improvements (P3)
- **Severity:** INFO
- **Categories:**
  - CORS: AllowAnyHeader() restriction
  - JSON: Deserialization validation (38 locations)
  - Logging: Token hash length reduction
- **Risk:** Low (hardening opportunities)
- **Fix:** Multiple improvements
- **Estimate:** 7-9 hours (4-6h core + optional)
- **Labels:** `security`, `hardening`, `p3-low`, `backend`

---

## 🔍 Verification

After creating the issues, verify with:

```bash
# Using GitHub CLI
gh issue list --repo DegrassiAaron/meepleai-monorepo --label security

# Or visit:
https://github.com/DegrassiAaron/meepleai-monorepo/issues?q=is%3Aissue+is%3Aopen+label%3Asecurity
```

Expected output: 3 open security issues

---

## 📖 Related Documentation

### Security Reports (Updated 2025-11-15)
- **📊 Executive Audit Summary:** `docs/06-security/security-issue-audit.md` - **START HERE**
- **📋 Comprehensive Analysis (954 Issues):** `docs/06-security/SECURITY_ANALYSIS_954_ISSUES.md`
- **🔍 Original Security Audit:** `docs/06-security/SECURITY_AUDIT_2025-11-04.md`

### Remediation Documentation
- **Path Injection Fixes:** `docs/06-security/code-scanning-remediation-summary.md`
- **IDisposable Leaks:** `docs/06-security/disposable-resource-leak-remediation.md`
- **Null References:** `docs/06-security/null-reference-remediation.md`
- **Exception Handling:** `docs/06-security/generic-catch-analysis.md`

### Security Policies & Configuration
- **Security Policy:** `SECURITY.md` (root) and `docs/SECURITY.md`
- **Semgrep Rules:** `.semgrep.yml`
- **Security Patterns:** `docs/06-security/security-patterns.md`
- **OAuth Security:** `docs/06-security/oauth-security.md`

---

## 🎯 Next Steps After Creating Issues

1. **Review Issues:** Ensure all details are correct
2. **Assign Priority Labels:** Already included in scripts
3. **Assign to Team Members:** Add assignees
4. **Add to Milestone:** Link to current sprint/milestone
5. **Start Remediation:**
   - Week 1: Fix P1 (XSS, 6-7h)
   - Week 2: Fix P2 (Credentials, 3h)
   - Week 3-4: Fix P3 (Improvements, 7-9h)

---

## 🛠️ Troubleshooting

### "gh: command not found"
Install GitHub CLI:
- macOS: `brew install gh`
- Ubuntu/Debian: `sudo apt install gh`
- Fedora/CentOS: `sudo dnf install gh`
- Windows: `winget install GitHub.cli`

### "gh: authentication required"
Authenticate with:
```bash
gh auth login
```
Follow the prompts to authenticate via browser.

### "Python script: 401 Unauthorized"
Your GitHub token is invalid or expired. Create a new token at:
https://github.com/settings/tokens

### "Python script: 403 Forbidden"
Your token doesn't have the `repo` scope. Create a new token with:
- ✅ repo (Full control of private repositories)

### "Python script: File not found"
Make sure you're running the script from the repository root:
```bash
cd /path/to/meepleai-monorepo
python3 tools/create-security-issues.py
```

---

## 📞 Support

For questions or issues:
- Tag @DegrassiAaron in issue comments
- Create a discussion in GitHub Discussions
- Review security documentation in `docs/security/`

---

**Created:** 2025-11-04
**Last Updated:** 2025-11-15
**Security Audit:** `docs/06-security/security-issue-audit.md` (Executive Summary)
**Comprehensive Analysis:** `docs/06-security/SECURITY_ANALYSIS_954_ISSUES.md` (954 Issues Analyzed)
**Issues Ready:** 3 (P1, P2, P3)
**Overall Security Rating:** 9.5/10 (EXCELLENT)
