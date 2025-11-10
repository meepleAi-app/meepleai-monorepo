# Issue #817: Fix Cleanup Script Exit Behavior

## Problem

The `cleanup-caches.sh` script uses `set -e` to exit on errors, but the `clean_directory` helper function returns a non-zero status (1) when a directory doesn't exist. Since the main routine unconditionally calls `clean_directory` for multiple cache directories (`.serena`, `codeql-db`, `.playwright-mcp`, etc.), any missing directory causes the script to terminate prematurely, preventing cleanup of remaining directories and the summary display.

**Impact**: Most developers have only a subset of these caches locally, so typical runs exit midway without completing the cleanup or showing the summary.

## Root Cause

```bash
clean_directory() {
    # ... existing directory logic ...
    else
        if [ "$VERBOSE" = true ]; then
            echo -e "${GREEN}Not found: $dir (skipping)${NC}"
        fi
        return 1  # ❌ Causes set -e to exit the script
    fi
}
```

With `set -e` enabled, any command returning non-zero causes immediate script termination.

## Solution

Changed `clean_directory` to return `0` when skipping missing directories:

```bash
clean_directory() {
    # ... existing directory logic ...
    else
        if [ "$VERBOSE" = true ]; then
            echo -e "${GREEN}Not found: $dir (skipping)${NC}"
        fi
        return 0  # ✅ Allows script to continue processing
    fi
}
```

## Validation

Created test script (`tools/test-cleanup-fix.sh`) that validates:
1. Mixed existing and missing directories
2. All directories missing
3. All directories existing

**Test Results**: ✅ All scenarios pass, script completes without early exit.

```bash
$ bash tools/test-cleanup-fix.sh
Testing cleanup script behavior with missing directories...

Test 1: Mixed existing and missing directories
----------------------------------------------
○ Skipping (not found): /tmp/.../missing1
✓ Found and would clean: /tmp/.../exists1
○ Skipping (not found): /tmp/.../missing2
✓ Found and would clean: /tmp/.../exists2
○ Skipping (not found): /tmp/.../missing3

Test 2: All directories missing
--------------------------------
○ Skipping (not found): /tmp/.../missing4
○ Skipping (not found): /tmp/.../missing5
○ Skipping (not found): /tmp/.../missing6

Test 3: All directories exist
------------------------------
✓ Found and would clean: /tmp/.../exists1
✓ Found and would clean: /tmp/.../exists2

✅ SUCCESS: All tests passed!
```

## Changes Made

1. **cleanup-caches.sh**: Fixed `return 0` for missing directories (line 112)
2. **tools/README.md**: Added technical notes explaining the behavior
3. **tools/test-cleanup-fix.sh**: Created validation test script

## PowerShell Version

The PowerShell version (`cleanup-caches.ps1`) doesn't have this issue because:
- PowerShell doesn't use `set -e` equivalent by default
- The function returns `$true/$false` which don't cause termination
- No changes needed

## Developer Experience

**Before**: Script exits at first missing directory, no summary
**After**: Script processes all directories, shows complete summary

```bash
# Typical developer machine (only some caches present)
$ bash tools/cleanup-caches.sh --dry-run

╔════════════════════════════════════════════╗
║   MeepleAI Cache Cleanup                   ║
╚════════════════════════════════════════════╝

📊 Calculating current sizes...
📁 Target directories:
  - codeql-db
  - apps/web/.next

🧹 Cleaning cache directories...
[DRY RUN] Would delete: codeql-db (62.8 MB)
[DRY RUN] Would delete: apps/web/.next (45.2 MB)

╔════════════════════════════════════════════╗
║   Cleanup Summary                          ║
╚════════════════════════════════════════════╝

✓ Dry run completed. No files were deleted.
  Estimated space that would be freed: 108 MB
```

## Related

- **Issue**: #817
- **Files Modified**:
  - `tools/cleanup-caches.sh` (1 line)
  - `tools/README.md` (documentation)
  - `tools/test-cleanup-fix.sh` (new test)
- **Testing**: Manual validation + automated test script
