# Spike dell'assistente in partita

Set di scenari ed esecutore della spec `docs/for-developers/specs/2026-10-11-session-assistant-agent-design.md` §7.1.

1. Costruisci l'immagine dell'API **dal checkout da misurare**, con un tag dedicato (il tag di default è condiviso con le altre sessioni):
   `docker build -f apps/api/src/Api/Dockerfile -t meepleai-api:assistant-spike <radice del checkout>`
2. Annota l'immagine in uso, per ripristinarla dopo: `docker inspect meepleai-api --format '{{.Config.Image}}'`.
3. Ricrea **solo** l'API **dal checkout principale**, che ha `infra/secrets/` (un worktree no: il container monterebbe una cartella vuota, e smetterebbe di ripartire quando il worktree viene cancellato), con **entrambi** i file compose (con il solo file base l'API perde gli `env_file` e va in crash-loop):
   `cd <checkout principale>/infra && API_IMAGE=meepleai-api:assistant-spike docker compose -f docker-compose.yml -f compose.dev.yml up -d --no-deps --no-build api`
4. Credenziali di un account admin senza 2FA in `MEEPLE_EMAIL`/`MEEPLE_PASSWORD` (vedi `infra/secrets/admin.secret`; non vanno mai nel repository).
5. Ogni modello da misurare deve stare nel catalogo (`GET /api/v1/admin/ai-models`), anche come riga inattiva: il gateway rifiuta un modello senza prezzo. Una riga inattiva entra nel listino e non nel routing; a fine misura si toglie.
6. `node tests/llm-eval/session-assistant/run-eval.mjs deepseek-flash qwen2.5:3b`
7. Ripristina l'immagine annotata al punto 2: `API_IMAGE=<immagine> docker compose -f docker-compose.yml -f compose.dev.yml up -d --no-deps --no-build api`.

I risultati grezzi vanno in `results/`, che è ignorata da git, e il file di un modello si sovrascrive a ogni run dello stesso giorno: copialo prima di rilanciare. L'esito si riassume nella spec, e i run citati dalla spec si copiano in `baselines/<data>/`.

Le chiamate passano dal gateway con la quota giornaliera delle valutazioni (`GET /api/v1/admin/llm/budget/usage` la mostra): quando finisce, ogni scenario esce con `AI_BUDGET_DAILY_EXHAUSTED` fino al giorno dopo. Per iterare su un prompt esegui solo gli scenari che falliscono. I modelli via OpenRouter richiedono una chiave locale valida (oggi `401`: va risincronizzata con `make secrets-sync`).
