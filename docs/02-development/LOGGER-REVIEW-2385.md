# Logger Configuration Review - Issue #2385

**Date**: 2026-01-14
**Status**: Review Complete
**Result**: No code changes required

## Summary

Review of `apps/web/src/lib/logger.ts` modification triggered by 3-line change in commit `d4da34c4`.

## Findings

### 1. Recent Change Analysis

**Commit**: `d4da34c4` - "Fix remote log default to same-origin"

**Change**:
```diff
- enableRemote: process.env.NEXT_PUBLIC_ENABLE_REMOTE_LOGS !== 'false',
- remoteEndpoint: process.env.NEXT_PUBLIC_LOG_ENDPOINT || 'http://localhost/api/v1/logs',
+ // Disable custom remote logging - using HyperDX instead
+ enableRemote: process.env.NEXT_PUBLIC_ENABLE_REMOTE_LOGS === 'true',
+ // Prefer explicit env; otherwise fall back to same-origin relative path
+ remoteEndpoint: process.env.NEXT_PUBLIC_LOG_ENDPOINT || '/api/v1/logs',
```

**Impact**:
- Remote logging is now **disabled by default** (opt-in instead of opt-out)
- Endpoint changed from absolute URL to same-origin relative path
- Both changes are intentional and correct

### 2. Logger Architecture

Two separate loggers exist in the frontend:

| Logger | Location | Purpose | Remote |
|--------|----------|---------|--------|
| Main Logger | `lib/logger.ts` | General app logging with batching | Optional |
| API Logger | `lib/api/core/logger.ts` | API-specific errors | Console only |

**Assessment**: This separation is intentional and follows best practices:
- Main logger handles general app events with optional remote batching
- API logger is specialized for HTTP client error context
- Both integrate with HyperDX via browser SDK (primary observability)

### 3. Configuration Status

| Variable | Default | Purpose |
|----------|---------|---------|
| `NEXT_PUBLIC_ENABLE_REMOTE_LOGS` | `false` (implicit) | Enable custom remote log endpoint |
| `NEXT_PUBLIC_LOG_ENDPOINT` | `/api/v1/logs` | Custom log endpoint URL |

**Current State**: Correct - HyperDX is the primary observability solution, custom endpoint is available for specific debugging scenarios.

### 4. Usage Audit

- **63 files** import from logger modules
- **34 files** contain direct `console.*` calls (acceptable for debugging, tests, and workers)
- All production logging goes through structured loggers

### 5. Recommendations

**No immediate action required.** Current configuration follows best practices:

1. **HyperDX Integration**: Primary observability via `lib/hyperdx.ts`
2. **Structured Logging**: Both loggers produce consistent log entries
3. **Correlation IDs**: Main logger supports distributed tracing
4. **Graceful Degradation**: Console fallback when remote unavailable

### Future Improvements (Low Priority)

If consolidation is desired in the future:
- Consider unifying logger interfaces for consistency
- Add JSDoc to clarify when to use which logger
- Document HyperDX browser SDK configuration

## Acceptance Criteria Status

- [x] Review logger.ts modification
- [x] Verify logging configuration follows best practices
- [x] Document findings (this document)
- [x] No fixes needed - configuration is correct
- [ ] Frontend builds successfully (pending validation)

## Related

- Commit: `d4da34c4` - Original change
- Issue #1564 - HyperDX migration
- `lib/hyperdx.ts` - HyperDX browser integration
