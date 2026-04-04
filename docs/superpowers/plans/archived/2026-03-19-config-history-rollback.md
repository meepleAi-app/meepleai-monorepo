# Config History & Rollback UI — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add ConfigHistoryDialog component with timeline and rollback, integrate into FeatureFlagsTab, and write browser tests.

**Architecture:** New standalone dialog component using existing `api.config.getHistory()` and `api.config.rollback()` client methods. Integrates via a History column in FeatureFlagsTab.

**Tech Stack:** React, Tailwind, Radix Dialog, Lucide icons, Sonner toast, Playwright

---

## Task 1: Create ConfigHistoryDialog component

**Files:**
- Create: `apps/web/src/components/admin/ConfigHistoryDialog.tsx`

- [ ] **Step 1: Create the dialog component**

```typescript
'use client';

import { useCallback, useEffect, useState } from 'react';

import { Clock, RotateCcw } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { ConfigurationHistoryDto } from '@/lib/api/schemas/config.schemas';

interface ConfigHistoryDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  configId: string;
  configKey: string;
  onRollbackComplete: () => void;
}

export function ConfigHistoryDialog({
  open,
  onOpenChange,
  configId,
  configKey,
  onRollbackComplete,
}: ConfigHistoryDialogProps) {
  const [history, setHistory] = useState<ConfigurationHistoryDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [rollingBack, setRollingBack] = useState(false);

  const fetchHistory = useCallback(async () => {
    if (!configId) return;
    setLoading(true);
    try {
      const data = await api.config.getHistory(configId, 20);
      setHistory(data);
    } catch {
      setHistory([]);
      toast.error('Errore nel caricamento della cronologia');
    } finally {
      setLoading(false);
    }
  }, [configId]);

  useEffect(() => {
    if (open) {
      fetchHistory();
    }
  }, [open, fetchHistory]);

  const canRollback =
    history.length > 0 &&
    history[0].changeReason !== 'Configuration created' &&
    history[0].oldValue !== '';

  const handleRollback = async () => {
    if (!canRollback) return;

    const entry = history[0];
    const confirmed = window.confirm(
      `Ripristinare il valore precedente '${entry.oldValue}'?`
    );
    if (!confirmed) return;

    setRollingBack(true);
    try {
      await api.config.rollback(configId, entry.version);
      toast.success('Configurazione ripristinata');
      onRollbackComplete();
      onOpenChange(false);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Errore durante il rollback';
      toast.error(msg);
    } finally {
      setRollingBack(false);
    }
  };

  const formatDate = (iso: string) => {
    try {
      return new Intl.DateTimeFormat('it-IT', {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(new Date(iso));
    } catch {
      return iso;
    }
  };

  const displayKey = configKey.replace('Features:', '');

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg" data-testid="config-history-dialog">
        <DialogHeader>
          <DialogTitle className="font-quicksand flex items-center gap-2">
            <Clock className="h-5 w-5 text-muted-foreground" />
            Cronologia: {displayKey}
          </DialogTitle>
        </DialogHeader>

        <div className="py-2">
          {loading ? (
            <div className="flex items-center justify-center py-8 text-muted-foreground text-sm">
              Caricamento…
            </div>
          ) : history.length === 0 ? (
            <div className="text-center py-8 text-muted-foreground text-sm">
              Nessuna cronologia disponibile.
            </div>
          ) : (
            <div className="space-y-0">
              {history.map((entry, idx) => {
                const isFirst = idx === 0;
                const isCreation = entry.changeReason === 'Configuration created';
                const isLast = idx === history.length - 1;

                return (
                  <div
                    key={entry.id}
                    className="relative flex gap-3"
                    data-testid={`history-entry-${entry.version}`}
                  >
                    {/* Timeline line */}
                    <div className="flex flex-col items-center">
                      <div
                        className={`h-3 w-3 rounded-full border-2 mt-1.5 ${
                          isFirst
                            ? 'border-primary bg-primary'
                            : 'border-muted-foreground/40 bg-background'
                        }`}
                      />
                      {!isLast && (
                        <div className="w-px flex-1 bg-border" />
                      )}
                    </div>

                    {/* Content */}
                    <div className="pb-4 flex-1">
                      <div className="flex items-center gap-2 mb-1">
                        <Badge variant="outline" className="text-xs font-mono">
                          v{entry.version}
                        </Badge>
                        <span className="text-xs text-muted-foreground">
                          {formatDate(entry.changedAt)}
                        </span>
                      </div>

                      <div className="text-sm">
                        {isCreation ? (
                          <span className="text-muted-foreground">
                            Creato con valore{' '}
                            <code className="px-1 py-0.5 rounded bg-muted text-xs">
                              {entry.newValue}
                            </code>
                          </span>
                        ) : (
                          <span>
                            <code className="px-1 py-0.5 rounded bg-red-50 dark:bg-red-950/30 text-red-700 dark:text-red-300 text-xs">
                              {entry.oldValue}
                            </code>
                            <span className="mx-1.5 text-muted-foreground">→</span>
                            <code className="px-1 py-0.5 rounded bg-green-50 dark:bg-green-950/30 text-green-700 dark:text-green-300 text-xs">
                              {entry.newValue}
                            </code>
                          </span>
                        )}
                      </div>

                      <p className="text-xs text-muted-foreground mt-0.5">
                        {entry.changeReason}
                      </p>

                      {/* Rollback button: only on most recent non-creation entry */}
                      {isFirst && canRollback && (
                        <Button
                          variant="outline"
                          size="sm"
                          className="mt-2"
                          onClick={handleRollback}
                          disabled={rollingBack}
                          data-testid="btn-rollback"
                        >
                          <RotateCcw className="h-3 w-3 mr-1" />
                          {rollingBack ? 'Ripristino…' : 'Rollback'}
                        </Button>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>

        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => onOpenChange(false)}
            data-testid="btn-close-history"
          >
            Chiudi
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
```

- [ ] **Step 2: Verify TypeScript compiles**

Run: `cd apps/web && npx tsc --noEmit`
Expected: No errors

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/admin/ConfigHistoryDialog.tsx
git commit -m "feat(admin): add ConfigHistoryDialog component for config history/rollback"
```

---

## Task 2: Integrate into FeatureFlagsTab

**Files:**
- Modify: `apps/web/src/components/admin/FeatureFlagsTab.tsx`

- [ ] **Step 1: Add History column and dialog state to FeatureFlagsTab**

Changes needed:
1. Import `ConfigHistoryDialog` and `Clock` icon
2. Add state: `const [historyConfigId, setHistoryConfigId] = useState<string | null>(null);` and `const [historyConfigKey, setHistoryConfigKey] = useState('');`
3. Add `<th>` column header after Status column
4. Add `<td>` with History button per row
5. Render `<ConfigHistoryDialog>` at the bottom of the component

Specific additions:
- Import: `import { ConfigHistoryDialog } from './ConfigHistoryDialog';`
- Import: Add `Clock` to the lucide-react import
- State: `historyConfigId` and `historyConfigKey`
- Column header: `<th className="px-4 py-3 text-center font-semibold text-foreground">History</th>`
- Cell per row: Button with `data-testid="btn-history-{flag.id}"` and Clock icon
- Dialog: `<ConfigHistoryDialog open={!!historyConfigId} configId={historyConfigId ?? ''} configKey={historyConfigKey} onOpenChange={(open) => { if (!open) setHistoryConfigId(null); }} onRollbackComplete={onConfigurationChange} />`

- [ ] **Step 2: Verify TypeScript compiles**

Run: `cd apps/web && npx tsc --noEmit`

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/admin/FeatureFlagsTab.tsx
git commit -m "feat(admin): add History column to FeatureFlagsTab with ConfigHistoryDialog"
```

---

## Task 3: Browser tests for history and rollback

**Files:**
- Create: `apps/web/e2e/admin/admin-config-history.spec.ts`

- [ ] **Step 1: Create the spec file**

3 tests:
- **CH-01**: View configuration history — mock getHistory with 2 entries, click History button, assert dialog opens with timeline entries
- **CH-02**: Rollback to previous value — mock getHistory + rollback endpoint, click Rollback, accept confirm, assert API called + toast + dialog closes
- **CH-03**: No rollback for creation-only — mock getHistory with 1 creation entry, assert no Rollback button visible

Mock patterns: Same as admin-feature-flags.spec.ts (mockAdminAuth, mock configurations, mock history endpoint per config ID)

Key selectors:
- `data-testid="btn-history-{configId}"` — opens dialog
- `data-testid="config-history-dialog"` — the dialog
- `data-testid="history-entry-{version}"` — timeline entries
- `data-testid="btn-rollback"` — rollback button
- `page.on('dialog')` for `window.confirm` on rollback

- [ ] **Step 2: Run test to verify**

Run: `cd apps/web && npx playwright test e2e/admin/admin-config-history.spec.ts --list --reporter=line`
Expected: 3 tests listed

- [ ] **Step 3: Commit**

```bash
git add apps/web/e2e/admin/admin-config-history.spec.ts
git commit -m "test(e2e): add browser tests for config history and rollback (CH-01, CH-02, CH-03)"
```
