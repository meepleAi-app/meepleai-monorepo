# Config History & Rollback UI — Design Spec

**Goal:** Add per-configuration history view and single-level rollback to the admin config hub, closing the 2 UI gaps identified in the spec panel review.

**Approach:** Inline dialog per config row (Approach A). No new backend endpoints needed — uses existing `getHistory()` and `rollback()` API client methods.

---

## Architecture

### Component: `ConfigHistoryDialog`

A reusable modal dialog triggered from any configuration table row. Shows a vertical timeline of changes and allows single-level rollback.

**Props:**
```typescript
interface ConfigHistoryDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  configId: string;          // UUID of the configuration
  configKey: string;         // Display key (e.g., "Features:PdfUpload")
  onRollbackComplete: () => void; // Callback to refresh parent table
}
```

### UI Layout

```
┌─────────────────────────────────────────────┐
│  Cronologia: Features:PdfUpload        [X]  │
├─────────────────────────────────────────────┤
│                                             │
│  ● v3 — 2026-03-19 10:30                   │
│  │  "false" → "true"                       │
│  │  Configuration updated                   │
│  │  [Rollback]                             │
│  │                                         │
│  ● v2 — 2026-03-18 14:00                   │
│  │  "true" → "false"                       │
│  │  Configuration updated                   │
│  │                                         │
│  ● v1 — 2026-03-01 09:00                   │
│     "" → "true"                            │
│     Configuration created                   │
│                                             │
├─────────────────────────────────────────────┤
│                              [Chiudi]       │
└─────────────────────────────────────────────┘
```

### Rollback Flow

1. User clicks "Rollback" button on the most recent non-creation entry
2. `window.confirm("Ripristinare il valore precedente '{oldValue}'?")`
3. If confirmed: `POST /admin/configurations/{id}/rollback/{version}`
4. On success: `toast.success("Configurazione ripristinata")`, call `onRollbackComplete()`, close dialog
5. On error: `toast.error(message)`, dialog stays open

### Backend Constraints

- **Single-level only**: `SystemConfiguration.Rollback()` swaps `Value` ↔ `PreviousValue`
- **TargetVersion ignored**: The handler doesn't use the version parameter (simplified impl)
- **History limited**: Handler returns at most 2 entries (last change + creation) from `PreviousValue` field, not a full audit table
- **Rollback unavailable when**: No `previousValue` exists (creation-only configs)

---

## Integration Points

### FeatureFlagsTab.tsx

Add a "History" column with a clock icon button per row. The button opens `ConfigHistoryDialog`.

**Changes:**
- Add `<th>` column "History" after the Status column
- Add `<td>` with `<button data-testid="btn-history-{flag.id}">` per row
- Import and render `ConfigHistoryDialog` with state management for `selectedConfigId`

### Future: GeneralTab / Other Config Tables

The `ConfigHistoryDialog` component is standalone and reusable. Any table with `SystemConfigurationDto` rows can add a History button that opens this dialog.

---

## Data-testid Attributes

| Element | data-testid |
|---------|-------------|
| History button in row | `btn-history-{configId}` |
| Dialog container | `config-history-dialog` |
| Timeline entry | `history-entry-{version}` |
| Rollback button | `btn-rollback` |
| Close button | `btn-close-history` |

---

## API Endpoints Used

| Method | Endpoint | Response |
|--------|----------|----------|
| GET | `/api/v1/admin/configurations/{id}/history?limit=20` | `ConfigurationHistoryDto[]` |
| POST | `/api/v1/admin/configurations/{id}/rollback/{version}` | `SystemConfigurationDto` |

### ConfigurationHistoryDto shape
```typescript
{
  id: string,
  configurationId: string,
  key: string,
  oldValue: string,
  newValue: string,
  version: number,
  changedAt: string (ISO datetime),
  changedByUserId: string,
  changeReason: string,
}
```

---

## Browser Test: `admin-config-history.spec.ts`

### CH-01: View configuration history
- Mock `getHistory` returning 2 entries (v2 update + v1 creation)
- Click History button on a flag row
- Assert dialog opens with correct title
- Assert both timeline entries visible with correct values

### CH-02: Rollback to previous value
- Mock `getHistory` returning entry with `oldValue`
- Mock `rollback` endpoint returning updated config
- Click History → Click Rollback → Accept confirm dialog
- Assert rollback API called
- Assert success toast "Configurazione ripristinata"
- Assert dialog closes

### CH-03: Rollback unavailable for creation-only config
- Mock `getHistory` returning only 1 entry (creation, no previous value)
- Click History
- Assert no Rollback button visible

---

## Files

| File | Action |
|------|--------|
| `apps/web/src/components/admin/ConfigHistoryDialog.tsx` | **Create** |
| `apps/web/src/components/admin/FeatureFlagsTab.tsx` | **Modify** — add History column + dialog |
| `apps/web/e2e/admin/admin-config-history.spec.ts` | **Create** — 3 browser tests |
