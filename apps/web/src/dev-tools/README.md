# MeepleDev DevTools (frontend)

Codice attivo SOLO con `NODE_ENV=development` + `NEXT_PUBLIC_MOCK_MODE=true`.
Tree-shaken dal bundle prod via dynamic import + dead-code elimination.

## Architettura

- `mockControlCore.ts`: store Zustand per toggle MSW groups
- `scenarioStore.ts`: store Zustand per dati scenario (stateful CRUD)
- `mockAuthStore.ts`: store Zustand per utente simulato
- `mswHandlerRegistry.ts`: decide quali handler MSW attivare
- `scenarioValidator.ts`: Ajv loader per JSON scenari
- `devBadge.tsx`: componente UI (bottom-right)
- `install.ts`: bootstrap sequence — popola il **scenario bridge** in `@/mocks/scenarioBridge`

## Scenario bridge (issue #366)

Gli handler MSW in `@/mocks/handlers/` non importano da `@/dev-tools/` (per
preservare l'isolation contract — il folder `dev-tools/` è tree-shaken in prod).
Invece `install.ts` chiama `setScenarioBridge(...)` esponendo un adapter con
i metodi `getGames/getSessions/getLibrary/getChatHistory/...`. Gli handler
(games, library, sessions) leggono da `getScenarioBridge()` se presente, con
fallback ai factory hardcoded per test unitari senza dev-tools installati.

**Cosa funziona oggi**: scenario → games, library (derivato owned/wishlist), sessions.

**Limiti noti**:
- I CRUD writes dei handler `sessions/library` mutano un array locale
  (`fallbackSessions`, `fallbackLibrary`) non il bridge: il reload scenario
  resetta le modifiche. Questo matchа il contratto "scenario = source of truth".
- `chat`, `documents`, `game-nights`, `players`, `badges`, `notifications`,
  `shared-games`, `admin` handlers non ancora collegati al bridge.

## Env vars

| Var | Effetto |
|---|---|
| `NEXT_PUBLIC_MOCK_MODE=true` | Attiva MSW + dev-tools (richiede `NODE_ENV=development`) |
| `NEXT_PUBLIC_DEV_SCENARIO={nome}` | Scenario iniziale (default: `empty`) |
| `NEXT_PUBLIC_DEV_AS_ROLE={role}` | Override del role utente (`Guest`/`User`/`Editor`/`Admin`/`SuperAdmin`) |
| `NEXT_PUBLIC_MSW_ENABLE=auth,games` | Abilita solo i gruppi elencati |
| `NEXT_PUBLIC_MSW_DISABLE=admin` | Disabilita i gruppi elencati (precedenza su ENABLE) |
| `?dev-role=Admin` (query string) | Override runtime del role (precedenza su env var) |

## Come aggiungere un nuovo scenario

1. Crea `docs/superpowers/fixtures/scenarios/{nome}.json`
2. Conforme a `scenario.schema.json`
3. `pnpm test __tests__/dev-tools/scenarioValidator.test.ts` deve passare
4. Aggiungi `NEXT_PUBLIC_DEV_SCENARIO={nome}` in `.env.dev.local` per usarlo
