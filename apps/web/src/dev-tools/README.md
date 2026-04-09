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
- `install.ts`: bootstrap sequence

## Come aggiungere un nuovo scenario

1. Crea `docs/superpowers/fixtures/scenarios/{nome}.json`
2. Conforme a `scenario.schema.json`
3. `pnpm test __tests__/dev-tools/scenarioValidator.test.ts` deve passare
4. Aggiungi `NEXT_PUBLIC_DEV_SCENARIO={nome}` in `.env.dev.local` per usarlo
