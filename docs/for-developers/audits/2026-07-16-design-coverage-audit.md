# Audit copertura design — `claude-design-handoff/` + sottocartelle `admin-mockups/`

> **Scopo**: inventariare le fonti di design/mockup **non incluse** nell'audit `2026-07-15-mockup-implementation-gap-audit.md` (che copriva solo `admin-mockups/design_files/`) — cioè `claude-design-handoff/` e le sottocartelle `admin-mockups/{briefs, check, design_handoff, design_handoff_admin, standalone, mockup-meeplecard}` — e mapparle alle route del [site-map](../frontend/site-map.md). Individua copertura design non riflessa, route "morte" ancora descritte da mockup, path divergenti e gap reali.
>
> **Metodo**: workflow a 5 agenti (uno per area) + sintesi. Read-only, nessuna verifica visiva browser: i verdetti "gap"/"partial" si basano sull'assenza di riscontro testuale nel site-map/codice citato. I punti "da verificare" richiedono un controllo puntuale del codice prima di essere trattati come gap confermati. Data: **2026-07-16**.
>
> **Origine**: richiesto dopo aver constatato che l'audit del 2026-07-15 e la ricerca "route senza mockup" si erano fermati a `design_files/` + storie Storybook, saltando `claude-design-handoff/` e le altre sottocartelle.

## Esito netto

La stragrande maggioranza del materiale è **già consumato** dallo sviluppo (i mockup hanno alimentato feature già shippate: Asse A/B/C/D, console admin, play-records) e le route sono già nel site-map. Sono però emersi **~10 gap concreti**, **4 impatti** su fix in corso e **2-3 fonti stale** da non trattare come source-of-truth.

| Fonte | Route reali coperte | Già in site-map |
|---|---|---|
| `claude-design-handoff/2026-06-04` (demo desktop React) | 13 route core | 13/13 sì |
| `claude-design-handoff` SP6 librogame + SP8/SP9 mobile | ~11 route/superfici | 7 sì, 5 partial/no |
| `admin-mockups/design_handoff_admin` (SP5 admin, 31 mockup) | 31 route/tab admin | 24 sì, 5 partial, 2 route morte |
| `admin-mockups/standalone` + `mockup-meeplecard` (MeepleCard + poster) | 3 route dirette | resto component-showcase |
| `admin-mockups/briefs` + `design_handoff` (brief + gap-report) | ~40 route (via SCREENS.md) | in gran parte sì, con staleness |

## Impatti sui fix in corso (questa sessione)

- **Redesign `/setup` (PR #3036)** — ✅ nessun conflitto. Il `setup-wizard`/`setup-chat` di `claude-design-handoff/2026-06-30-sp6` è il setup di un **librogame** (Tab "Setup" nel play, con paragrafo tradotto + checklist), superficie **diversa** dalla route `/setup` generica ("Game Setup Guide" AI). Il redesign resta valido. Overlap concettuale ("genera guida setup") da annotare.
- **Rimozione `sp4-dashboard`** — ✅ corretta. Ma la copertura design dashboard **non sparisce**: `screen-dashboard.jsx` (fonte Asse-C shippato), `dashboard-new-user.html` (empty-state, da classificare a parte), `sp9-dashboard-game-night-mobile.dc.html` (mobile) sono riferimenti **attivi** distinti dal solo HTML Pre-Stage-3 rimosso.
- **Storie chat (pilota)** — `meeple-card-real-app--chat-card.html` + `mobile-card-entity--chat.html` + `chatDesktop.png` (non indicizzati in `MOCKUPS_INDEX.md`) sono rilevanti se le storie toccano il rendering delle entità chat come card.
- **Deprecazione `/private-games`** — ⚠️ `briefs/SP5-admin-tools.md` + `design_handoff_admin/admin/sp5-private-games.html` descrivono una publish-checklist/index che oggi non esiste sulla route reale. La deprecazione li rende moot → nota di riconciliazione necessaria.

## Gap vs site-map (prioritari, da verificare puntualmente)

1. **`/library/[gameId]/play/[campaignId]/translate` — 61.5% drift** (`design_handoff/translate-gap-report.md`): 8/13 stati previsti mancanti (loading skeleton, reader-mode toggle, modal multi-lang, manual-mode, wake-lock, banner low-confidence, badge lang-detection, CTA abort). Possibile debito UX aperto.
2. **`/profile?tab=settings` — #1608 P0**: il tab "settings" potrebbe non essere incluso nel tipo `Tab`, rendendo il wizard 2FA irraggiungibile via UI. Da verificare sul codice corrente.
3. **Route admin morte descritte come vive**: `/admin/agents/sandbox`, `/admin/agents/debug-chat`, `/admin/agents/ab-testing/{[id],new,results}` sono tutte SSR-redirect immediati; i mockup D2/D3 le descrivono come pagine funzionanti.
4. **Slack integration** (`sp5-admin-integrations.html`): zero occorrenze "Slack" nel site-map, metà mockup senza route.
5. **`/admin/secrets`**: nessuna route standalone, `SecretsPanel` embedded in `/admin/monitor/services`.
6. **`/admin/llm/emergency`**: path mockup diverge dal reale `/admin/monitor/operations?tab=emergency`.
7. **RAG index backup/restore** (`sp5-admin-rag-backup.html`): nessuna route dedicata; solo "seeding" coperto.
8. **SP6 librogame**: `glossary-editor`, `quota-credits`, `session-end` (3-way) senza match univoco.
9. **SP9 mobile** (dashboard/game-nights/detail): 3 mockup validati che ridisegnano viste mobile-specifiche non riflesse (touch-target ≥44px, scrim tokenizzato, CTA RSVP 52px).
10. **`meeple-card-drawer-tabs-mockup.html`**: pianificato per conversione standalone ma mai generato (gap di processo).
11. **Duplicazione `tokens.css`/`components.css`** tra `design_files/` (canonico, importato in prod) e `design_handoff/` (copia byte-identica) — rischio drift.

## Fonti stale (NON source-of-truth corrente)

- `design_handoff/SCREENS.md` + `MANIFEST.json` (2026-05-24): elencano ancora `sp4-dashboard`/`sp4-hub-*` come attivi nonostante il ritiro.
- `design_handoff/{CODEBASE_AUDIT,COMPONENTS_AUDIT,SCHEMA_DIFF,REVIEW_REPORT}.md` (2026-05-24): superati dallo stato corrente.
- `design_handoff_admin/ADMIN_AUDIT.md` (2026-05-24): ~2 mesi più vecchio del site-map, verificare prima di fidarsi.

## Path divergenti mockup ↔ implementazione

| Route reale | Path nel mockup | Fonte |
|---|---|---|
| `/admin/monitor/operations?tab=emergency` | `/admin/llm/emergency` | `sp5-admin-emergency.html` |
| `/admin/monitor/services` (SecretsPanel embedded) | `/admin/secrets` | `sp5-admin-secrets.html` |
| `/library/[gameId]` (variante libro) | `/gamebook` | brief `SP6-libro-game.md` |
