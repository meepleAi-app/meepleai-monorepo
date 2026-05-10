# MeepleAI v2 deploy goals — design (process-focused redesign)

**Status**: 🟢 ACTIVE reference — supersedes a1-promote-main-dev-staging plan

> **Date**: 2026-05-07
> **Owner**: @DegrassiAaron (single dev / decision authority)
> **Supersedes**: `docs/superpowers/plans/2026-05-07-a1-promote-main-dev-staging.md` (plan basato su assunzioni invalidate)
> **Companion plan**: TBD — questo design viene tradotto in implementation plan via `superpowers:writing-plans` skill

## Context

### Goal di sessione

Definire goal SMART trackable per portare la nuova UI v2 (Wave A/B/C/D + SP6, ~16 rotte già `done` su `main-dev`) a essere visibile e funzionale su `https://meepleai.app` (staging che funge da unico ambiente pubblico), con qualità accettabile su accessibility e performance.

### Findings dalla sessione di discovery (2026-05-07)

Iterazione precedente di brainstorming aveva prodotto 5 goal outcome-focused (A1, A2, B1, B2, B3) che assumevano:

- pipeline `deploy-staging.yml` automatica e affidabile
- produzione separata su `meepleai.com`
- baseline performance pre-Wave-A misurabile
- smoke verificabile esternamente via `curl https://meepleai.app/health`

Investigation diretta ha invalidato tutte e quattro le assunzioni:

1. **Pipeline staging in stato ambiguo**: `deploy-staging.yml` gate (riga 89-95) richiede `head_branch=main-staging` ma run "success" recenti mostrano `head_branch=main-dev` → gate skippato silentemente, success vacuo. Deploy reali eseguiti manualmente via `workflow_dispatch` (alcuni recenti FAIL: `c9e997c77`, `38c08d693`).
2. **CI policy permissiva**: PR mergiate con `CI Success = FAILURE` via `--admin` override (es. #819, #823 nel set Wave 3). Promote automatico porterebbe red su main-staging.
3. **Cloudflare Access wrappa `meepleai.app/*` incluso `/health`**: smoke esterni ritornano 302 (redirect login). Service token CF Access esistente (lavoro precedente). GitHub IP whitelist non confermato.
4. **`meepleai.com` (produzione) inesistente**: no record DNS, no server provisionato. Goal di "production deploy" out-of-scope finché stand-up infrastructure non viene affrontato come progetto separato.
5. **Nessuna baseline performance pre-Wave-A**: codice mai rilasciato in produzione, quindi "no regression vs legacy" è ill-definito.

### Decisioni di scoping

- **Approccio**: `β` da brainstorm — process focus + outcome semplificato (3 macro-goal invece di 5)
- **EAA 2026-06-01**: IGNORE — non più hard deadline; goal owner-paced
- **Produzione `meepleai.com`**: out of scope per questo design; eventuale stand-up tracked separatamente
- **P1 scope**: minimal viable — solo quanto serve per sbloccare P2

## Goal P1 — Pipeline staging trustworthy (minimal viable)

| SMART | Detail |
|---|---|
| **S**pecific | `deploy-staging.yml` esegue end-to-end su trigger deterministico (push a `main-staging` post-merge release PR da `main-dev`, o `workflow_dispatch` come escape hatch): build/migrate/deploy/validate completano success reale (no jobs vacuamente skippati); smoke test job verifica HTTP 200 reale (non 302 da CF Access redirect) usando service token; runbook documenta strategia smoke + promote model |
| **M**easurable | 3 deploy "Deploy to Staging" consecutivi (push reali a main-staging O `gh workflow run` manuali surrogati) con conclusion=success E nessun job critico (Build Images, Database Migration, Deploy to Staging, Post-deploy Validation) in stato `skipped`; job `validate.smoke_tests` ritorna HTTP 200 da `/health`; documento `docs/for-developers/operations/deploy-staging-runbook.md` esiste e include sezione CF Access bypass + promote PR procedure |
| **A**chievable | Solo workflow YAML edits + 1 doc + uso service token già disponibile. Nessun infra provisioning. Single dev fattibile in ~1 settimana |
| **R**elevant | Pre-requisito atomico per P2: deploy a `meepleai.app` non può essere "trusted" senza pipeline trusted |
| **T**ime-bound | **~1 settimana** owner-paced |

### Gherkin scenarios

```gherkin
Feature: deploy-staging.yml fires reliably on main-dev push

  Scenario: Promote main-dev → main-staging triggers automatic staging deploy end-to-end
    Given un release PR "main-dev → main-staging" viene mergiato (push a main-staging)
    When GitHub Actions trigger "push: branches: [main-staging]" del workflow "Deploy to Staging" fires
    Then il job "CI Quality Gate" passa (observability-only, sempre success)
    And i job [build, migrate-db, snapshot-baseline, deploy, validate, e2e-staging] eseguono effettivamente (status≠skipped)
    And la conclusion finale del run "Deploy to Staging" è "success"
    And la latenza totale push-to-deploy è ≤ 60 minuti

  Scenario: Smoke test job verifica HTTP 200 reale dietro Cloudflare Access
    Given deploy-staging.yml job "validate" è in esecuzione
    And i secrets CF_ACCESS_CLIENT_ID + CF_ACCESS_CLIENT_SECRET sono popolati nel repo
    When eseguo "smoke_test API Health" contro https://meepleai.app/health con CF Access service token headers
    Then il response status è 200 (non 302 da Cloudflare Access redirect)
    And il body JSON contiene {"status": "Healthy"}

  Scenario: Tre deploy consecutivi registrati green (manuali accettati come surrogato)
    Given P1 è in fase di acceptance verification
    When ispeziono "gh run list --workflow 'Deploy to Staging' --limit 3 --json conclusion,event,headBranch"
    Then i 3 entry più recenti hanno tutti event in [push, workflow_dispatch] AND conclusion=success
    And nessuno dei 3 ha "skipped" come stato finale di alcun job critico (Build Images, Database Migration, Deploy to Staging, Post-deploy Validation)
    And tutti e 3 referenziano commit reali su main-staging

  Scenario: Runbook documenta strategia smoke test + promote model
    Given un nuovo developer/operator vuole verificare salute staging post-deploy
    When apre "docs/for-developers/operations/deploy-staging-runbook.md"
    Then trova sezione "Smoke testing meepleai.app dietro CF Access"
    And documenta: come usare service token esistente, come interpretare 200 vs 302
    And trova sezione "Promote main-dev → main-staging"
    And documenta: procedura release PR + watch deploy completion
    And documenta: rollback manuale via revert PR
```

## Goal P2 — v2 visibile + accessibile su meepleai.app (consolida A1+A2+B1+B2)

| SMART | Detail |
|---|---|
| **S**pecific | 16+ rotte v2 (Wave A/B/C/D + SP6) live su `meepleai.app`; codice legacy lungo critical-path Alpha rimosso/corretto in favore dei v2 component; 0 axe-core color-contrast violations sulle rotte v2; keyboard nav funzionante (verifica automated via axe + role check); #807 Fase 1 (audit) + Fase 2 (token redesign AA) deployati nello **stesso PR** atomico; #808 freeze policy lifted |
| **M**easurable | Critical-path Alpha 9-step (login → /library → /games?tab=library → /games/[id] → /agents → /agents/[id] → /sessions → /sessions/[id] → /gamebook) renderizza componenti v2 nel DOM (`data-slot` v2-* presente) senza fallback legacy; `pnpm test:e2e:a11y` ritorna 0 violations su tutte le rotte v2; A11y E2E job CI con `continue-on-error: false`; #807 e #808 issue closed |
| **A**chievable | Dipende da P1. #807 audit + token redesign è il lavoro più sostanzioso (~2-3 settimane single dev). Cleanup legacy critical-path è scope addizionale rispetto al goal originale |
| **R**elevant | Output user-visible primario — è "la nuova UI" promessa; sblocca anche Wave 3 frontend pending tramite freeze lift |
| **T**ime-bound | **~3-4 settimane** dopo P1 (owner-paced, no EAA pressure) |

### Gherkin scenarios

```gherkin
Feature: v2 routes deployed AND accessible on meepleai.app

  Scenario: Critical-path Alpha user vede 16+ rotte v2 live, no fallback legacy
    Given P1 è done (pipeline trustworthy) e ultimo deploy main-dev è success
    And sono utente Alpha autenticato su meepleai.app (post CF Access + login app)
    When percorro: /login → /library → /games?tab=library → /games/[id] → /agents → /agents/[id] → /sessions → /sessions/[id] → /gamebook
    Then ogni rotta renderizza componenti v2 (DOM contiene "data-slot" che inizia con "v2-" oppure markup da components/v2/**)
    And nessuna rotta cade in fallback legacy (data-slot v1-* assente) — eventuali deviazioni sono tracked come issue + documentate
    And la console browser non mostra errori critici (severity=error)

  Scenario: axe-core 0 color-contrast violations su tutte le 16 rotte v2
    Given P1 è done e il workflow "Frontend - A11y E2E" gira contro meepleai.app post-deploy
    When eseguo "pnpm test:e2e:a11y" su staging (o equivalente CI job)
    Then il report axe-core ritorna 0 violations di rule "color-contrast"
    And tutti i testi normali hanno ratio computato ≥ 4.5:1 (WCAG 2.1 AA SC 1.4.3)
    And il job CI "Frontend - A11y E2E" è impostato a continue-on-error=false (blocking)
    And nessuna PR successiva può passare merge gate con violazioni nuove

  Scenario: #807 token redesign + visual baseline regen + freeze lift atomic in same PR
    Given audit token ha catalogato N violazioni (#807 Fase 1)
    When committo Fase 1 (catalogo audit CSV) + Fase 2 (token redesign AA in tailwind.config + tokens.css) + visual baselines rigenerati nello STESSO PR
    Then "pnpm test:e2e:a11y" passa su tutte le rotte v2 senza .exclude(...) workaround
    And visual baseline rigenerati committati senza diff inattesi (>0.5%) sui componenti già done
    And #807 issue closed con summary che include catalogo audit + diff token
    And #808 freeze policy issue closed con commento "lift criteria met"
    And v2-migration-matrix permette nuovi pending row pickable

  Scenario: Keyboard navigation funzionante su drawer + dialog v2 (automated check)
    Given sono su https://meepleai.app/sessions/[id]/live (E2E test scenario)
    And il test driver simula solo keyboard input (no mouse)
    When premo Tab finché focus arriva su trigger button "Apri menu"
    And premo Enter
    Then drawer si apre AND focus si sposta su primo elemento focusabile interno
    And premendo Escape drawer si chiude AND focus torna al trigger button
    And axe-core verifica role="dialog" + aria-modal="true" + aria-labelledby presenti
```

## Goal P3 — Performance v2 meets absolute Web Vitals "Good" thresholds

> **Re-framing rispetto al draft originale (B3)**: il goal originale era "no regression vs legacy baseline". Senza baseline (mai rilasciato in produzione) il confronto è ill-definito. Il goal viene re-framed con **threshold assoluti** Web Vitals "Good" + first-deploy v2 come baseline per future PR.

| SMART | Detail |
|---|---|
| **S**pecific | Aggiornare `apps/web/lighthouserc.json` + `lighthouserc.mobile.json` per coprire critical-path Alpha (5 rotte sample); tutte le rotte rispettano threshold assoluti Web Vitals "Good" (LCP ≤ 2.5s, CLS ≤ 0.1, TBT ≤ 200ms); bundle aggregato JS first-load ≤ 20 MB; first-deploy v2 produce baseline JSON committato per future regression detection |
| **M**easurable | Lighthouse CI run post-P2 ottiene Performance ≥ 90 su 5 rotte [/library, /games, /agents, /sessions, /gamebook] in entrambi profili desktop + mobile; `Frontend - Bundle Size` job verifica ≤ 20 MB JS first-load aggregato e nessun chunk > 500 KB gzipped; file `docs/for-developers/frontend/perf-baselines/baseline-v2-<DATE>.json` committato; il job Bundle Size è blocking (no continue-on-error) post-baseline establishment |
| **A**chievable | Tooling già esistente (Lighthouse CI workflow + lighthouserc + Bundle Size CI). Solo URL list update + budget assertion + baseline file commit. ~2-3 giorni single dev |
| **R**elevant | Impedisce regressioni perf future + stabilisce contract observable per rotte v2 |
| **T**ime-bound | **~1 settimana** post-P2 |

### Gherkin scenarios

```gherkin
Feature: Performance v2 meets absolute Web Vitals "Good" thresholds

  Scenario: Lighthouse CI URL list aggiornato al critical-path Alpha
    Given P2 è completato e Lighthouse CI gira su PR via "test-performance.yml"
    When ispeziono "apps/web/lighthouserc.json" + "apps/web/lighthouserc.mobile.json"
    Then la lista URL include le 5 rotte critical-path Alpha [/library, /games, /agents, /sessions, /gamebook]
    And rimuove URL legacy non più rilevanti per v2 (es. /editor se obsoleto)
    And il file è committato come parte del PR P3

  Scenario: Tutte le rotte Alpha rispettano Web Vitals "Good" thresholds (absolute)
    Given Lighthouse CI run su 5 rotte critical-path Alpha post-build localhost (artefatto = stesso bundle deployato)
    When ispeziono il report dell'ultimo run su PR
    Then ogni rotta ottiene LCP ≤ 2.5s ("Good")
    And ogni rotta ottiene CLS ≤ 0.1 ("Good")
    And ogni rotta ottiene TBT ≤ 200ms ("Good" — surrogato di FID per Lighthouse synthetic)
    And score Performance ≥ 90 su ogni rotta
    And nessun threshold richiede confronto con baseline storico (assoluto)

  Scenario: Bundle aggregato sotto budget hard 20 MB
    Given il job CI "Frontend - Bundle Size" gira su main-dev post-P2
    When ispeziono il report bundle-size dell'ultimo run
    Then bundle JS first-load aggregato ≤ 20 MB
    And nessun chunk individuale eccede 500 KB gzipped
    And il job è blocking (continue-on-error=false) per future PR

  Scenario: First-deploy v2 produce baseline che future PR confrontano
    Given P3 acceptance run completa con tutti threshold rispettati
    When committo il report Lighthouse + bundle-size come "baseline-v2-2026-XX-XX.json"
    Then il file è salvato in "docs/for-developers/frontend/perf-baselines/"
    And future PR che peggiorano metriche di > 10% vs questa baseline triggherano warning su PR comment
    And la baseline è ri-aggiornata solo via PR esplicita (no auto-update silente)
```

## Dependency graph

```
P1 (~1 settimana)
  ↓ blocking
P2 (~3-4 settimane)
  ↓ blocking
P3 (~1 settimana)

Total owner-paced: ~5-6 settimane
```

P1 deve essere `done` prima che P2 cleanup deploy abbia senso (deploy va eseguito da pipeline trustworthy, non da workflow_dispatch manuali ad hoc). P2 deve essere `done` prima che P3 misuri perf su rotte v2 effettivamente live.

## Out of scope (esplicitamente)

- **Production stand-up** (`meepleai.com` DNS + server + secrets bootstrap) — progetto separato, requires brainstorm dedicato + plan infrastruttura
- **Real User Monitoring** (RUM) per Web Vitals reali p75 da utenti — non disponibile, P3 usa Lighthouse synthetic come surrogato
- **Wave 3 frontend rotte rimanenti** (`/discover`, `/game-nights`, `/kb/[id]`, `/toolkits/[id]`) — sbloccate da P2 (#808 lift) ma non parte di questo set di goal; tracked in v2-migration-matrix
- **SP5 admin batch** + **SP3 public secondary** — fuori scope
- **CI policy hardening** (no più `--admin` merge con CI Success rosso) — out of scope P1 minimal; potenziale follow-up se P1 done expone necessity

## Riferimenti

- v2 migration matrix: `docs/for-developers/frontend/v2-migration-matrix.md`
- Plan A1 (invalidato): `docs/superpowers/plans/2026-05-07-a1-promote-main-dev-staging.md`
- Workflow staging: `.github/workflows/deploy-staging.yml`
- Workflow performance: `.github/workflows/test-performance.yml`
- Lighthouse config: `apps/web/lighthouserc.json` + `lighthouserc.mobile.json`
- Issue #807 — A11y design system audit
- Issue #808 — Freeze SP6 v2 expansion (policy)

## Self-review checklist

- ✅ **Placeholder scan**: nessun TBD/TODO non risolto. "TBD" appare solo nel header come riferimento al companion plan da generare via writing-plans
- ✅ **Internal consistency**: dipendenze P1→P2→P3 coerenti tra sezioni; threshold assoluti P3 non confliggono con altri goal; baseline storage path consistente in P3
- ✅ **Scope check**: 3 macro-goal, ognuno SMART discreto, owner-paced timeline, decomposizione adeguata per single dev
- ✅ **Ambiguity check**: critical-path 9-step esplicito; threshold Web Vitals "Good" valori esatti; bundle 20 MB esplicito; "atomic same PR" per #807 redesign chiarito
