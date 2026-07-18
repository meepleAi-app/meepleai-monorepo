# Wave #3 — Mobile parity session-live: design

**Data**: 2026-07-18 · **Tracking**: #3150 · **Follow-up di**: #2989 (SP9 mobile GameNight social) · **Gap headline**: #1890 / SP8 gap report
**Stato**: PROPOSED

## Contesto

SP8 (#1890) e il next-steps di #2989 registravano il flow **session-live immersivo** come "non portato a mobile". Una discovery sul codice `main-dev` (2026-07-18) ha però stabilito che **entrambe le superfici mobile esistono già in gran parte** — il lavoro reale è **wiring + polish**, non greenfield:

| Superficie | Realtà su `main-dev` |
|---|---|
| `SessionLiveView` (`/sessions/[id]/live`, shell single-session) | Mobile **attivo** via CSS `lg:hidden` (`DesktopBody` `hidden lg:flex` + `MobileBody` bottom-sheet + FAB). Grezzo: no safe-area, touch-target desktop-scale. |
| `NightLiveHub` (`/game-nights/[id]/live`, orchestratore multi-session) | Mobile **costruito e testato** (`NightLiveHub.test.tsx` → `describe('mobile layout')`: tablist 3-tab Current/Planned/Diary, topbar `compact`) ma **mai attivato** — `NightLiveClientView` non passa mai la prop `mobile`. |
| `MobileBottomBar` su route immersive | Si nasconde già via `isImmersiveRoute` — ma la lista pattern **non include** `/game-nights/[id]/live`. |
| Guard max-1-live su NightLive | Già presente (`BlockedLiveSessionModal` in `NightLiveClientView`). |

Conseguenza: la SP10 design-generation prevista dal next-steps sarebbe **parzialmente ridondante**. Wave #3 è riformulata come **wave di implementazione** in 2 workstream (WS-A ora, WS-B follow-up) + 1 deferral.

## Invarianti dominio rilevanti (session-live)

Da `docs/for-developers/specs/2026-06-04-gamenight-session-domain-model.md` (#10-#15): **#10** max 1 live per GameNight · **#11** 3 timestamp (createdAt/startedAt/completedAt) · **#12** sort session `createdAt ASC` · **#13** draft-warning non-bloccante (toast 6s ambra) · **#14** ora-inizio derivata (no time-picker) · **#15** GameNight `planned→in-progress` alla prima session. WS-B B5 verifica che ciascuna sia resa sulle superfici mobile.

## WS-A — Attiva NightLive mobile (SLICE CORRENTE)

Obiettivo: attivare il layout mobile già esistente di `NightLiveHub` su `/game-nights/[id]/live`, risolvendo lo stacking bottom.

- **A1 — Viewport wiring**. `NightLiveClientView` rileva il viewport con un hook esistente (`useBreakpoint('lg')` da `hooks/useResponsive.ts`, o `useMediaQuery`) e passa `mobile={!isLg}` + `initialMobileTab` a `NightLiveHub`.
  - **SSR-safety**: l'hook deve restituire un default deterministico lato server (desktop) e idratare a mobile lato client senza hydration-mismatch. Preferire il pattern già usato in repo (mounted-guard / `useSyncExternalStore`). Se il rischio mismatch è alto, valutare in alternativa il pattern CSS `lg:hidden` come per `SessionLiveView` (render entrambi, toggle via CSS) — decisione da confermare in fase di plan leggendo l'implementazione dell'hook.
- **A2 — Immersive route**. Aggiungere `/^\/game-nights\/[^/]+\/live(\/|$)/` a `IMMERSIVE_ROUTE_PATTERNS` (`immersive-routes.ts`). Effetto: `MobileBottomBar` si nasconde e `DesktopShell` droppa il padding bottom-bar (già sincronizzati sulla stessa funzione) → niente triplo stack in basso (bottombar globale + tablist hub + CTA).
- **A3 — CTA stacking + safe-area**. Le 3 CTA organizer in `NightLiveClientView` (Avvia prossimo / Completa partita / Concludi serata) sono `fixed inset-x-0 bottom-0 p-4`: su mobile si sovrappongono alla tablist a 3 tab dell'hub. Riposizionarle sopra la tablist (offset bottom) + `env(safe-area-inset-bottom)`.
- **A4 — Test**. Viewport → prop `mobile` passata; `isImmersiveRoute('/game-nights/x/live') === true`; CTA con offset corretto su mobile; nessuna regressione desktop.

### Componenti toccati (WS-A)
- `apps/web/src/app/(authenticated)/game-nights/[id]/live/_components/NightLiveClientView.tsx` (wiring viewport + CTA offset)
- `apps/web/src/components/layout/AppNav/immersive-routes.ts` (pattern)
- Test: `NightLiveClientView.test.tsx`, `immersive-routes.test.ts` (se esiste, altrimenti aggiungere)

### Fuori scope WS-A
`NightLiveHub` mobile UI (già costruito+testato — non ridisegnare). Solo attivazione.

## WS-B — Polish session shell mobile (FOLLOW-UP)

- **B1** safe-area insets su `MobileBody` (session shell) + CTA hub — `pb: max(x, env(safe-area-inset-bottom))`.
- **B2** touch-target ≥44px su FAB, send chat, score spinner (guard SP8 B-02).
- **B3** keyboard-aware scroll per la compose chat (IME).
- **B4** toast guard 409 max-1-live sulla creazione session (`StartSessionSheet`) se mancante.
- **B5** audit invarianti #10-#15 rese sulle superfici mobile; fix gap.

## Deferito (issue follow-up dedicata)

Per-game **flavor mobile UX** (Catan hex pan/zoom, Codenames tap-target, Puerto Rico mat stacking, ecc.) — sub-wave a sé su 7 giochi, fuori da Wave #3.

## Definition of Done (WS-A)

- `NightLiveHub` mobile reso su `/game-nights/[id]/live` a <lg; desktop invariato a lg+.
- `MobileBottomBar` nascosta sulla route (immersive) → no doppio/triplo bottom-nav.
- CTA organizer visibili e non sovrapposte alla tablist, con safe-area.
- Test vitest verdi (nuovi + esistenti), zero regressioni; `pnpm exec eslint` pulito; return type senza `: JSX.Element` esplicito dove crea attrito col typecheck.
- Zero hex/scrim hardcoded nuovi (token semantici / entity utilities).

## Testing

TDD per ogni task: test (vitest) → implementa → verde → eslint → commit. Pattern esistenti: `NightLiveHub.test.tsx` (mobile layout), `NightLiveClientView.test.tsx`.
