# Issue: Implement Structured Logging

**ID**: LOG-001
**Category**: Logging & Observability
**Priority**: 🟡 **HIGH**
**Status**: 🔴 Open
**Created**: 2025-11-19

---

## 📋 Summary

Sostituire tutti i `console.error` (155 occorrenze) con un sistema di structured logging che integri Sentry per error tracking in production.

---

## 🎯 Problem Statement

### Current State
```typescript
// ❌ PROBLEMA: console.error senza struttura
catch (err) {
  console.error('Failed to load messages:', err);
  setError('Errore nel caricamento dei messaggi');
}
```

### Issues
- ⚠️ **No structured data** - Impossibile filtrare/aggregare logs
- ⚠️ **No production tracking** - Errori non monitorati
- ⚠️ **No correlation IDs** - Difficile tracciare request flow
- ⚠️ **No context** - Mancano userId, component, action

---

## 📊 Affected Areas

**Occurrences**: 155 console statements
- Store slices: 5 files
- Hooks: 8 files
- Components: ~12 files
- Pages: ~20 files

---

## 🔧 Solution

### 1. Create Logger Infrastructure

**File**: `apps/web/src/lib/logger/index.ts`
```typescript
import * as Sentry from '@sentry/nextjs';

export enum LogLevel {
  Debug = 'debug',
  Info = 'info',
  Warn = 'warn',
  Error = 'error',
}

interface LogContext {
  [key: string]: unknown;
  userId?: string;
  correlationId?: string;
  component?: string;
  action?: string;
}

class Logger {
  private isDevelopment = process.env.NODE_ENV === 'development';
  private isProduction = process.env.NODE_ENV === 'production';

  error(message: string, error?: Error | unknown, context?: LogContext): void {
    console.error(`[ERROR] ${message}`, error, context);

    if (this.isProduction) {
      Sentry.captureException(
        error instanceof Error ? error : new Error(message),
        {
          extra: { ...context, originalMessage: message },
          level: 'error',
        }
      );
    }
  }

  warn(message: string, context?: LogContext): void {
    console.warn(`[WARN] ${message}`, context);

    if (this.isProduction) {
      Sentry.captureMessage(message, {
        level: 'warning',
        extra: context,
      });
    }
  }

  info(message: string, context?: LogContext): void {
    if (this.isDevelopment) {
      console.info(`[INFO] ${message}`, context);
    }
  }

  debug(message: string, context?: LogContext): void {
    if (this.isDevelopment) {
      console.debug(`[DEBUG] ${message}`, context);
    }
  }
}

export const logger = new Logger();
```

### 2. Sentry Configuration

**File**: `apps/web/sentry.client.config.ts`
```typescript
import * as Sentry from '@sentry/nextjs';

Sentry.init({
  dsn: process.env.NEXT_PUBLIC_SENTRY_DSN,
  environment: process.env.NODE_ENV,
  tracesSampleRate: 0.1, // 10% sampling
  beforeSend(event, hint) {
    // Filter out development errors
    if (process.env.NODE_ENV === 'development') {
      return null;
    }
    return event;
  },
});
```

### 3. Usage Examples

**Before**:
```typescript
// Store slice
catch (err) {
  console.error('Failed to load messages:', err);
  setError('Errore nel caricamento dei messaggi');
}
```

**After**:
```typescript
import { logger } from '@/lib/logger';

catch (err) {
  logger.error('Failed to load messages', err, {
    component: 'MessageSlice',
    action: 'loadMessages',
    threadId,
    gameId: selectedGameId,
  });
  setError('Errore nel caricamento dei messaggi');
}
```

---

## 📝 Implementation Checklist

### Phase 1: Infrastructure (3h)
- [ ] Install Sentry: `pnpm add @sentry/nextjs`
- [ ] Create logger module
- [ ] Configure Sentry (client + server)
- [ ] Add environment variables
- [ ] Write logger tests

### Phase 2: Replace Console Statements (5h)
- [ ] Replace in store slices (5 files)
- [ ] Replace in hooks (8 files)
- [ ] Replace in components (12 files)
- [ ] Replace in pages (20+ files)

### Phase 3: Testing & Validation (2h)
- [ ] Test logger in development
- [ ] Test Sentry integration in staging
- [ ] Verify correlation IDs
- [ ] Check Sentry dashboard

---

## ✅ Acceptance Criteria

- [ ] All console.error replaced with logger
- [ ] Sentry integration configured
- [ ] Logs include context (userId, component, action)
- [ ] Correlation IDs in all logs
- [ ] Error rates visible in Sentry dashboard
- [ ] Tests pass

---

## 📊 Effort: 8 hours

---

**Last Updated**: 2025-11-19
**Status**: 🔴 Open
