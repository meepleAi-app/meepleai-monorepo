# Spike dell'assistente in partita

Set di scenari ed esecutore della spec `docs/for-developers/specs/2026-10-11-session-assistant-agent-design.md` §7.1.

1. Costruisci l'immagine dell'API **dal checkout da misurare**, con un tag dedicato (il tag di default è condiviso con le altre sessioni):
   `docker build -f apps/api/src/Api/Dockerfile -t meepleai-api:assistant-spike <radice del checkout>`
2. Annota l'immagine in uso, per ripristinarla dopo: `docker inspect meepleai-api --format '{{.Config.Image}}'`.
3. Ricrea **solo** l'API **dal checkout principale**, che ha `infra/secrets/` (un worktree no: il container monterebbe una cartella vuota, e smetterebbe di ripartire quando il worktree viene cancellato), con **entrambi** i file compose (con il solo file base l'API perde gli `env_file` e va in crash-loop):
   `cd <checkout principale>/infra && API_IMAGE=meepleai-api:assistant-spike docker compose -f docker-compose.yml -f compose.dev.yml up -d --no-deps --no-build api`
4. Credenziali di un account admin senza 2FA in `MEEPLE_EMAIL`/`MEEPLE_PASSWORD` (vedi `infra/secrets/admin.secret`; non vanno mai nel repository).
5. `node tests/llm-eval/session-assistant/run-eval.mjs deepseek-flash qwen2.5:3b`
6. Ripristina l'immagine annotata al punto 2: `API_IMAGE=<immagine> docker compose -f docker-compose.yml -f compose.dev.yml up -d --no-deps --no-build api`.

I risultati grezzi vanno in `results/`, che è ignorata da git; l'esito si riassume nella spec. I modelli via OpenRouter richiedono una chiave locale valida (oggi `401`: va risincronizzata con `make secrets-sync`).
