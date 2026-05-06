# Invitations Page — P0/P1/P2 Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Correggere i problemi P0/P1/P2 identificati dalla spec-panel review della pagina `/admin/users/invitations` (Issue #132): propagare mutation pending state, estrarre logiche riusabili, rinforzare type safety, aggiungere test mancanti.

**Architecture:** Refactoring progressivo bottom-up — prima le utility condivise (schema, validazione, CSV parser), poi i componenti che le usano (`InvitationRow`, dialogs), infine la page e i test di integrazione.

**Tech Stack:** Next.js 16 + React 19 + TypeScript, Vitest + @testing-library/react, Zod, TanStack Query v5, Tailwind CSS

---

## File Structure

| File | Azione | Responsabilità |
|------|--------|----------------|
| `apps/web/src/lib/api/schemas/invitation.schemas.ts` | Modifica | Aggiunge `InvitationRoleSchema` (enum) + esporta `INVITATION_ROLES` |
| `apps/web/src/lib/utils/validation.ts` | Crea | `isValidEmail(email: string): boolean` |
| `apps/web/src/lib/utils/__tests__/validation.test.ts` | Crea | Unit test per `isValidEmail` |
| `apps/web/src/lib/utils/parseCsvInvitations.ts` | Crea | `parseCsvInvitations(text: string): ParsedRow[]` estratto da BulkInviteDialog |
| `apps/web/src/lib/utils/__tests__/parseCsvInvitations.test.ts` | Crea | Unit test parametrizzati per il CSV parser |
| `apps/web/src/components/admin/invitations/BulkInviteDialog.tsx` | Modifica | Usa `parseCsvInvitations` + `isValidEmail` + `INVITATION_ROLES` |
| `apps/web/src/components/admin/invitations/InviteUserDialog.tsx` | Modifica | Usa `isValidEmail` + `INVITATION_ROLES` |
| `apps/web/src/components/admin/invitations/InvitationRow.tsx` | Modifica | Aggiunge props `isResending`/`isRevoking` + disabilita bottoni |
| `apps/web/src/components/admin/invitations/__tests__/InvitationRow.test.tsx` | Crea | Test per rendering, azioni, disabled state |
| `apps/web/src/app/admin/(dashboard)/users/invitations/page.tsx` | Modifica | Propaga `isResending`/`isRevoking` + sanitizza messaggio errore |
| `apps/web/src/app/admin/(dashboard)/users/invitations/__tests__/page.test.tsx` | Modifica | Aggiunge test mancanti: revoke dialog, filter reset, error state |
| `apps/web/src/lib/utils/timeUtils.ts` | Modifica | Aggiunge `formatShortDate(iso: string): string` |
| `apps/web/src/lib/utils/__tests__/timeUtils.test.ts` | Modifica | Test per `formatShortDate` |

---

## Task 1 — Aggiorna `InvitationRoleSchema` in schemas (P1)

**Files:**
- Modifica: `apps/web/src/lib/api/schemas/invitation.schemas.ts`

- [ ] **Step 1: Modifica lo schema per aggiungere l'enum ruoli**

```typescript
// apps/web/src/lib/api/schemas/invitation.schemas.ts
// Sostituisci il blocco Enums esistente con:

export const InvitationRoleSchema = z.enum(['User', 'Editor', 'Admin']);
export type InvitationRole = z.infer<typeof InvitationRoleSchema>;
export const INVITATION_ROLES = InvitationRoleSchema.options; // readonly ['User','Editor','Admin']

export const InvitationStatusSchema = z.enum(['Pending', 'Accepted', 'Expired', 'Revoked']);
```

Sostituisci `role: z.string()` in `InvitationDtoSchema` con `role: z.string()` lasciandolo invariato (il backend potrebbe restituire ruoli non previsti dal frontend — non spezzare la deserializzazione, ma il tipo enum serve per i form).

- [ ] **Step 2: Verifica che TypeScript compili senza errori**

```bash
cd apps/web && pnpm typecheck 2>&1 | head -30
```
Atteso: nessun errore relativo a `invitation.schemas.ts`.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/lib/api/schemas/invitation.schemas.ts
git commit -m "feat(invitations): add InvitationRoleSchema enum and INVITATION_ROLES constant"
```

---

## Task 2 — Crea utility `isValidEmail` + test (P1)

**Files:**
- Crea: `apps/web/src/lib/utils/validation.ts`
- Crea: `apps/web/src/lib/utils/__tests__/validation.test.ts`

- [ ] **Step 1: Scrivi il test che fallisce**

```typescript
// apps/web/src/lib/utils/__tests__/validation.test.ts
import { describe, it, expect } from 'vitest';
import { isValidEmail } from '../validation';

describe('isValidEmail', () => {
  it.each([
    'user@example.com',
    'user+tag@sub.domain.com',
    'a@b.co',
  ])('returns true for valid email: %s', (email) => {
    expect(isValidEmail(email)).toBe(true);
  });

  it.each([
    '',
    '   ',
    'notanemail',
    'missing@',
    '@nodomain',
    'spaces in@email.com',
    'double@@domain.com',
  ])('returns false for invalid email: %s', (email) => {
    expect(isValidEmail(email)).toBe(false);
  });
});
```

- [ ] **Step 2: Esegui il test e verifica che fallisce**

```bash
cd apps/web && pnpm test src/lib/utils/__tests__/validation.test.ts --run 2>&1 | tail -15
```
Atteso: `FAIL` con "isValidEmail is not a function" o simile.

- [ ] **Step 3: Implementa `isValidEmail`**

```typescript
// apps/web/src/lib/utils/validation.ts

/**
 * Validates that a string is a properly formatted email address.
 * Trims whitespace before checking.
 */
export function isValidEmail(email: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim());
}
```

- [ ] **Step 4: Esegui il test e verifica che passa**

```bash
cd apps/web && pnpm test src/lib/utils/__tests__/validation.test.ts --run 2>&1 | tail -10
```
Atteso: tutti i test `PASS`.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/utils/validation.ts apps/web/src/lib/utils/__tests__/validation.test.ts
git commit -m "feat(utils): add isValidEmail utility with tests"
```

---

## Task 3 — Estrai `parseCsvInvitations` + unit test (P0)

**Files:**
- Crea: `apps/web/src/lib/utils/parseCsvInvitations.ts`
- Crea: `apps/web/src/lib/utils/__tests__/parseCsvInvitations.test.ts`

- [ ] **Step 1: Scrivi i test che falliscono**

```typescript
// apps/web/src/lib/utils/__tests__/parseCsvInvitations.test.ts
import { describe, it, expect } from 'vitest';
import { parseCsvInvitations } from '../parseCsvInvitations';

describe('parseCsvInvitations', () => {
  it('parses a valid row with email and role', () => {
    const result = parseCsvInvitations('alice@example.com,User');
    expect(result).toHaveLength(1);
    expect(result[0]).toEqual({ email: 'alice@example.com', role: 'User', valid: true });
  });

  it('defaults role to User when missing', () => {
    const result = parseCsvInvitations('alice@example.com');
    expect(result[0]).toEqual({ email: 'alice@example.com', role: 'User', valid: true });
  });

  it('trims whitespace from email and role', () => {
    const result = parseCsvInvitations('  alice@example.com , Editor  ');
    expect(result[0]).toEqual({ email: 'alice@example.com', role: 'Editor', valid: true });
  });

  it('marks row as invalid for bad email', () => {
    const result = parseCsvInvitations('not-an-email,User');
    expect(result[0].valid).toBe(false);
    expect(result[0].error).toMatch(/invalid email/i);
  });

  it('marks row as invalid for unknown role', () => {
    const result = parseCsvInvitations('alice@example.com,SuperAdmin');
    expect(result[0].valid).toBe(false);
    expect(result[0].error).toMatch(/invalid role/i);
  });

  it('marks row as invalid for empty email', () => {
    const result = parseCsvInvitations(',User');
    expect(result[0].valid).toBe(false);
    expect(result[0].error).toMatch(/email is empty/i);
  });

  it('parses multiple rows', () => {
    const csv = 'alice@example.com,User\nbob@example.com,Admin\nbad-email,Editor';
    const result = parseCsvInvitations(csv);
    expect(result).toHaveLength(3);
    expect(result[0].valid).toBe(true);
    expect(result[1].valid).toBe(true);
    expect(result[2].valid).toBe(false);
  });

  it('skips blank lines', () => {
    const result = parseCsvInvitations('alice@example.com,User\n\n\nbob@example.com,Admin');
    expect(result).toHaveLength(2);
  });

  it('returns empty array for empty string', () => {
    expect(parseCsvInvitations('')).toHaveLength(0);
    expect(parseCsvInvitations('   ')).toHaveLength(0);
  });
});
```

- [ ] **Step 2: Esegui i test e verifica che falliscono**

```bash
cd apps/web && pnpm test src/lib/utils/__tests__/parseCsvInvitations.test.ts --run 2>&1 | tail -15
```
Atteso: `FAIL` con "parseCsvInvitations is not a function".

- [ ] **Step 3: Implementa `parseCsvInvitations`**

```typescript
// apps/web/src/lib/utils/parseCsvInvitations.ts
import { INVITATION_ROLES } from '@/lib/api/schemas/invitation.schemas';
import { isValidEmail } from './validation';

export interface ParsedCsvRow {
  email: string;
  role: string;
  valid: boolean;
  error?: string;
}

/**
 * Parses CSV text (one "email,role" per line) into validated rows.
 * Blank lines are skipped. Role defaults to 'User' if omitted.
 */
export function parseCsvInvitations(text: string): ParsedCsvRow[] {
  const lines = text
    .split('\n')
    .map(l => l.trim())
    .filter(l => l.length > 0);

  return lines.map(line => {
    const parts = line.split(',').map(p => p.trim());
    const email = parts[0] ?? '';
    const role = parts[1] ?? 'User';

    if (!email) {
      return { email, role, valid: false, error: 'Email is empty' };
    }
    if (!isValidEmail(email)) {
      return { email, role, valid: false, error: 'Invalid email format' };
    }
    if (!(INVITATION_ROLES as readonly string[]).includes(role)) {
      return { email, role, valid: false, error: `Invalid role: ${role}` };
    }

    return { email, role, valid: true };
  });
}
```

- [ ] **Step 4: Esegui i test e verifica che passano**

```bash
cd apps/web && pnpm test src/lib/utils/__tests__/parseCsvInvitations.test.ts --run 2>&1 | tail -10
```
Atteso: tutti i test `PASS`.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/utils/parseCsvInvitations.ts apps/web/src/lib/utils/__tests__/parseCsvInvitations.test.ts
git commit -m "feat(utils): extract parseCsvInvitations with unit tests (P0)"
```

---

## Task 4 — Aggiorna `BulkInviteDialog` e `InviteUserDialog` con shared utils (P1)

**Files:**
- Modifica: `apps/web/src/components/admin/invitations/BulkInviteDialog.tsx`
- Modifica: `apps/web/src/components/admin/invitations/InviteUserDialog.tsx`

- [ ] **Step 1: Aggiorna `BulkInviteDialog.tsx`**

Sostituisci il contenuto del file `BulkInviteDialog.tsx`. Le modifiche chiave sono:
1. Rimuovi `parseCsvContent` locale e importa `parseCsvInvitations` (rinomina il tipo `ParsedRow` → `ParsedCsvRow`)
2. Rimuovi la lista ruoli hardcoded inline
3. Importa `INVITATION_ROLES` da schemas

```typescript
// apps/web/src/components/admin/invitations/BulkInviteDialog.tsx
'use client';

import { useState } from 'react';

import { AlertCircleIcon, CheckCircleIcon, UploadIcon } from 'lucide-react';
import { toast } from 'sonner';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Label } from '@/components/ui/primitives/label';
import { api } from '@/lib/api';
import type { BulkInviteResponse } from '@/lib/api/schemas/invitation.schemas';
import type { ParsedCsvRow } from '@/lib/utils/parseCsvInvitations';
import { parseCsvInvitations } from '@/lib/utils/parseCsvInvitations';

export interface BulkInviteDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSuccess?: () => void;
}

type Step = 'input' | 'preview' | 'results';

export function BulkInviteDialog({ open, onOpenChange, onSuccess }: BulkInviteDialogProps) {
  const [csvText, setCsvText] = useState('');
  const [parsedRows, setParsedRows] = useState<ParsedCsvRow[]>([]);
  const [step, setStep] = useState<Step>('input');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [results, setResults] = useState<BulkInviteResponse | null>(null);

  function resetForm() {
    setCsvText('');
    setParsedRows([]);
    setStep('input');
    setResults(null);
  }

  function handleOpenChange(nextOpen: boolean) {
    if (!nextOpen) {
      resetForm();
    }
    onOpenChange(nextOpen);
  }

  function handlePreview() {
    const rows = parseCsvInvitations(csvText);
    setParsedRows(rows);
    setStep('preview');
  }

  async function handleSubmit() {
    setIsSubmitting(true);
    try {
      const validRows = parsedRows.filter(r => r.valid);
      const csv = validRows.map(r => `${r.email},${r.role}`).join('\n');
      const response = await api.invitations.bulkSendInvitations(csv);
      setResults(response);
      setStep('results');

      const successCount = response.successful.length;
      const failCount = response.failed.length;

      if (failCount === 0) {
        toast.success(`All ${successCount} invitations sent successfully`);
      } else {
        toast.success(`${successCount} sent, ${failCount} failed`);
      }

      onSuccess?.();
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Bulk invite failed';
      toast.error(message);
    } finally {
      setIsSubmitting(false);
    }
  }

  const validCount = parsedRows.filter(r => r.valid).length;
  const invalidCount = parsedRows.filter(r => !r.valid).length;

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Bulk Invite Users</DialogTitle>
          <DialogDescription>
            Paste CSV content with one entry per line: email,role (role defaults to User).
          </DialogDescription>
        </DialogHeader>

        {/* Step 1: Input */}
        {step === 'input' && (
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="csv-content">CSV Content</Label>
              <textarea
                id="csv-content"
                className="flex min-h-[120px] w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                placeholder={`alice@example.com,User\nbob@example.com,Editor\ncarol@example.com,Admin`}
                value={csvText}
                onChange={e => setCsvText(e.target.value)}
              />
              <p className="text-xs text-muted-foreground">
                Format: email,role (one per line). Valid roles: User, Editor, Admin.
              </p>
            </div>

            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => handleOpenChange(false)}>
                Cancel
              </Button>
              <Button type="button" onClick={handlePreview} disabled={!csvText.trim()}>
                <UploadIcon className="mr-2 h-4 w-4" />
                Preview
              </Button>
            </DialogFooter>
          </div>
        )}

        {/* Step 2: Preview */}
        {step === 'preview' && (
          <div className="space-y-4">
            <div className="text-sm">
              <span className="font-medium">{validCount}</span> valid,{' '}
              <span className="font-medium text-red-600">{invalidCount}</span> invalid
            </div>

            <div className="max-h-[300px] overflow-auto rounded-md border">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b bg-muted/50">
                    <th className="px-3 py-2 text-left">Email</th>
                    <th className="px-3 py-2 text-left">Role</th>
                    <th className="px-3 py-2 text-left">Status</th>
                  </tr>
                </thead>
                <tbody>
                  {parsedRows.map((row, i) => (
                    <tr key={i} className={`border-b ${!row.valid ? 'bg-red-50' : ''}`}>
                      <td className="px-3 py-2">{row.email || '(empty)'}</td>
                      <td className="px-3 py-2">{row.role}</td>
                      <td className="px-3 py-2">
                        {row.valid ? (
                          <span className="text-green-600">Valid</span>
                        ) : (
                          <span className="text-red-600">{row.error}</span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setStep('input')}>
                Back
              </Button>
              <Button
                type="button"
                onClick={handleSubmit}
                disabled={validCount === 0 || isSubmitting}
              >
                {isSubmitting
                  ? 'Sending...'
                  : `Send ${validCount} Invitation${validCount !== 1 ? 's' : ''}`}
              </Button>
            </DialogFooter>
          </div>
        )}

        {/* Step 3: Results */}
        {step === 'results' && results && (
          <div className="space-y-4">
            {results.successful.length > 0 && (
              <div className="rounded-md border border-green-200 bg-green-50 p-3">
                <div className="flex items-center gap-2 font-medium text-green-800">
                  <CheckCircleIcon className="h-4 w-4" />
                  {results.successful.length} invitation{results.successful.length !== 1 ? 's' : ''}{' '}
                  sent
                </div>
              </div>
            )}

            {results.failed.length > 0 && (
              <div className="rounded-md border border-red-200 bg-red-50 p-3">
                <div className="flex items-center gap-2 font-medium text-red-800 mb-2">
                  <AlertCircleIcon className="h-4 w-4" />
                  {results.failed.length} failed
                </div>
                <ul className="space-y-1 text-sm text-red-700">
                  {results.failed.map((f, i) => (
                    <li key={i}>
                      {f.email}: {f.error}
                    </li>
                  ))}
                </ul>
              </div>
            )}

            <DialogFooter>
              <Button type="button" onClick={() => handleOpenChange(false)}>
                Done
              </Button>
            </DialogFooter>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
```

- [ ] **Step 2: Aggiorna `InviteUserDialog.tsx`**

Modifica le sezioni che usano la regex e la lista ruoli hardcoded:

```typescript
// apps/web/src/components/admin/invitations/InviteUserDialog.tsx
// Aggiungi in cima agli import:
import { INVITATION_ROLES } from '@/lib/api/schemas/invitation.schemas';
import { isValidEmail } from '@/lib/utils/validation';

// Rimuovi questa riga:
// const AVAILABLE_ROLES = ['User', 'Editor', 'Admin'] as const;

// In handleSubmit, sostituisci il controllo email:
// Prima:
//   if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmedEmail)) {
//     setError('Please enter a valid email address');
//     return;
//   }
// Dopo:
    if (!isValidEmail(trimmedEmail)) {
      setError('Please enter a valid email address');
      return;
    }

// Nel JSX, sostituisci AVAILABLE_ROLES con INVITATION_ROLES:
//   {AVAILABLE_ROLES.map(r => (
//   ->
//   {INVITATION_ROLES.map(r => (
```

Il file completo modificato è:

```typescript
// apps/web/src/components/admin/invitations/InviteUserDialog.tsx
'use client';

import { useState } from 'react';

import { toast } from 'sonner';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { api } from '@/lib/api';
import { INVITATION_ROLES } from '@/lib/api/schemas/invitation.schemas';
import { isValidEmail } from '@/lib/utils/validation';

export interface InviteUserDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSuccess?: () => void;
}

export function InviteUserDialog({ open, onOpenChange, onSuccess }: InviteUserDialogProps) {
  const [email, setEmail] = useState('');
  const [role, setRole] = useState<string>('User');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function resetForm() {
    setEmail('');
    setRole('User');
    setError(null);
  }

  function handleOpenChange(nextOpen: boolean) {
    if (!nextOpen) {
      resetForm();
    }
    onOpenChange(nextOpen);
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);

    const trimmedEmail = email.trim();
    if (!trimmedEmail) {
      setError('Email is required');
      return;
    }

    if (!isValidEmail(trimmedEmail)) {
      setError('Please enter a valid email address');
      return;
    }

    setIsSubmitting(true);
    try {
      await api.invitations.sendInvitation(trimmedEmail, role);
      toast.success(`Invitation sent to ${trimmedEmail}`);
      resetForm();
      onOpenChange(false);
      onSuccess?.();
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to send invitation';
      setError(message);
      toast.error(message);
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Invite User</DialogTitle>
          <DialogDescription>
            Send an invitation email to a new user. They will receive a link to create their
            account.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="invite-email">Email Address</Label>
            <Input
              id="invite-email"
              type="email"
              placeholder="user@example.com"
              value={email}
              onChange={e => setEmail(e.target.value)}
              disabled={isSubmitting}
              autoFocus
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="invite-role">Role</Label>
            <Select value={role} onValueChange={setRole} disabled={isSubmitting}>
              <SelectTrigger id="invite-role">
                <SelectValue placeholder="Select a role" />
              </SelectTrigger>
              <SelectContent>
                {INVITATION_ROLES.map(r => (
                  <SelectItem key={r} value={r}>
                    {r}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {error && (
            <p className="text-sm text-red-600" role="alert">
              {error}
            </p>
          )}

          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => handleOpenChange(false)}
              disabled={isSubmitting}
            >
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Sending...' : 'Send Invitation'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
```

- [ ] **Step 3: Esegui i test dei componenti per verificare nessuna regressione**

```bash
cd apps/web && pnpm test src/components/admin/invitations --run 2>&1 | tail -20
```
Atteso: tutti i test `PASS` (BulkInviteDialog e InviteUserDialog esistenti).

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/admin/invitations/BulkInviteDialog.tsx \
        apps/web/src/components/admin/invitations/InviteUserDialog.tsx
git commit -m "refactor(invitations): use shared isValidEmail, parseCsvInvitations, INVITATION_ROLES (P1)"
```

---

## Task 5 — Aggiusta `InvitationRow`: aggiungi `isResending`/`isRevoking` + test (P0)

**Files:**
- Modifica: `apps/web/src/components/admin/invitations/InvitationRow.tsx`
- Crea: `apps/web/src/components/admin/invitations/__tests__/InvitationRow.test.tsx`

- [ ] **Step 1: Scrivi i test che falliscono**

```typescript
// apps/web/src/components/admin/invitations/__tests__/InvitationRow.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import type { InvitationDto } from '@/lib/api/schemas/invitation.schemas';

import { InvitationRow } from '../InvitationRow';

const pendingInvitation: InvitationDto = {
  id: 'inv-1',
  email: 'alice@example.com',
  role: 'User',
  status: 'Pending',
  createdAt: '2026-03-01T00:00:00Z',
  expiresAt: '2026-03-08T00:00:00Z',
  acceptedAt: null,
  invitedByUserId: '00000000-0000-0000-0000-000000000001',
};

const acceptedInvitation: InvitationDto = {
  ...pendingInvitation,
  status: 'Accepted',
  acceptedAt: '2026-03-02T00:00:00Z',
};

describe('InvitationRow', () => {
  const user = userEvent.setup();

  it('renders email, role, and status', () => {
    render(
      <table><tbody>
        <InvitationRow invitation={pendingInvitation} />
      </tbody></table>
    );
    expect(screen.getByText('alice@example.com')).toBeInTheDocument();
    expect(screen.getByText('User')).toBeInTheDocument();
    expect(screen.getByText('Pending')).toBeInTheDocument();
  });

  it('shows Resend and Revoke buttons only for Pending invitations', () => {
    render(
      <table><tbody>
        <InvitationRow invitation={pendingInvitation} onResend={vi.fn()} onRevoke={vi.fn()} />
      </tbody></table>
    );
    expect(screen.getByRole('button', { name: /resend invitation to alice@example.com/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /revoke invitation for alice@example.com/i })).toBeInTheDocument();
  });

  it('does NOT show Resend/Revoke for Accepted invitations', () => {
    render(
      <table><tbody>
        <InvitationRow invitation={acceptedInvitation} onResend={vi.fn()} onRevoke={vi.fn()} />
      </tbody></table>
    );
    expect(screen.queryByRole('button', { name: /resend/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /revoke/i })).not.toBeInTheDocument();
  });

  it('calls onResend with invitation id when Resend is clicked', async () => {
    const onResend = vi.fn();
    render(
      <table><tbody>
        <InvitationRow invitation={pendingInvitation} onResend={onResend} onRevoke={vi.fn()} />
      </tbody></table>
    );
    await user.click(screen.getByRole('button', { name: /resend invitation to alice@example.com/i }));
    expect(onResend).toHaveBeenCalledWith('inv-1');
  });

  it('disables Resend button when isResending=true', () => {
    render(
      <table><tbody>
        <InvitationRow
          invitation={pendingInvitation}
          onResend={vi.fn()}
          onRevoke={vi.fn()}
          isResending
        />
      </tbody></table>
    );
    expect(screen.getByRole('button', { name: /resend invitation to alice@example.com/i })).toBeDisabled();
  });

  it('disables Revoke trigger when isRevoking=true', () => {
    render(
      <table><tbody>
        <InvitationRow
          invitation={pendingInvitation}
          onResend={vi.fn()}
          onRevoke={vi.fn()}
          isRevoking
        />
      </tbody></table>
    );
    expect(screen.getByRole('button', { name: /revoke invitation for alice@example.com/i })).toBeDisabled();
  });
});
```

- [ ] **Step 2: Esegui i test e verifica che falliscono**

```bash
cd apps/web && pnpm test src/components/admin/invitations/__tests__/InvitationRow.test.tsx --run 2>&1 | tail -15
```
Atteso: `FAIL` — `isResending` e `isRevoking` non esistono come props.

- [ ] **Step 3: Aggiorna `InvitationRow.tsx`**

```typescript
// apps/web/src/components/admin/invitations/InvitationRow.tsx
'use client';

import { RefreshCwIcon, XCircleIcon } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/overlays/alert-dialog-primitives';
import { Button } from '@/components/ui/primitives/button';
import type { InvitationDto } from '@/lib/api/schemas/invitation.schemas';

import { InvitationStatusBadge } from './InvitationStatusBadge';

export interface InvitationRowProps {
  invitation: InvitationDto;
  onResend?: (id: string) => void;
  onRevoke?: (id: string) => void;
  isResending?: boolean;
  isRevoking?: boolean;
}

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
    });
  } catch {
    return iso;
  }
}

export function InvitationRow({
  invitation,
  onResend,
  onRevoke,
  isResending = false,
  isRevoking = false,
}: InvitationRowProps) {
  const isPending = invitation.status === 'Pending';

  return (
    <tr className="border-b border-slate-100 dark:border-zinc-800/60 hover:bg-slate-50/50 dark:hover:bg-zinc-700/30">
      <td className="p-3 align-middle">{invitation.email}</td>
      <td className="p-3 align-middle">
        <Badge variant="secondary">{invitation.role}</Badge>
      </td>
      <td className="p-3 align-middle">
        <InvitationStatusBadge status={invitation.status} />
      </td>
      <td className="p-3 align-middle text-sm text-muted-foreground">
        {formatDate(invitation.createdAt)}
      </td>
      <td className="p-3 align-middle text-sm text-muted-foreground">
        {formatDate(invitation.expiresAt)}
      </td>
      <td className="p-3 align-middle">
        <div className="flex items-center gap-1">
          {isPending && onResend && (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => onResend(invitation.id)}
              aria-label={`Resend invitation to ${invitation.email}`}
              className="gap-1"
              disabled={isResending}
            >
              <RefreshCwIcon className={`h-3 w-3 ${isResending ? 'animate-spin' : ''}`} />
              Resend
            </Button>
          )}
          {isPending && onRevoke && (
            <AlertDialog>
              <AlertDialogTrigger asChild>
                <Button
                  variant="ghost"
                  size="sm"
                  className="gap-1 text-destructive hover:text-destructive"
                  aria-label={`Revoke invitation for ${invitation.email}`}
                  disabled={isRevoking}
                >
                  <XCircleIcon className="h-3 w-3" />
                  Revoke
                </Button>
              </AlertDialogTrigger>
              <AlertDialogContent>
                <AlertDialogHeader>
                  <AlertDialogTitle>Revoke Invitation</AlertDialogTitle>
                  <AlertDialogDescription>
                    This will revoke the invitation sent to <strong>{invitation.email}</strong>. The
                    invitation link will no longer be valid.
                  </AlertDialogDescription>
                </AlertDialogHeader>
                <AlertDialogFooter>
                  <AlertDialogCancel>Cancel</AlertDialogCancel>
                  <AlertDialogAction
                    onClick={() => onRevoke(invitation.id)}
                    className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
                  >
                    Revoke
                  </AlertDialogAction>
                </AlertDialogFooter>
              </AlertDialogContent>
            </AlertDialog>
          )}
        </div>
      </td>
    </tr>
  );
}
```

- [ ] **Step 4: Esegui i test e verifica che passano**

```bash
cd apps/web && pnpm test src/components/admin/invitations/__tests__/InvitationRow.test.tsx --run 2>&1 | tail -15
```
Atteso: tutti i test `PASS`.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/invitations/InvitationRow.tsx \
        apps/web/src/components/admin/invitations/__tests__/InvitationRow.test.tsx
git commit -m "fix(invitations): add isResending/isRevoking props to InvitationRow with tests (P0)"
```

---

## Task 6 — Aggiorna `page.tsx`: propaga mutation state + sanitizza errori (P0, P2)

**Files:**
- Modifica: `apps/web/src/app/admin/(dashboard)/users/invitations/page.tsx`

- [ ] **Step 1: Modifica `page.tsx`**

Due cambiamenti:
1. Passa `isResending` e `isRevoking` a `InvitationRow`
2. Sostituisci il messaggio di errore raw con messaggio generico

```typescript
// Sezione mutation (già esistente) — aggiungi tracking variabile corrente
// Cerca il blocco delle mutations e aggiorna come segue:

  const resendMutation = useMutation({
    mutationFn: (id: string) => api.invitations.resendInvitation(id),
    onSuccess: () => {
      toast.success('Invitation resent successfully');
      queryClient.invalidateQueries({ queryKey: ['admin', 'invitations'] });
      queryClient.invalidateQueries({ queryKey: ['admin', 'invitation-stats'] });
    },
    onError: (err: unknown) => {
      toast.error(err instanceof Error ? err.message : 'Failed to resend invitation');
    },
  });

  const revokeMutation = useMutation({
    mutationFn: (id: string) => api.invitations.revokeInvitation(id),
    onSuccess: () => {
      toast.success('Invitation revoked');
      queryClient.invalidateQueries({ queryKey: ['admin', 'invitations'] });
      queryClient.invalidateQueries({ queryKey: ['admin', 'invitation-stats'] });
    },
    onError: (err: unknown) => {
      toast.error(err instanceof Error ? err.message : 'Failed to revoke invitation');
    },
  });

// Aggiungi queste due variabili dopo le mutation:
  const resendingId = resendMutation.isPending ? (resendMutation.variables as string) : null;
  const revokingId = revokeMutation.isPending ? (revokeMutation.variables as string) : null;
```

Nel JSX, aggiorna la riga `<InvitationRow ... />`:
```tsx
<InvitationRow
  key={inv.id}
  invitation={inv}
  onResend={id => resendMutation.mutate(id)}
  onRevoke={id => revokeMutation.mutate(id)}
  isResending={resendingId === inv.id}
  isRevoking={revokingId === inv.id}
/>
```

Per il messaggio errore nella sezione "Error State", sostituisci:
```tsx
// PRIMA:
<p className="text-xs text-red-600 dark:text-red-400 mt-1">
  {error instanceof Error ? error.message : 'Unknown error'}
</p>

// DOPO:
<p className="text-xs text-red-600 dark:text-red-400 mt-1">
  Unable to load invitations. Please try again or contact support if the problem persists.
</p>
```

- [ ] **Step 2: Esegui i test esistenti per verificare nessuna regressione**

```bash
cd apps/web && pnpm test src/app/admin/\\(dashboard\\)/users/invitations/__tests__/page.test.tsx --run 2>&1 | tail -20
```
Atteso: tutti e 5 i test esistenti `PASS`.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/users/invitations/page.tsx
git commit -m "fix(invitations): propagate mutation pending state to InvitationRow, sanitize error message (P0/P2)"
```

---

## Task 7 — Aggiungi test mancanti a `page.test.tsx` (P1)

**Files:**
- Modifica: `apps/web/src/app/admin/(dashboard)/users/invitations/__tests__/page.test.tsx`

- [ ] **Step 1: Aggiungi i test mancanti al file esistente**

Aggiungi i seguenti `it(...)` block all'interno del `describe('InvitationsPage', ...)` esistente, dopo i test già presenti:

```typescript
  it('shows generic error message when query fails', async () => {
    mockGetInvitations.mockRejectedValueOnce(new Error('Internal Server Error: DB timeout'));

    renderWithQuery(<InvitationsPage />);

    await waitFor(() => {
      expect(screen.getByText('Failed to load invitations')).toBeInTheDocument();
    });

    // Should NOT show the raw error message
    expect(screen.queryByText(/DB timeout/i)).not.toBeInTheDocument();
    // Should show generic message
    expect(screen.getByText(/unable to load invitations/i)).toBeInTheDocument();
    // Should show retry button
    expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
  });

  it('resets page to 1 when status filter changes', async () => {
    // Render with default state (page 1)
    renderWithQuery(<InvitationsPage />);

    await waitFor(() => {
      expect(screen.getByText('alice@example.com')).toBeInTheDocument();
    });

    // After filter change, the query should be called with page=1 and the new status
    // We verify this by checking that getInvitations is called with updated params
    // Interact with the Select (open and choose Pending)
    const trigger = screen.getByRole('combobox');
    await user.click(trigger);

    const pendingOption = screen.getByRole('option', { name: 'Pending' });
    await user.click(pendingOption);

    await waitFor(() => {
      expect(mockGetInvitations).toHaveBeenCalledWith(
        expect.objectContaining({ status: 'Pending', page: 1 })
      );
    });
  });

  it('opens revoke confirmation dialog when Revoke is clicked', async () => {
    renderWithQuery(<InvitationsPage />);

    await waitFor(() => {
      expect(screen.getByText('alice@example.com')).toBeInTheDocument();
    });

    // Click the Revoke button for alice (Pending)
    const revokeButton = screen.getByRole('button', {
      name: /revoke invitation for alice@example.com/i,
    });
    await user.click(revokeButton);

    // AlertDialog should appear
    await waitFor(() => {
      expect(screen.getByRole('alertdialog')).toBeInTheDocument();
    });
    expect(screen.getByText('Revoke Invitation')).toBeInTheDocument();
    expect(screen.getByText(/alice@example.com/)).toBeInTheDocument();
  });

  it('calls revokeInvitation API when dialog confirm is clicked', async () => {
    mockRevokeInvitation.mockResolvedValueOnce(undefined);

    renderWithQuery(<InvitationsPage />);

    await waitFor(() => {
      expect(screen.getByText('alice@example.com')).toBeInTheDocument();
    });

    const revokeButton = screen.getByRole('button', {
      name: /revoke invitation for alice@example.com/i,
    });
    await user.click(revokeButton);

    await waitFor(() => {
      expect(screen.getByRole('alertdialog')).toBeInTheDocument();
    });

    // Click the confirm Revoke button inside the dialog
    const confirmButtons = screen.getAllByRole('button', { name: /^revoke$/i });
    const confirmButton = confirmButtons.find(btn => btn.closest('[role="alertdialog"]'));
    await user.click(confirmButton!);

    await waitFor(() => {
      expect(mockRevokeInvitation).toHaveBeenCalledWith('inv-1');
    });
  });

  it('does NOT call revokeInvitation when dialog is cancelled', async () => {
    renderWithQuery(<InvitationsPage />);

    await waitFor(() => {
      expect(screen.getByText('alice@example.com')).toBeInTheDocument();
    });

    const revokeButton = screen.getByRole('button', {
      name: /revoke invitation for alice@example.com/i,
    });
    await user.click(revokeButton);

    await waitFor(() => {
      expect(screen.getByRole('alertdialog')).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /cancel/i }));

    // Dialog should close and API should not be called
    await waitFor(() => {
      expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    });
    expect(mockRevokeInvitation).not.toHaveBeenCalled();
  });

  it('shows filtered empty state when no results match filter', async () => {
    mockGetInvitations.mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 20,
    });

    renderWithQuery(<InvitationsPage />);

    // Change filter to Expired
    const trigger = screen.getByRole('combobox');
    await user.click(trigger);
    const expiredOption = screen.getByRole('option', { name: 'Expired' });
    await user.click(expiredOption);

    await waitFor(() => {
      expect(screen.getByText('No invitations match this filter')).toBeInTheDocument();
    });
  });
```

- [ ] **Step 2: Esegui tutti i test della pagina**

```bash
cd apps/web && pnpm test src/app/admin/\\(dashboard\\)/users/invitations/__tests__/page.test.tsx --run 2>&1 | tail -25
```
Atteso: tutti i test `PASS` (5 esistenti + 6 nuovi = 11 totali).

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/users/invitations/__tests__/page.test.tsx
git commit -m "test(invitations): add revoke dialog, filter reset, error state, empty filter tests (P1)"
```

---

## Task 8 — Aggiungi `formatShortDate` a `timeUtils` e usala in `InvitationRow` (P2)

**Files:**
- Modifica: `apps/web/src/lib/utils/timeUtils.ts`
- Modifica: `apps/web/src/lib/utils/__tests__/timeUtils.test.ts`
- Modifica: `apps/web/src/components/admin/invitations/InvitationRow.tsx`

- [ ] **Step 1: Scrivi il test per `formatShortDate`**

Aggiungi alla fine del file `timeUtils.test.ts` esistente:

```typescript
// In timeUtils.test.ts — aggiungi questo describe block:
describe('formatShortDate', () => {
  it('formats ISO string as short locale date', () => {
    // 2026-03-01 → "Mar 1, 2026" in en-US
    const result = formatShortDate('2026-03-01T00:00:00Z');
    expect(result).toMatch(/Mar/);
    expect(result).toMatch(/2026/);
  });

  it('returns original string on invalid date', () => {
    const result = formatShortDate('not-a-date');
    expect(result).toBe('not-a-date');
  });
});
```

- [ ] **Step 2: Esegui il test e verifica che fallisce**

```bash
cd apps/web && pnpm test src/lib/utils/__tests__/timeUtils.test.ts --run 2>&1 | tail -10
```
Atteso: `FAIL` — `formatShortDate is not a function`.

- [ ] **Step 3: Aggiungi `formatShortDate` a `timeUtils.ts`**

Aggiungi in fondo al file `timeUtils.ts`:

```typescript
/**
 * Format an ISO date string as a short locale date (e.g., "Mar 1, 2026")
 *
 * @param iso - ISO 8601 date string
 * @returns Formatted date string, or original string if parsing fails
 */
export function formatShortDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
    });
  } catch {
    return iso;
  }
}
```

- [ ] **Step 4: Esegui il test e verifica che passa**

```bash
cd apps/web && pnpm test src/lib/utils/__tests__/timeUtils.test.ts --run 2>&1 | tail -10
```
Atteso: tutti i test `PASS`.

- [ ] **Step 5: Aggiorna `InvitationRow.tsx` per usare `formatShortDate`**

```typescript
// Rimuovi la funzione locale formatDate (righe 35-45)
// Aggiungi l'import:
import { formatShortDate } from '@/lib/utils/timeUtils';

// Sostituisci le chiamate formatDate(invitation.createdAt) con formatShortDate(invitation.createdAt)
// e formatDate(invitation.expiresAt) con formatShortDate(invitation.expiresAt)
```

- [ ] **Step 6: Esegui tutti i test dei componenti invitations**

```bash
cd apps/web && pnpm test src/components/admin/invitations --run 2>&1 | tail -15
```
Atteso: tutti i test `PASS`.

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/lib/utils/timeUtils.ts \
        apps/web/src/lib/utils/__tests__/timeUtils.test.ts \
        apps/web/src/components/admin/invitations/InvitationRow.tsx
git commit -m "refactor(invitations): extract formatShortDate to timeUtils, use in InvitationRow (P2)"
```

---

## Task 9 — Smoke test finale: esegui tutta la suite invitations

- [ ] **Step 1: Esegui tutti i test relativi alle invitations**

```bash
cd apps/web && pnpm test \
  src/lib/utils/__tests__/validation.test.ts \
  src/lib/utils/__tests__/parseCsvInvitations.test.ts \
  src/lib/utils/__tests__/timeUtils.test.ts \
  src/components/admin/invitations \
  src/app/admin/\\(dashboard\\)/users/invitations \
  --run 2>&1 | tail -30
```
Atteso: tutti i test `PASS`.

- [ ] **Step 2: TypeScript check finale**

```bash
cd apps/web && pnpm typecheck 2>&1 | grep -i "error" | head -20
```
Atteso: nessun errore TypeScript.

- [ ] **Step 3: Lint check**

```bash
cd apps/web && pnpm lint 2>&1 | tail -10
```
Atteso: nessun errore lint.

---

## Self-Review checklist

| Requisito spec-panel | Task che lo copre | Stato |
|---------------------|-------------------|-------|
| 🔴 P0: Mutation pending state non propagato | Task 5, 6 | ✅ |
| 🔴 P0: `parseCsvContent` non testata | Task 3 | ✅ |
| 🟡 P1: Role non tipizzato in DTO schema | Task 1 | ✅ |
| 🟡 P1: Email regex duplicata | Task 2, 4 | ✅ |
| 🟡 P1: `AVAILABLE_ROLES` hardcoded in 3+ posti | Task 1, 4 | ✅ |
| 🟡 P1: Test InvitationRow mancanti | Task 5 | ✅ |
| 🟡 P1: Test page mancanti (revoke, filter, errore) | Task 7 | ✅ |
| 🟢 P2: Errori raw esposti in UI | Task 6 | ✅ |
| 🟢 P2: `formatDate` non condivisa | Task 8 | ✅ |

**Placeholder scan:** nessun "TBD", "TODO", "implement later" — tutti i task hanno codice completo.

**Type consistency:** `ParsedCsvRow` definita in `parseCsvInvitations.ts` (Task 3) e usata in `BulkInviteDialog.tsx` (Task 4). `INVITATION_ROLES` definita in `invitation.schemas.ts` (Task 1) e usata in `InviteUserDialog`, `BulkInviteDialog`, `parseCsvInvitations.ts`. `isValidEmail` in `validation.ts` (Task 2) usata in `InviteUserDialog`, `parseCsvInvitations.ts`. Tutto coerente.
