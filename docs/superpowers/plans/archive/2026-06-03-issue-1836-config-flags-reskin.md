# #1836 A7 — /admin/config?tab=flags FE re-skin

**Branch**: `feature/issue-1836-config-flags-reskin` (parent `main-dev`)
**Parent epic**: #1833 (F4 Ondata Ops)
**Effort**: ~7-8h FE-only

## Decisioni di scope (2026-06-03)

1. ✅ **DirtyStateBar batch save**: refactor da immediate save (PUT per toggle) a batch save (Apply changes button)
2. ⏸️ **Env-pill**: defer — placeholder statico "prd" su tutti i flag (no BE field). Follow-up issue se serve dev/stg distinction
3. ✅ **Sub-tabs categories** (Features / AI / Integrations / Security) dentro `tab=flags`: filter per category prefix dei flag

## Phase 1 — DirtyStateBar component (~2h)

**File nuovo**: `apps/web/src/components/admin/DirtyStateBar.tsx`

Sticky bottom bar che appare quando ci sono modifiche locali non salvate. Props:
- `dirtyCount: number`
- `onRevert: () => void`
- `onPreview: () => void` (opzionale, può aprire un modal con diff)
- `onApply: () => void`
- `applying: boolean` (loading state durante Apply)

Stile: sticky bottom-0, background warning, backdrop-blur, z-index alto. Conforme tokens canonical (no hardcoded colors).

Test: render condizionale, click handlers, loading state.

## Phase 2 — Local dirty state in FeatureFlagsTab (~3h)

**Modifica**: `apps/web/src/components/admin/FeatureFlagsTab.tsx`

Aggiungi state `Map<flagId, { newValue: string, originalValue: string }>` per tracking modifiche pending.

Refactor `handleToggle`:
- **Prima**: chiama API → onSuccess → refetch
- **Dopo**: aggiorna solo lo state locale → no API call. Quando user clicca "Apply changes" → loop sequenziale di PUT.

Aggiungi:
- `pendingChanges: Map<flagId, PendingChange>` state
- `applyPendingChanges()` → sequential `api.config.updateConfiguration(flagId, {value})` for each, then refetch + clear state
- `revertPendingChanges()` → clear Map, no API
- `previewDiff()` → opzionale, modal showing pending changes (defer se time ridotto)
- Toggle display value = `pendingChanges.has(flagId) ? newValue : flag.value`
- Row visual: `dirty` class se ha pending change

Confirm critical flags al click Apply (non al toggle).

Edge: navigation away with dirty changes → `beforeunload` warning.

## Phase 3 — Static env-pill (~1h)

**File nuovo**: `apps/web/src/components/admin/EnvPill.tsx`

Component statico che mostra "prd" pill (single env per ora, placeholder). Variants `dev | stg | prd`. Per ora hardcode `env="prd"` ovunque finché BE non aggiunge il field.

Inserito accanto al toggle in FlagRow.

## Phase 4 — Sub-tabs categories (~1.5h)

Aggiungi secondo livello tab dentro FeatureFlagsTab:
- Categorie: All · Features · AI · Integrations · Security
- Filter logic: match su `category` field o prefix del `key` (es. `Features.AI.*`, `Features.Integrations.*`)
- Default "All", URL hash `#category=ai`
- Count badge per categoria (es. "AI <span>8</span>")

Possibile fallback se BE non ha categories chiare: split per key prefix.

## Phase 5 — Audit log button reposition (~30min)

ConfigHistoryDialog è già accessibile per-flag. Aggiungere un button "📜 Audit log" globale in `admin-top` o nell'header del panel che apre la dialog senza un flag specifico, mostrando history di tutti i config FeatureFlag.

## Phase 6 — Tests + polish (~1h)

- Unit tests per DirtyStateBar (4-5 scenari)
- Unit tests per refactor FeatureFlagsTab (toggle marca dirty, revert clears, apply batches)
- Update existing tests che assumono immediate save

## Phase 7 — Docs + PR (~30min)

- Archive plan
- PR ready-for-review on main-dev

## Open risks

- **Tier toggles** (Free/Normal/Premium) — esistente, non toccato dal mockup. Lasciato così com'è ma incluso nel dirty state se modificato (richiede check).
- **beforeunload warning** — possibile UX overhead se l'admin usa molto questo tab. Decidere se attivarlo.
- **Critical flag confirm** — il confirm window.confirm attuale al click toggle può essere spostato al click Apply (batch confirm "Stai per disabilitare 2 critical flags, confermi?")

## Verification gates

- Phase 1: `pnpm test DirtyStateBar` verde
- Phase 2: `pnpm test FeatureFlagsTab` aggiornato e verde
- Phase 4: navigation Tab=AI mostra solo flag AI
- Phase 6: full suite verde, no regressions
- E2E (opzionale): Playwright spec `e2e/admin/config-flags-dirty-state.spec.ts`

---

🤖 Plan generato 2026-06-03.
