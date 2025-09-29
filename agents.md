# Agents.md — MeepleAI + MeepleAgentAI

## Obiettivi
- Repo monorepo con web (Next.js) + api (.NET) + infra (Compose).
- Automatizzare: issue → branch → test → draft PR.

## Standard
- TypeScript/React per web, .NET 8 Minimal API per backend.
- Ogni feature include test (anche placeholder) e aggiornamento README.
- Prima di aprire PR: esegui `npm test` e `dotnet test`.

## Task Starter
1) **Scansiona repo** e proponi 5 issue con titolo, motivazione, file toccati, test richiesti.
2) **Implementa QA endpoint** se mancano parti (contratto: `{tenantId, gameId, query}` → `{answer, snippets[]}`).
3) **Parsing PDF → RuleSpec**: prepara interfaccia in `RuleSpecService` e test d’esempio.
4) **Rate limit**: integra Redis token bucket middleware nel backend.

## Prompt operativi
- *Genera issue*:
  > Analizza la codebase. Elenca 5 issue prioritarie (descrizione, rischio, payoff, file coinvolti, test).
- *Implementa endpoint*:
  > Aggiungi validazioni e log `request_id`. Rispondi "Not Specified" se il contesto non copre la domanda.
- *Testing*:
  > Esegui `npm test` (web) e `dotnet test` (api). Fissa i test rossi, poi apri **Draft PR** con changelog.

## Done
- Build locale ok con `scripts/dev-up.ps1`.
- Web ↔ API: bottone “Ask QA” funziona (risposta demo).
- Log includono `tenant_id` e `game_id`.
