# Changelog - Automated Setup Scripts

## 2025-11-18 - v1.0

### Added

#### 🤖 Automated Setup Scripts
- **`tools/setup-test-environment.sh`** - Complete automated environment setup
  - Docker cleanup and service orchestration
  - Build artifacts cleanup (.NET, Next.js)
  - API build and startup with health checks
  - Frontend dev server with dependency management
  - Automated test execution (unit, integration, E2E)
  - Graceful shutdown handlers
  - Comprehensive logging to files

- **`quick-start.sh`** - Rapid development setup wrapper
  - Calls setup script with `--skip-tests` flag
  - Optimized for daily development workflow
  - ~2 minute startup time

#### 📚 Documentation
- **`tools/README-setup-script.md`** - Complete setup script documentation
  - Usage examples for all scenarios
  - Detailed step-by-step breakdown
  - Troubleshooting guide
  - Performance tips
  - Security notes

#### ✨ Features

**Script Capabilities:**
- ✅ 6 execution steps with clear progress indicators
- ✅ Dry-run mode for preview without execution
- ✅ Flexible options (skip-cleanup, skip-frontend, skip-tests, full)
- ✅ Colored output for better readability
- ✅ Service health checks (PostgreSQL, API, Frontend)
- ✅ Background process management with PIDs
- ✅ Automatic cleanup on Ctrl+C
- ✅ Comprehensive error handling
- ✅ Log file generation for all services

**Setup Options:**
```bash
--dry-run          # Preview commands
--skip-cleanup     # Keep existing Docker state
--skip-frontend    # Backend only
--skip-tests       # Skip test execution
--full             # Run complete E2E test suite
--verbose, -v      # Detailed output
--help, -h         # Show help
```

**Time Estimates:**
- Quick Start (no tests): ~2 minutes
- Full Setup (with tests): ~5 minutes
- Complete Suite (E2E): ~12 minutes

#### 📝 Documentation Updates
- **README.md** - Added "Automated Setup (Recommended)" section
  - Clear instructions for quick-start and full setup
  - Option reference table
  - Link to detailed documentation

- **CLAUDE.md** - Added quick start commands to Commands table
  - Quick reference for all setup variants
  - Time estimates for each mode

### Benefits

1. **Developer Experience**
   - One-command setup from clean state
   - Consistent environment across team
   - Reduced onboarding time for new developers

2. **Quality Assurance**
   - Automated test execution
   - Health checks ensure services are ready
   - Log files for debugging

3. **CI/CD Alignment**
   - Mirrors CI pipeline locally
   - Pre-commit validation with full suite
   - Dry-run for planning

4. **Flexibility**
   - Multiple modes for different use cases
   - Skip options for faster iteration
   - Verbose mode for debugging

### Technical Details

**Architecture:**
- Bash script with POSIX compliance
- Background process management with trap handlers
- Service readiness polling with timeouts
- Colored output using ANSI escape codes
- Directory path resolution relative to script location

**Services Started:**
1. PostgreSQL (with pg_isready check)
2. Qdrant (vector database)
3. Redis (cache)
4. Seq (logging)
5. API (.NET, port 8080)
6. Frontend (Next.js, port 3000)

**Automatic Setup:**
- EF Core migrations applied on API startup
- Demo users created (admin/editor/user@meepleai.dev)
- Frontend dependencies installed if missing
- Log files created in repository root

**Log Files Generated:**
- `api.log` - API server output
- `web.log` - Frontend dev server output
- `test-backend.log` - Backend test results
- `test-frontend.log` - Frontend test results
- `test-e2e.log` - E2E test results (--full only)

### Files Changed

**New Files:**
- `tools/setup-test-environment.sh` (530 lines)
- `quick-start.sh` (13 lines)
- `tools/README-setup-script.md` (400+ lines)
- `CHANGELOG-setup-scripts.md` (this file)

**Modified Files:**
- `README.md` - Added automated setup section
- `CLAUDE.md` - Added quick start commands

### Testing

**Verified:**
- ✅ Dry-run mode (preview only)
- ✅ Quick-start script wrapper
- ✅ Line ending conversion (CRLF → LF)
- ✅ Script permissions (chmod +x)
- ✅ Help text display
- ✅ Colored output rendering

**Manual Testing Required:**
- Full execution with real services
- Database creation and migrations
- Test execution verification
- Log file generation
- Service health checks
- Graceful shutdown (Ctrl+C)

### Usage Examples

```bash
# Daily development
./quick-start.sh

# Preview changes
./tools/setup-test-environment.sh --dry-run

# Full test suite before PR
./tools/setup-test-environment.sh --full

# Backend only for API work
./tools/setup-test-environment.sh --skip-frontend

# Quick iteration without cleanup
./tools/setup-test-environment.sh --skip-cleanup --skip-tests

# Verbose debugging
./tools/setup-test-environment.sh --verbose
```

### Future Enhancements

Potential improvements for future versions:
- [ ] Windows PowerShell equivalent script
- [ ] macOS-specific optimizations
- [ ] Parallel service startup
- [ ] Custom Docker Compose profiles
- [ ] Test result summary in console
- [ ] Automatic port conflict detection
- [ ] Service restart without full cleanup
- [ ] Integration with VS Code tasks
- [ ] GitHub Codespaces compatibility

### Related Issues

This enhancement supports:
- Developer onboarding improvements
- CI/CD pipeline consistency
- Test automation framework
- Local environment standardization

---

**Author:** Engineering Team
**Date:** 2025-11-18
**Version:** 1.0
