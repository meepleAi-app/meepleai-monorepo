# Codebase Analysis: React 19 & Next.js 16 Migration

**Date**: 2025-11-10
**Branch**: feat/823-react-19-nextjs-16-migration
**Analysis Tool**: Serena MCP + Grep

## Summary

This document analyzes the MeepleAI frontend codebase to identify all React and Next.js usage patterns that may be affected by the migration to React 19 and Next.js 16.

## Scope

- **Total React Import Files**: 100+ files
- **Total Next.js Import Files**: 50+ files
- **React Hooks Usage**: 677 occurrences across 82 files
- **Next.js API Usage**: 22 files (cookies, headers, params, searchParams)
- **react-chessboard Usage**: 6 files (primary: chess.tsx, tests)

## Critical Files for Migration

### 1. React Hooks Analysis (677 occurrences)

**Most Used Hooks**:
- `useState`: Used in ~82 files
- `useEffect`: Used in ~82 files
- `useMemo`: Lower usage
- `useCallback`: Lower usage
- `useRef`: Moderate usage
- `useContext`: Lower usage

**Files with Heavy Hook Usage**:
1. `apps/web/src/pages/chat.tsx` - Chat interface with streaming
2. `apps/web/src/pages/editor.tsx` - Rich text editor (TipTap)
3. `apps/web/src/pages/chess.tsx` - Chess board and AI chat
4. `apps/web/src/lib/hooks/useMultiGameChat.ts` - Multi-game chat logic
5. `apps/web/src/lib/hooks/useChatStreaming.ts` - SSE streaming
6. `apps/web/src/components/chat/ChatProvider.tsx` - Chat context

**React 19 Impact**:
- ✅ Most hooks remain unchanged
- ⚠️ `useEffect` cleanup timing may change slightly
- ⚠️ `useLayoutEffect` SSR behavior changed

### 2. Next.js Async APIs (22 files)

**Files Using `cookies()`, `headers()`, `params`, `searchParams`**:
- `apps/web/src/pages/admin/n8n-templates.tsx`
- `apps/web/src/pages/admin/prompts/index.tsx`
- `apps/web/src/pages/admin/prompts/[id]/audit.tsx`
- `apps/web/src/pages/admin/prompts/[id]/compare.tsx`
- `apps/web/src/lib/api.ts` (API client)

**Next.js 16 Impact**:
- 🔴 **CRITICAL**: All these APIs now return Promises
- 🔴 Must add `await` to all `cookies()`, `headers()`, `params`, `searchParams` calls
- 🔴 Files using these APIs must be converted to `async` functions

### 3. react-chessboard Component (6 files)

**Primary Implementation**:
- `apps/web/src/pages/chess.tsx` - Main chess page

**Test Files**:
- `apps/web/src/__tests__/pages/chess.test.tsx`
- `apps/web/src/__tests__/pages/chess.accessibility.test.tsx`

**Breaking Changes Required**:
1. `onPieceDragStart` → `onPieceDrag` (handler rename)
2. `onPieceDragEnd` removed (delete handler)
3. `onPieceDrop` must return boolean (currently returns boolean ✅)
4. `arrowSettings` → `arrowOptions` (if used)
5. Cell → Square terminology (documentation only)

**Current chess.tsx Props**:
```typescript
<Chessboard
  position={currentPosition}
  onPieceDrop={onDrop}  // ✅ Already returns boolean
  boardOrientation={boardOrientation}
  customSquareStyles={highlightedSquares}
  boardWidth={550}
/>
```

**Migration Needed**:
- ✅ `onPieceDrop` already returns boolean - no change needed
- ⚠️ No `onPieceDragStart` or `onPieceDragEnd` used - safe
- ⚠️ No `arrowSettings` used - safe
- ✅ Minimal changes required!

### 4. Component Patterns

**Pages Using React Components** (100+ files):
- Admin pages: analytics, users, cache, configuration, prompts, bulk-export
- Chat pages: chat, setup
- Editor page: editor
- Auth pages: login, reset-password, settings, profile, auth/callback
- Other: versions, logs, n8n, chess, upload, index

**Component Types**:
1. **Functional Components**: 100% (all are functional)
2. **Class Components**: 0 (none found ✅)
3. **Server Components**: Limited (mostly client components)
4. **Client Components**: Majority

### 5. Next.js-Specific Imports

**Most Common Next.js Imports**:
- `next/link`: Used extensively (Link component)
- `next/router`: Used for routing
- `next/image`: Used for optimized images
- `next/head`: Used for meta tags
- `next/document`: `_document.tsx` (custom document)
- `next/app`: `_app.tsx` (custom app)

**Next.js 16 Impact**:
- ✅ `next/link`: No breaking changes
- ⚠️ `next/image`: Minor API changes (cache TTL: 60s → 4hrs)
- ✅ `next/router`: No breaking changes for Pages Router
- ⚠️ `_document.tsx` / `_app.tsx`: Verify compatibility

### 6. TypeScript Types

**Current @types Versions**:
- `@types/react`: 18.2.66
- `@types/react-dom`: 18.2.22

**Target @types Versions**:
- `@types/react`: 19.2.2
- `@types/react-dom`: 19.2.2

**Impact**:
- 🔴 Major type changes expected
- 🔴 `FC` type definition changes
- 🔴 Event handler types updated
- 🔴 JSX namespace changes

## Migration Priority

### Phase 1: Dependencies (Low Risk)
1. Update `package.json` with new versions
2. Run `pnpm install`
3. Resolve any peer dependency conflicts

### Phase 2: TypeScript Fixes (Medium Risk)
1. Fix type errors from `@types/react` 19.x
2. Update component type definitions
3. Fix event handler types
4. Update test type assertions

### Phase 3: Next.js 16 Async APIs (High Risk)
1. Convert files using `cookies()`, `headers()`, `params`, `searchParams` to async
2. Add `await` to all async API calls
3. Update error handling for Promise rejections

### Phase 4: react-chessboard (Low Risk)
1. Update version in `package.json`
2. No code changes needed (already compatible!)
3. Update tests if needed
4. Verify functionality manually

### Phase 5: React 19 Component Updates (Low Risk)
1. No class components to convert ✅
2. Review `useEffect` cleanup behavior
3. Test streaming QA (SSE)
4. Test rich text editor (TipTap)

### Phase 6: Testing (Critical)
1. Run full Jest suite (4000+ tests)
2. Run Playwright E2E (30 test files)
3. Manual testing of critical user journeys
4. Performance testing (bundle size, load time)

## Risk Assessment

| Area | Risk Level | Impact | Effort |
|------|-----------|--------|--------|
| Dependencies | Low | Low | 15 min |
| TypeScript | Medium | Medium | 2-4 hours |
| Next.js Async APIs | High | High | 3-5 hours |
| react-chessboard | Low | Low | 30 min |
| React Components | Low | Low | 1-2 hours |
| Testing | Medium | High | 4-6 hours |

**Total Estimated Effort**: 11-18 hours (1.5-2.5 days)

## Compatibility Matrix

| Dependency | Current | Target | Breaking Changes |
|-----------|---------|--------|------------------|
| react | 18.3.1 | 19.2.0 | Yes (minor) |
| react-dom | 18.3.1 | 19.2.0 | Yes (minor) |
| next | 15.5.6 | 16.0.1 | Yes (major) |
| react-chessboard | 4.7.3 | 5.8.3 | Yes (moderate) |
| @types/react | 18.2.66 | 19.2.2 | Yes (major) |
| @types/react-dom | 18.2.22 | 19.2.2 | Yes (major) |

## Key Findings

### ✅ Strengths
1. **No class components** - All functional components, easier migration
2. **Minimal Next.js async API usage** - Only 22 files affected
3. **react-chessboard ready** - Chess page already compatible
4. **Modern codebase** - Using latest patterns (hooks, functional components)

### ⚠️ Risks
1. **TypeScript type errors** - Expect 50-100 type errors from @types/react 19.x
2. **Next.js async APIs** - 22 files need async conversion
3. **Testing complexity** - 4000+ Jest + 30 E2E tests must pass
4. **Streaming QA** - SSE streaming needs thorough testing

### 🎯 Recommendations
1. **Test early, test often** - Run tests after each phase
2. **Use codemods** - React provides codemods for automated migration
3. **Incremental approach** - Merge dependencies first, then code changes
4. **Staging deployment** - Deploy to dev environment first, then staging
5. **Rollback plan** - Keep backup branch for easy rollback

## Next Steps

1. ✅ **Phase 1**: Update `package.json` dependencies
2. ✅ **Phase 2**: Run `pnpm install` and resolve conflicts
3. ✅ **Phase 3**: Run tests to identify breaking changes
4. ⏳ **Phase 4**: Fix TypeScript errors
5. ⏳ **Phase 5**: Convert Next.js async APIs
6. ⏳ **Phase 6**: Update react-chessboard
7. ⏳ **Phase 7**: Full testing and validation

## References

- React 19 Research: `claudedocs/research_react19_migration_20251110.md`
- Next.js 16 Research: `claudedocs/research_nextjs16_migration_20251110_112519.md`
- react-chessboard Research: `claudedocs/research_react_chessboard_5x_20251110_113130.md`
- Issue #823: https://github.com/DegrassiAaron/meepleai-monorepo/issues/823
