# Mock Mode Removal — Design Spec

**Date**: 2026-04-25
**Status**: ✅ COMPLETED — frontend removed (PR #555), Makefile dev-fast targets + scripts removed (verified absent 2026-05-10), backend DevTools intentionally retained per scope decision
**Author**: Claude (subagent-driven controller)
**Decision authority**: User confirmed scope on 2026-04-25

---

## Goal

Eliminare definitivamente la modalità "mock mode" del frontend Next.js (UI mock, MSW, Dev Panel) e il fast-dev loop infrastrutturale che la abilita, mantenendo i mock backend (.NET) per i test integration.

## Sintomo che si chiude

Visitando `http://localhost:3000` con `NEXT_PUBLIC_MOCK_MODE=true`, il middleware `apps/web/src/proxy.ts:340-355` forza `isAuthenticated=true` e legge `NEXT_PUBLIC_DEV_AS_ROLE=admin` come fallback role; di conseguenza la guard `apps/web/src/proxy.ts:405-407` redirige `/` → `/admin`. Rimuovendo il branch MOCK_MODE, l'autenticazione torna a essere guidata solo dalla session cookie reale; il redirect a `/admin` resta legittimo per admin autenticati.

## Scope confermato (deciso 2026-04-25)

| Decisione | Valore | Conseguenza |
|-----------|--------|-------------|
| 1. Scope | Scope C ridotto | UI mock + dev-fast loop infrastruttura. Backend DevTools resta. |
| 2. Fast-dev loop | Rinunciato | `make dev-fast*` (5 target), `infra/scripts/dev-fast*.sh`, `infra/.env.dev.local*` cancellati |
| 3. Mock backend | Mantenuti | `apps/api/src/Api/DevTools/` invariato (già isolato in Debug via `#if DEBUG` + `<Compile Remove Release>`) |
| 4. CI workflow `dev-tools-isolation.yml` | Cancellato | Già ridondante dopo rimozione frontend; backend protetto per costruzione |

## Architettura post-rimozione

```
apps/web/
├── src/
│   ├── mocks/             ❌ DELETE (24 files, MSW handlers)
│   ├── dev-tools/         ❌ DELETE (37 files, Dev Panel)
│   ├── app/
│   │   ├── mock-provider.tsx     ❌ DELETE
│   │   └── providers.tsx          🔧 EDIT (drop conditional require)
│   ├── proxy.ts                   🔧 EDIT (remove MOCK_MODE branches)
│   ├── __tests__/mocks/           ✅ KEEP (vitest server, indipendente)
│   └── ...
├── __tests__/
│   ├── dev-tools/         ❌ DELETE (14 test files)
│   └── integration/dev-tools/   ❌ DELETE (3 test files)
├── public/
│   └── mockServiceWorker.js  ❌ DELETE
├── package.json           🔧 EDIT (rm dep `msw`, rm script `dev:mock`)
└── bundle-size-baseline.json  🔧 UPDATE (rimisura post-build)

infra/
├── Makefile                🔧 EDIT (rm 5 target dev-fast*)
├── scripts/dev-fast*.sh    ❌ DELETE
└── .env.dev.local*         ❌ DELETE

apps/api/src/Api/DevTools/  ✅ UNCHANGED (resta, già isolato)
apps/api/tests/Api.Tests/DevTools/  ✅ UNCHANGED
.github/workflows/dev-tools-isolation.yml  ❌ DELETE
```

## Surgical edit reference

### `apps/web/src/proxy.ts`
Rimuovere 3 branch MOCK_MODE (le righe esatte saranno determinate al momento dell'edit):
- riga ~117: short-circuit per CSRF/login bypass in mock mode
- righe ~340-355: forzaggio `isAuthenticated=true` + lettura `NEXT_PUBLIC_DEV_AS_ROLE`
- nessuna modifica alle righe 405-407 (redirect / → /admin) né alle righe 418-421 (Alpha mode logic)

Risultato atteso: `process.env.NEXT_PUBLIC_MOCK_MODE` e `process.env.NEXT_PUBLIC_DEV_AS_ROLE` non compaiono più nel file.

### `apps/web/src/app/providers.tsx`
Rimuovere il blocco `require('./mock-provider')` condizionale (~righe 148-153) e il wrapping condizionale dell'app (~righe 178-180). Il provider tree resta invariato senza il branch.

### `apps/web/src/app/mock-provider.tsx`
Cancellazione completa.

### `apps/web/package.json`
- `dependencies`/`devDependencies`: rimuovere `msw` (^2.12.10)
- `scripts`: rimuovere `dev:mock`

### `infra/Makefile`
Rimuovere target: `dev-fast`, `dev-fast-api`, `dev-fast-full`, `dev-fast-down`, `dev-fast-check`. Aggiornare `.PHONY` (riga ~332). Aggiornare comment header di sezione.

## Sequenza di esecuzione (8 step Fowler)

Implementazione single-PR atomica, ogni step termina con `pnpm typecheck && pnpm lint` (frontend) o equivalente per essere committable indipendentemente.

| # | Step | Verifica gate |
|---|------|--------------|
| 1 | Edit `proxy.ts` (rm MOCK_MODE branch) + smoke test localhost:3000 senza redirect | `pnpm test src/proxy.test.ts && manual: curl localhost:3000 returns 200 not redirect` |
| 2 | Edit `mock-provider.tsx` delete + `providers.tsx` rm conditional | `pnpm typecheck` |
| 3 | `rm -rf src/mocks src/dev-tools public/mockServiceWorker.js` | `pnpm typecheck && pnpm test` (deve fallire SOLO i 17 test in `__tests__/{dev-tools,integration/dev-tools}`) |
| 4 | `rm -rf __tests__/dev-tools __tests__/integration/dev-tools` | `pnpm test` passa interamente |
| 5 | `pnpm remove msw` + rm script `dev:mock` da package.json | `pnpm install && pnpm build` |
| 6 | `rm -rf infra/scripts/dev-fast*.sh infra/.env.dev.local*` + edit `infra/Makefile` | `make help` non elenca dev-fast |
| 7 | `rm .github/workflows/dev-tools-isolation.yml` + aggiorna sezione MSW in: ADR-022, ADR-042, `docs/for-developers/frontend/storybook-guide.md`, `docs/for-developers/testing/performance-testing-guide.md`, `docs/vitest-migration-guide.md` | grep verifica nessun riferimento residuo |
| 8 | `pnpm build` → misura `du -sb .next/static/chunks` → aggiorna `bundle-size-baseline.json` (atteso drop ~80-120KB) | bundle test passa con nuova baseline |

## Test verification matrix

```bash
# Frontend
cd apps/web
pnpm install                     # rebuilds lockfile senza msw
pnpm typecheck                   # 0 errori
pnpm lint                        # 0 errori
pnpm test                        # tutti i test passano (i 17 dev-tools sono cancellati con i loro target)
pnpm build                       # build pulito, bundle baseline ricalcolata

# Backend (sanity — non dovrebbe essere toccato)
cd ../api/src/Api
dotnet build -c Release          # passa (DevTools già escluso)
dotnet build -c Debug            # passa (DevTools incluso)
dotnet test                      # tests DevTools passano (mock services intatti)

# Stack reale
cd ../../../../infra
make dev                         # stack si avvia, web punta su api reale
curl http://localhost:3000       # 200 (landing), no redirect a /admin senza session
```

## Risks & mitigations

| Risk | Severity | Mitigation |
|------|----------|------------|
| Dev locali con `.env.local` contenente `NEXT_PUBLIC_MOCK_MODE=true` continuano a leggere env var dopo il merge → noop, non rotture | LOW | Il branch che lo legge è rimosso; env var orfana è inerte |
| Test E2E Playwright dipendenti da `/dev/toggles` endpoint | LOW | Backend DevTools resta + endpoint resta in Debug; non impattati. Verificato: nessun e2e in `apps/web/e2e/` cita mock mode |
| Bundle baseline non aggiornata = bundle-size CI fallisce | MED | Step 8 esplicito; expected drop documentato |
| `make dev-fast*` ricordato a memoria team | LOW | Annotare in commit message + session memory |
| Rollback necessario | LOW | Tag `pre-mock-removal` su `main-dev` prima del merge |
| Pre-existing flaky test `scenarioSwitchIntegration.test.tsx` (segnato in memoria 2026-04-12) | INFO | Cancellato come parte dello scope, beneficio collaterale |

## Documentation impact

**Update (rimuovere sezioni MSW/mock):**
- `docs/for-claude/architecture/adr/adr-022-ssr-auth-protection.md` (rimuove riferimento "MSW per E2E")
- `docs/for-claude/architecture/adr/adr-042-dashboard-performance.md` (rimuove "MSW mocks in integration tests")
- `docs/for-developers/frontend/storybook-guide.md` (rimuove sezione `msw-storybook-addon`)
- `docs/for-developers/testing/performance-testing-guide.md` (rimuove setup MSW)
- `docs/vitest-migration-guide.md` (rimuove menzioni MSW non già necessarie per `src/__tests__/mocks/`)

**Archive (lasciare in `archived/` come storia):**
- `docs/for-developers/plans/archived/2026-04-02-frontend-mock-mode.md`
- `docs/for-developers/plans/archived/2026-04-09-meepledev-phase1-plan.md`
- `docs/for-developers/plans/archived/2026-04-10-meepledev-phase2-plan.md`
- `docs/for-developers/specs/2026-04-02-frontend-mock-mode-design.md`
- `docs/for-developers/specs/2026-04-09-meepledev-fast-dev-loop-design.md`
- `docs/for-developers/specs/2026-04-10-meepledev-phase2-dev-panel-design.md`

**Add — final ADR:**
- `docs/for-claude/architecture/adr/adr-XXX-mock-mode-removal.md` documenta rationale, data, file impattati, lessons learned

## Non-goals

- Rimuovere `apps/api/src/Api/DevTools/` o i mock backend services (`MockLlmService`, `MockBggApiService`, etc.)
- Rimuovere i test backend `apps/api/tests/Api.Tests/DevTools/`
- ~~Toccare logica Alpha mode (`NEXT_PUBLIC_ALPHA_MODE`) — sistema separato~~ *(nota retroattiva: ALPHA_MODE rimosso in PR #949, 2026-05-10)*
- Toccare `src/__tests__/mocks/` (vitest test infrastructure indipendente)
- ~~Toccare `make alpha`, `make dev`, `make dev-core`~~ → solo `make dev` / `make dev-core` rimangono (target `alpha*` rimossi in PR #949)

## Acceptance criteria

- [ ] `grep -r "NEXT_PUBLIC_MOCK_MODE\|NEXT_PUBLIC_DEV_AS_ROLE" apps/web/src` ritorna 0 risultati
- [ ] `find apps/web/src/mocks apps/web/src/dev-tools` non esiste (cartelle assenti)
- [ ] `pnpm test && pnpm build` pulito post-rimozione
- [ ] `make dev` (stack reale) avvia `localhost:3000` senza redirect a `/admin`
- [ ] `bundle-size-baseline.json` aggiornata, drop misurabile
- [ ] `dotnet build -c Release` continua a passare (regression sanity)
- [ ] PR mergiata in `main-dev`; tag `pre-mock-removal` esiste come restore point
- [ ] ADR di chiusura redatto e committato
