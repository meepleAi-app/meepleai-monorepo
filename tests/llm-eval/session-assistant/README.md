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

Le chiamate passano dal gateway con la quota giornaliera delle valutazioni (`GET /api/v1/admin/llm/budget/usage` la mostra): quando finisce, ogni scenario esce con `AI_BUDGET_DAILY_EXHAUSTED` fino al giorno dopo. Per iterare su un prompt esegui solo gli scenari che falliscono, più volte, con il prompt in un file:

`node tests/llm-eval/session-assistant/run-eval.mjs deepseek-flash --only=en-turn,it-score-dimension-unknown --repeat=6 --prompts=tests/llm-eval/session-assistant/prompts/v4.json --label=prova`

`--rules=off` (#4270) non offre `cerca_regole` a nessuno scenario, e lascia fuori quelli che lo aspettano (il file dei risultati li elenca in `skippedForRules`). Dalla fetta A5 (#4411) il turno offre `cerca_regole` quando la sessione ha un gioco, quindi si misura **senza** `--rules=off`: serve solo a rifare i confronti con `prompts/v3.json`, il testo pubblicato prima di A5, che dice che il regolamento non si consulta. Il predefinito del codice è `prompts/v4.json`; `v1`, `v2` e `v3` sono le versioni precedenti.

`--prompts` manda il prompt nella richiesta (`{ "italian": "...", "english": "..." }`, una lingua basta): non serve ricostruire l'immagine, purché l'immagine conosca il campo (da #4256 in poi). Un'immagine più vecchia lo scarterebbe in silenzio: l'API restituisce l'impronta del prompt che ha usato, lo script la confronta con ciò che ha mandato e si ferma al primo turno se non coincidono. L'impronta resta nel file dei risultati. Senza, vale il prompt del codice, che un test tiene uguale a `prompts/v4.json`. Quando un prompt supera il set intero tre volte su tre, diventa il nuovo file misurato e il nuovo predefinito. I modelli via OpenRouter richiedono una chiave locale valida (oggi `401`: va risincronizzata con `make secrets-sync`).
