# Issue #2955 — Restore per-entity coloring on UI primitives — Design Review

**Data**: 2026-07-16 · **Branch**: `feature/issue-2955-restore-entity-coloring` · **Tipo**: **design review** (l'implementazione segue in una PR successiva, dopo approvazione di questo spec).

## Contesto

La PR FU-2 (`feature/fu2-remove-dead-entity-tokens`) ha rimosso i dead composite entity tokens `--e-<entity>` (undefined a runtime dal #2857 → colori inline resi trasparenti) e, per non shippare componenti visibilmente rotti, ha uniformato **9 primitive** a `bg-primary` / `text-primary-foreground` / `border-primary`. Il per-entity coloring (game=orange, player=purple, session=indigo, agent=amber, kb=teal, chat=blue, event=rose, toolkit=green, tool=cyan) è quindi **perso** su queste primitive.

**Goal**: ripristinarlo usando le utility className **già funzionanti** `bg-entity-*` / `text-entity-*` / `border-entity-*` / `ring-entity-*` (risolvono via `--color-entity-*` → `--c-*` in `@theme inline` di `globals.css`; già usate in `EntityPip`/`EntityTableView`). Mantenere **AA su light + dark** (varianti `-text` per il testo su tinta).

## Sorgente del colore (metodo)

- **Nessun** CSS attribute-selector colora `[data-entity]` / `[data-drawer-accent]` / `[data-step-*]` (grep su tutti i `.css` = 0 match). Il colore vive **interamente** nelle className Tailwind; gli attributi `data-*` sono solo hook per test/QA.
- Le utility `*-entity-*` sono generate da **`@theme inline`** in `apps/web/src/styles/globals.css` (righe 537-548), **non** da `tailwind.config.js` (che ha un commento esplicito, righe 10-13, che demanda a globals).

## Mapping per-primitiva

Tutte espongono già la prop `entity` (tranne dove annotato) — oggi emessa solo come `data-*`, non usata per il colore.

| # | Primitiva (path) | Elemento da ri-colorare | Stato attuale | Utility da applicare | `-text` AA? |
|---|---|---|---|---|---|
| 1 | `ui/btn/btn.tsx` | primary bg (r.36); outline border+text (r.38) | `bg-primary` / `border-border` | `bg-entity-*` (testo bianco); outline `border-entity-*` + testo | **SÌ** (outline) |
| 2 | `ui/drawer/drawer.tsx` | mobile handle (r.106); desktop strip (r.121) | `bg-primary` | `bg-entity-*` | no (decorativo) |
| 3 | `ui/entity-card/entity-card.tsx` | left border (r.40) | **`border-border`** (non primary!) | `border-l-entity-*` | no |
| 4 | `ui/notification-card/notification-card.tsx` | left border (r.52); unread dot (r.77) | border=`border-border`; dot=`bg-primary` | `border-l-entity-*`; `bg-entity-*` | no |
| 5 | `ui/entity-pip/entity-pip.tsx` | active ring (r.46) | ring senza colore (bg **già** entity) | `ring-entity-*` | no |
| 6 | `ui/settings-row/settings-row.tsx` | icon (r.74-78) | `currentColor` (entity mai usato) | `text-entity-*` | **SÌ** se testo |
| 7 | `ui/step-progress/step-progress.tsx` | circle completed/current (r.70-72); connector (r.108); ring | `bg-primary` / `ring-primary` | `bg-entity-*` (testo bianco); `ring-entity-*` | no |
| 8 | `ui/toggle-switch/toggle-switch.tsx` | checked track (r.55); focus ring (r.53) | `bg-primary` / `ring-primary` | `bg-entity-*`; `ring-entity-*` | no |
| 9 | `ui/data-display/entity-list-view/components/entity-table-view.tsx` | row border (r.33-45,234); badge pill (r.104-112) | border=**HSL hardcoded**; badge=`bg-muted` | `border-entity-*`; badge `bg-entity-*/10` + testo | **SÌ** (badge) |

## ⚠️ Gap AA — decisione chiave per la review

Le utility `text-entity-<entity>-text` esistono **solo per `game` e `toolkit`** (`globals.css:539,547`). Mancano per **player, session, agent, kb, chat, event, tool** (7/9).

- Token grezzi `--c-*-text` (in `design-tokens-canonical.css:46-52`): esistono per **session/agent/kb/chat/event**; **mancano del tutto per `player` e `tool`**.
- I punti che richiedono `-text` (testo su tinta): **btn outline**, **entity-table-view badge**, **settings-row icon** (se trattata come testo).

→ Per coprire AA su tutte le 9 entity servono:
1. **Esporre a `@theme inline`** i 5 mapping `--color-entity-{session,agent,kb,chat,event}-text` (i token grezzi esistono già).
2. **Creare + auditare** `--c-player-text` e `--c-tool-text` prima di esporli.

Audit AA esistente (riferimento): tutti i 9 `--c-*` verificati ≥ 4.5:1 su `bg-card` bianco (#807 Iter 2, deliverable `docs/for-developers/frontend/v2-a11y-token-audit.md`); `--c-game-text` 6.14:1, `--c-kb-text` 4.95:1, `--c-toolkit-text` ~5.6:1 (refs #1094, #2862 C5, PR #1721). Nota: `--c-game` a 38% come **testo su cream #f7f3ee** fallisce (4.34:1) → da qui la necessità del `-text` a 32% (`globals.css:576`).

**Regola operativa AA**:
- Fill pieni con testo bianco (btn primary, step circle, toggle track, unread dot) → base `--c-*` OK.
- Bordi/ring (entity-card, notification border, entity-pip ring, connettori) → base OK (≥3:1).
- **Testo colorato su superficie chiara/tinta → richiede `-text`** (btn outline, table badge, settings-row icon).

## Discrepanze vs l'issue (da correggere nell'implementazione)

1. **entity-card** + **notification-card**: il left border oggi è `border-border` (default globale `* { @apply border-border }`), **non** `border-primary` come dice l'issue. Il fix applica `border-l-entity-*`.
2. **entity-table-view**: il row border è **già** per-entity ma via **HSL hardcoded** (`ENTITY_BORDER_COLORS` r.33-45, valori diversi dai canonici `--c-*`) e il badge è `bg-muted` (uniforme). Migrare a `border-entity-*` **scurisce leggermente** il bordo in light (es. agent 50%→32%) — verifica designer.

## Caveat kb → `document`

`entity-tokens.ts:27` mappa `kb → 'document'` e `getEntityToken('kb').bg = 'bg-entity-document'`. Ma `@theme inline` registra `--color-entity-kb`, **non** `--color-entity-document` (che vive solo in `@layer tokens` di `design-tokens.css:358` → in Tailwind v4 **non genera utility**). Quindi `bg-entity-document` / `text-entity-document` **rischiano di non risolvere a runtime**. Per la primitiva kb: usare `bg-entity-kb` (registrato) **oppure** aggiungere `--color-entity-document` a `@theme inline`. Da verificare visivamente.

## Consumer live a rischio (superfici QA)

- **`notifications/page.tsx`** — punto caldo prioritario: `Btn` (filter tabs r.290-300 + CTA drawer r.426-434), `NotificationCard` (lista r.378-386), `Drawer` (dettaglio r.407-413) — tutti per-entity contemporaneamente.
- **Public** (game→primary shift, molte pagine): `pricing-card`, `hero-gradient`, `how-it-works/game-comprehension`, `contact`.
- **Settings**: `AiConsentSection` toggle (`entity="agent"`, amber); `cookie-settings` toggle (default `game`).
- **Admin**: `TopAgentsTable` (`entity="agent"`), `CategoriesTable` (`entity="game"`) via `EntityListView`.
- **Dormanti** (basso rischio visivo, ma test da aggiornare): `entity-card` (nessun consumer live), `step-progress` (primitiva non consumata — il wizard admin usa un `./step-progress` locale), `settings-row` icon (`entity` mai passato dai consumer).

## Piano di implementazione proposto (fasi)

- **Fase 0 — Token AA** (prerequisito): esporre i 5 mapping `-text` mancanti in `@theme inline`; creare + auditare `--c-player-text` / `--c-tool-text`; risolvere kb→document.
- **Fase 1 — Primitive "safe"** (fill/border/ring, nessun `-text`): btn primary, drawer, entity-card, notification-card, entity-pip ring, step-progress, toggle-switch.
- **Fase 2 — Primitive con `-text`** (dopo Fase 0): btn outline, entity-table-view badge, settings-row icon.
- **Fase 3 — Test**: aggiornare `entity-card.test` + `entity-utilities-render`; aggiungere/estendere axe AA sui consumer a rischio (notifications, settings, cookie-settings, admin tables).

## Rischi

- L'**axe AA gate** (blocking) deve restare verde → `-text` obbligatorio su ogni testo-su-tinta prima di mergiare.
- Shift di tonalità `game→primary` su pagine public → verifica visiva designer.
- entity-table-view: scurimento bordo light-mode → verifica.
- `bg-entity-document` non risolvibile → verifica runtime.

## Decisioni aperte per la review

1. **Colori `--c-player-text` / `--c-tool-text`**: quali valori HSL (chi li decide/audita)? Bloccano la Fase 2 per player/tool.
2. **kb→document**: usare `bg-entity-kb` (rapido) o registrare `--color-entity-document` in `@theme` (coerente col mapping)?
3. **Scope PR**: tutte e 9 le primitive in una PR, o Fase 1 (safe) + Fase 2 (AA-gated) separate?
4. **entity-table-view**: accettare lo scurimento del bordo (migrazione ai canonici) o preservare i valori HSL attuali come override entity?
