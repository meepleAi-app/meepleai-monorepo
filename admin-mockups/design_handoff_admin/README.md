# Handoff Claude Code — MeepleAI Admin Console (SP5)

## Cosa contiene questo pacchetto

Una guida operativa per integrare i **18 mockup admin** in `design/admin/` nel tuo codebase MeepleAI esistente, usando Claude Code in VSCode.

I file sono HTML standalone (no React inline) con dati realistici. Replicano look-and-feel finale ma il task è ricreare quei design nel codebase reale come componenti React tipizzati, riusando i componenti già in produzione (`ConnectionBar`, `EntityChip`, `Drawer`, `Tabs`...) e cablando il backend reale + autorizzazioni RBAC.

## File in questo pacchetto

| File | A cosa serve |
|---|---|
| `README.md` | Questo file — overview + workflow |
| `QUICK_START.md` | Setup iniziale per Admin Console (RBAC, layout shell, gates) |
| `WIRING_GUIDE.md` | Come tradurre un mock admin in componente reale + RBAC + audit log |
| `BACKEND_PROMPTS.md` | 18 prompt pronti — uno per ogni mockup admin |
| `SCREENS.md` | Inventario 18 schermate + endpoint + permessi + priorità |
| `RBAC.md` | Modello permessi: user/admin/superadmin · audit log · impersonate |
| `admin-base.css` + `admin-nav.js` | Copie pronte per importare |

## Workflow consigliato

### 1. Apri il progetto + lancia Claude Code

```bash
cd /percorso/al/tuo/progetto-meepleai
code .
claude
```

### 2. Primo prompt (incolla esattamente)

```
Devo integrare nel codebase un set di 18 mockup admin in `design/admin/`.
È SEPARATO dal demo utente — questa è la console di amministrazione
per ruoli admin + superadmin (Aaron).

Leggi in ordine:
- design_handoff_admin/README.md
- design_handoff_admin/QUICK_START.md
- design_handoff_admin/RBAC.md
- design_handoff_admin/SCREENS.md
- design_handoff_admin/WIRING_GUIDE.md

Poi dimmi:
1. Esiste già una sezione admin nel codebase? Se sì, dove e quali pagine.
2. Quale modello RBAC c'è oggi? È compatibile con `user`/`admin`/`superadmin`?
3. Esiste già `AdminDataTable`, `KPISparkline`, `LiveEventLog`? Path?
4. Quale endpoint admin esistono lato BE? Quali mancano?
5. Quale schermata proponi come pilota?

NON modificare codice ancora. Voglio prima il report scritto in
design_handoff_admin/CODEBASE_AUDIT.md.
```

### 3. Procedi schermata per schermata

```
Implementiamo SP5 A1 Overview.
Usa il prompt 1.1 da BACKEND_PROMPTS.md.
Mostrami i diff prima di applicare.
```

## Regole d'oro per Claude Code (admin-specific)

1. **Tutte le pagine /admin/* richiedono ruolo `admin` o `superadmin`**. Il gate va al livello di route + API + UI. Triple lock.

2. **Tutte le mutazioni admin scrivono un audit_log entry** con: `actor`, `action`, `target`, `before`, `after`, `ip`, `ua`, `timestamp`. È non-negoziabile.

3. **Density alta**: `--s-2`/`--s-3` spacing, `--fs-sm` body, tabelle con righe 32-40px. NON applicare la density del prodotto utente.

4. **Dark mode preferito** in admin (vedi `admin-base.css`). Light mode è secondario.

5. **Mobile fallback** per /admin/*: messaggio "console solo desktop". NON ottimizzare per mobile. Eccezione: `/admin/overview` e `/admin/monitor` possono avere read-only mobile view.

6. **Keyboard shortcuts obbligatori**: `⌘K` search globale, `g+u` go to Users, `g+a` go to AI, `R` refresh. Implementa anche `Escape` per chiudere drawer/modal e `j/k` per navigare righe tabelle.

7. **NO action sensibile senza conferma**. Delete/Ban/Suspend/Force-logout vanno SEMPRE dietro un `<ConfirmModal>` con typed-confirm per superadmin actions (es. typing "DELETE USER MARCO" per cancellare un utente).

8. **Impersonate** è la più pericolosa: blocca con 2FA step-up, banner persistente con "TU SEI MARCO · termina impersonate", audit ogni request, max 30 min poi auto-end.

9. **Token only**: usa solo CSS vars da `admin-base.css`. NO hex hardcoded. Mantieni le 9 entity colors per le badge/chip cross-entity.

10. **Performance**: tabelle admin possono mostrare 10k+ row. Implementa virtualization (react-window/TanStack Virtual), cursor pagination, server-side filter+sort. NO client-side filtering su >500 row.

## Componenti v2 nuovi da estrarre (~38)

Ognuno va creato come componente isolato in `src/components/admin/v2/`:

- **Layout**: `AdminShell`, `AdminSidebar`, `AdminTopbar`, `MobileFallback`
- **Data display**: `AdminDataTable`, `BulkActionsBar`, `AdminKPICard`, `KPISparkline`, `AdminPanel`, `AdminTabs`
- **Status**: `StatusChip`, `RoleChip`, `EnvPill`, `StatusDot`
- **Forms**: `AdminFormRow`, `AdminInput`, `AdminSelect`, `AdminToggle`, `AdminTextarea`
- **Specialized**:
  - `QueryDrillDown` + `RetrievalChunkList` + `LatencyBreakdownBar` (A4)
  - `KBTree` + `DocumentViewer` + `ChunkTable` + `IngestionLog` (A5)
  - `UploadDropzone` + `ProcessingTimeline` + `KbIdempotencyBanner` (A5b/B4)
  - `SyncStatusHero` + `SyncRunTimeline` (A6)
  - `FlagRow` + `DirtyStateBar` (A7)
  - `LiveEventLog` + `TimeRangePicker` (A8)
  - `TemplateEditor` + `TemplatePreviewFrame` + `VariableHelper` (A9)
  - `BlockEditor` + `PreviewFrame` + `VersionHistoryPanel` (B1)
  - `FlowCanvas` + `NodePalette` + `NodeConfigPanel` (B2)
  - `VersionTimeline` + `VersionDiffViewer` (B6)
  - `PublishChecklist` + `PrivateGameCard` (B7)
  - `ScenarioPicker` + `AuthRoleSwitcher` + `StoreInspector` (B8, dev-only)
- **Modals**: `ConfirmModal`, `DangerZoneBox`, `AuditLogTimeline`

## Note specifiche admin

**SP5 B8 Dev Tools è DEV-ONLY**: deve essere tree-shaken in prod bundle. Wrap tutto in `if (process.env.NODE_ENV === 'development')` + entry-point separato.

**SP5 B7 Private Games**: i giochi privati DEVONO avere flag `is_private=true` lato DB. Endpoint `/api/public/*` NON deve mai esporli, anche con typo URL. Test esplicito.

**SP5 A2 Users · Impersonate**: il backend deve avere endpoint `POST /api/admin/users/{id}/impersonate-start` che restituisce un JWT short-lived (15 min) con claim `impersonated_by`. Ogni API call con questo token logga BOTH `actor` (admin) AND `impersonated_user_id`.

---

Vedi `WIRING_GUIDE.md`, `BACKEND_PROMPTS.md`, `SCREENS.md` per il dettaglio operativo.
