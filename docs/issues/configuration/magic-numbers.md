# Issue: Centralize Magic Numbers

**ID**: CFG-001
**Category**: Configuration
**Priority**: 🟡 **HIGH**
**Status**: 🔴 Open
**Created**: 2025-11-19

---

## 📋 Summary

Centralizzare tutti i magic numbers e stringhe hardcoded in file di configurazione type-safe.

---

## 🎯 Problem Statement

### Current Issues
```typescript
// ❌ Magic numbers sparsi nel codebase
const MAX_THREADS_PER_GAME = 5; // ChatSidebar.tsx:22
const autoTitle = content.substring(0, 50); // messagesSlice.ts:92
```

**Problems**:
- ⚠️ Duplicazione valori
- ⚠️ Non configurabile
- ⚠️ Testing difficile

---

## 🔧 Solution

### Create Config Files

**File**: `apps/web/src/config/chat.ts`
```typescript
export const CHAT_CONFIG = {
  MAX_THREADS_PER_GAME: 5,
  AUTO_TITLE_MAX_LENGTH: 50,
  MESSAGE_EDIT_TIMEOUT_MS: 5000,
  OPTIMISTIC_UPDATE_TIMEOUT_MS: 3000,
} as const;
```

**File**: `apps/web/src/config/ui.ts`
```typescript
export const UI_CONFIG = {
  SIDEBAR_COLLAPSE_DURATION_MS: 300,
  TOAST_DURATION_MS: 5000,
  MODAL_ANIMATION_DURATION_MS: 200,
} as const;
```

**File**: `apps/web/src/config/api.ts`
```typescript
export const API_CONFIG = {
  REQUEST_TIMEOUT_MS: 30000,
  RETRY_ATTEMPTS: 3,
  CACHE_TTL_MS: 5 * 60 * 1000,
} as const;
```

---

## 📝 Implementation Checklist

- [ ] Create config files in `src/config/`
- [ ] Replace hardcoded values in ~15 files
- [ ] Update tests to use config
- [ ] Document config in README

---

## ✅ Acceptance Criteria

- [ ] All magic numbers centralized
- [ ] Type-safe config (as const)
- [ ] Tests pass

---

## 📊 Effort: 6 hours

---

**Last Updated**: 2025-11-19
**Status**: 🔴 Open
