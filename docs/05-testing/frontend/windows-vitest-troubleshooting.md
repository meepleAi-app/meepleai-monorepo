# Windows Vitest Troubleshooting Guide

> **Issue Discovered**: 2026-01-23 (During EPIC #2759 validation)
> **Platform**: Windows 10/11
> **Impact**: Critical - Blocks local test execution

## Problem: Tests Hang with No Output

### Symptoms
- Running `pnpm test` or `npx vitest` produces no output
- Process appears to hang indefinitely
- No error messages displayed
- Ctrl+C required to exit

### Root Cause
**Windows stdout/stderr buffering issue** when running vitest through `npx` or `pnpm` on Windows systems.

The problem occurs because:
1. `npx` and `pnpm` add wrapper layers around the actual node process
2. Windows handles stdout/stderr buffering differently than Unix systems
3. Vitest's output gets stuck in the wrapper's buffer and never reaches the terminal

### Solution: Run Node Directly

Instead of using package manager wrappers, execute vitest directly with `node`:

```bash
# ❌ BROKEN (hangs on Windows)
pnpm test
pnpm test:coverage
npx vitest run

# ✅ WORKS (direct node execution)
node node_modules/vitest/vitest.mjs run
node node_modules/vitest/vitest.mjs run --coverage
node node_modules/vitest/vitest.mjs run --coverage --reporter=verbose
```

### Quick Reference Commands

```bash
# Run all tests
cd apps/web
node node_modules/vitest/vitest.mjs run

# Run with coverage
node node_modules/vitest/vitest.mjs run --coverage

# Run with verbose output
node node_modules/vitest/vitest.mjs run --reporter=verbose

# Run specific test file
node node_modules/vitest/vitest.mjs run src/components/__tests__/Button.test.tsx

# Run in watch mode
node node_modules/vitest/vitest.mjs

# Run with UI
node node_modules/vitest/vitest.mjs --ui
```

### Capture Output to File

When running coverage tests, capture output for analysis:

```bash
# Run and save to log file (shows first 100 lines)
node node_modules/vitest/vitest.mjs run --coverage --reporter=verbose 2>&1 | tee coverage-run.log | head -100

# View full log after completion
cat coverage-run.log

# Check coverage summary
cat coverage/coverage-summary.json
```

## Alternative: WSL or Git Bash

If you frequently encounter Windows-specific issues, consider using:

### WSL (Recommended for Windows 10/11)
```bash
# Install WSL if not already installed
wsl --install

# Run tests in WSL
wsl
cd /mnt/d/Repositories/meepleai-monorepo-dev/apps/web
pnpm test  # Works normally in WSL
```

### Git Bash
Git Bash often handles stdout/stderr better than cmd.exe or PowerShell:
```bash
# Open Git Bash terminal
pnpm test  # May work in Git Bash
```

## CI/CD Impact

**Good news**: This issue is Windows-specific and does NOT affect CI/CD pipelines.

- ✅ GitHub Actions (Linux) works normally
- ✅ CI coverage reports are accurate
- ✅ codecov uploads succeed

The issue only impacts local Windows development environments.

## Verification

To verify vitest is working:

```bash
# 1. Check vitest version (should output version number)
node node_modules/vitest/vitest.mjs --version
# Expected: vitest/3.2.4 win32-x64 node-v22.20.0

# 2. Run a simple test
node node_modules/vitest/vitest.mjs run src/components/__tests__/Button.test.tsx

# 3. Check coverage generation
node node_modules/vitest/vitest.mjs run --coverage
ls coverage/  # Should contain lcov.info, coverage-summary.json, etc.
```

## Related Issues

- **Issue #2759**: Frontend Test Coverage EPIC - This issue was discovered during coverage validation
- **Environment**: Node v22.20.0, pnpm 10.x, Vitest 3.2.4

## Prevention

### For Future Projects
1. **Document platform differences**: Note Windows-specific behaviors in README
2. **Test on multiple platforms**: Validate scripts work on Windows/macOS/Linux
3. **Use direct execution**: Consider npm scripts that use `node` directly for cross-platform compatibility

### Package.json Script Updates (Optional)

Consider adding Windows-friendly aliases:

```json
{
  "scripts": {
    "test": "vitest run",
    "test:win": "node node_modules/vitest/vitest.mjs run",
    "test:coverage": "vitest run --coverage",
    "test:coverage:win": "node node_modules/vitest/vitest.mjs run --coverage"
  }
}
```

## Lessons Learned

1. **Platform Testing**: Always test CLI tools on Windows, macOS, and Linux
2. **Direct Execution**: For critical tools, provide direct `node` execution paths as fallback
3. **Early Detection**: Run local tests before pushing to ensure dev environment parity
4. **Documentation**: Document platform-specific issues immediately when discovered

## References

- [Vitest GitHub Issues - Windows stdout buffering](https://github.com/vitest-dev/vitest/issues)
- [Node.js Windows stdout/stderr behavior](https://nodejs.org/api/process.html#processstdout)
- [pnpm Windows compatibility](https://pnpm.io/installation#compatibility)

---

**Last Updated**: 2026-01-23
**Reporter**: PM Agent (Issue #2759)
**Status**: ✅ Workaround documented
