# Spec — Verificare in locale ciò che andrà su Azure: profilo `azure-parity`

**Data**: 2026-10-15 · **Stato**: proposta, nata da una revisione a pannello (`/sc:spec-panel`, modalità critica, focus architettura e test) · **Epic**: [#4419](https://github.com/meepleAi-app/meepleai-monorepo/issues/4419)
**Linea di lavoro**: worktree `D:/Repositories/meepleai-monorepo-azure`, branch d'integrazione `azure/main`; feature `feature/azure-<issue>-<desc>` con PR verso `azure/main` (memoria `azure-line-worktree-and-emulators`).
**Parte da**: spec [`2026-10-09-azure-local-profile-design.md`](./2026-10-09-azure-local-profile-design.md) (prima iterazione, emulatori) · ricerca [`2026-10-08-azure-ai-200-provider-switch-research.md`](../research/2026-10-08-azure-ai-200-provider-switch-research.md) (§5 vincoli, §8 esercizi, §9 decisioni aperte).
**Convenzione**: nessun conteggio volatile in prosa; dove serve una misura c'è il comando che la produce. I riferimenti `file:riga` valgono per `feature/azure-1-local-profile-spec` al momento della stesura: dopo il riallineamento della fase 0 vanno riletti.

---

## 1. Problema

L'utente vuole **provare l'app in locale prima di un deploy su Azure**, usando servizi AI esterni (API gestite: LLM e, dove possibile, embedding) invece di far girare tutto in container.

La domanda contiene due requisiti distinti, e solo il primo è verificabile oggi:

| | Requisito | Stato |
|---|---|---|
| **R1** | L'app funziona in locale nel profilo `azure-local`, con LLM esterno: upload di un PDF, indicizzazione, risposta in chat con citazioni | **non soddisfatto**: senza embedding il RAG è fermo (§3, P1) |
| **R2** | Ciò che girerà su Container Apps parte e si comporta come in locale | **non verificabile**: il contratto locale (immagine, variabili, dipendenze) non coincide con quello del Bicep (§3, P3) |

Un test locale verde vale come prova per R2 solo se l'API locale parte con **le stesse variabili e gli stessi segreti** che il Bicep le consegna. Oggi le due liste divergono e nessun controllo lo segnala.

## 2. Stato di partenza

La prima iterazione `azure-local` (spec del 2026-10-09) è implementata sul branch `feature/azure-1-local-profile-spec`: Azurite + provider `azureblob`, App Configuration e Key Vault come sorgenti `IConfiguration`, `PdfUploaded` su Service Bus con consumer Azure Functions, export OTLP verso l'Aspire Dashboard, `infra/azure/**` validato da `bicep build` in CI.

Quel lavoro **non è integrato né pubblicato**, e diverge da `main-dev`. Misure, da rieseguire prima di agire:

```bash
cd D:/Repositories/meepleai-monorepo-azure
git log --oneline azure/main..feature/azure-1-local-profile-spec | wc -l   # commit non integrati in azure/main
git ls-remote origin 'refs/heads/azure/*'                                   # vuoto = azure/main non è su GitHub
git fetch origin && git log --oneline HEAD..origin/main-dev | wc -l          # commit di main-dev mancanti
```

Fra i commit mancanti c'è il refactor dell'agente unico (ADR-095): provare l'app su questo branch significa provare un prodotto che su `main-dev` non esiste più.

## 3. Difetti trovati

Gravità: 🔴 blocca R1 o R2 · 🟡 blocca il deploy ma non la verifica locale.

| # | | Difetto | Evidenza | Verificabile in locale |
|---|---|---|---|---|
| P1 | 🔴 | `make dev-azure-local` non attiva il profilo compose `ai`, quindi `embedding-service` non parte; l'API lo cerca comunque su `http://embedding-service:8000`. Indicizzazione e RAG falliscono. | `infra/Makefile` (`AZURE_LOCAL_COMPOSE`, target `dev-azure-local`), `infra/compose.dev.yml` (`Embedding__Provider: External`, `Embedding__LocalServiceUrl`) | sì |
| P2 | 🔴 | Nessun provider di embedding **esterno** è compatibile con lo spazio vettoriale: la colonna è `vector(768)`; OpenRouter restituisce 1536 (`text-embedding-3-small`) o 3072 e la richiesta non invia `dimensions`, quindi la validazione scarta il risultato; HuggingFace BGE-M3 è 1024. In più lo snapshot è indicizzato con e5-base (le query vanno codificate con lo stesso modello) e i pesi di `FuseGlobally` sono tarati su e5 (#3737). | `PgVectorEmbeddingEntityConfiguration.cs` (`vector(768)`), `OpenRouterEmbeddingProvider.cs` (record `OpenRouterEmbeddingRequest`, `ValidateEmbeddingDimensions`) | sì |
| P3 | 🔴 | Il Bicep non consegna all'API ciò che le serve per partire. Il container `api` riceve solo variabili Azure: mancano Postgres (`CONNECTIONSTRINGS__POSTGRES`), Redis, JWT, chiavi LLM, configurazione dell'embedding, `ASPNETCORE_ENVIRONMENT`. Il modulo Key Vault crea un vault **vuoto**, e nessun modulo assegna ruoli all'identità dell'API (`grep -rn roleAssignments infra/azure` → nessun risultato), quindi anche un segreto inserito a mano risponderebbe 403. `postgres.bicep` espone solo `serverFqdn`. `bicep build` valida la sintassi, non l'avvio. | `infra/azure/modules/containerapps.bicep` (env dell'app `api`), `keyvault.bicep`, `postgres.bicep`, `main.bicep` | sì, con il profilo di §5 |
| P4 | 🔴 | Le chiavi DataProtection vanno su file system (`PersistKeysToFileSystem`); su Container Apps il disco è effimero e `maxReplicas: 2`: cookie e antiforgery smettono di valere fra repliche e dopo ogni riavvio o scale-to-zero. | `AuthenticationServiceExtensions.cs` (blocco `DataProtection:KeysPath`), `containerapps.bicep` (`scale`) | sì, con Azurite |
| P5 | 🔴 | L'origine dell'API è fissata nel bundle del web: `NEXT_PUBLIC_API_BASE` è un `ARG` di build con default `http://localhost:8080` e viene letto da codice client; il Bicep imposta solo `API_BASE_URL`, che serve al proxy lato server. Su Azure il browser chiamerebbe `localhost`. Su staging il problema non esiste perché Caddy mette `/api/*` e il web sulla **stessa origine**; su Container Apps web e API hanno due FQDN distinti, e SignalR (`getHubBase`) non passa dal proxy di Next. Anche la CSP si fissa alla build (memoria `csp-two-sources-build-time`). | `apps/web/Dockerfile` (`ARG`/`ENV NEXT_PUBLIC_API_BASE`), `grep -rln NEXT_PUBLIC_API_BASE apps/web/src --include=*.ts --include=*.tsx \| grep -v __tests__`, `apps/web/src/app/api/v1/[...path]/route.ts`, `infra/Caddyfile` | sì |
| P6 | 🟡 | Redis si costruisce da `REDIS_HOST`/`REDIS_PORT`/`REDIS_PASSWORD` senza TLS, in tre punti distinti. Azure Managed Redis (`redis.bicep`: `clientProtocol: 'Encrypted'`, porta 10000) richiede TLS. | `InfrastructureServiceExtensions.cs` (`AddCachingServices`), `ObservabilityServiceExtensions.cs`, `Routing/AdminKBSettingsEndpoints.cs` | sì, con Redis in TLS |
| P7 | 🟡 | Il Bicep passa `APPLICATIONINSIGHTS_CONNECTION_STRING` ma nessun codice la legge (`grep -rn APPLICATIONINSIGHTS_CONNECTION_STRING apps/api/src apps/web/src` → nessun risultato): Application Insights resterebbe vuoto. L'export OTLP va solo dove punta `OTEL_EXPORTER_OTLP_ENDPOINT`. | grep | in parte |
| P8 | 🟡 | Con `STORAGE_PROVIDER=azureblob`, cover, seed, backup e outbox dello storage restano su `IAmazonS3`. Debito già dichiarato. | spec 2026-10-09 §4.1 | sì |
| P9 | 🟡 | SignalR non ha backplane, ma l'API può scalare a 2 repliche: le sessioni live si spezzano. | ricerca §5 #9, `containerapps.bicep` | no |
| P10 | 🟡 | La pipeline pubblica solo immagini `linux/arm64`; Container Apps accetta solo `linux/amd64`. In locale non pesa: Docker è amd64 e lo stack locale costruisce già l'API con lo stesso Dockerfile. | ricerca §5 #1, `apps/api/src/Api/Dockerfile` | non serve |
| P11 | 🟡 | LLM: in locale la chiave OpenRouter è rifiutata (401) dal 2026-07-14 (memoria `openrouter-local-key-rejected`), quindi l'unico LLM esterno osservabile è DeepSeek. Azure OpenAI non ha client (`LlmProvider`: `OpenRouter`, `Ollama`; DeepSeek ha `DeepSeekLlmClient`) e il free trial Azure ha quota 0 sui modelli. | `LlmProvider.cs`, memoria | sì |

**Verificato e non difettoso**: senza i servizi Python l'estrazione PDF degrada ma non si ferma. `EnhancedPdfProcessingOrchestrator` prova Unstructured (soglia 0,80), poi SmolDocling (0,70), poi Docnet come best effort. Su Azure senza `unstructured`/`smoldocling` la qualità scende al livello Docnet. Il costo in latenza dei due tentativi falliti per upload **non è misurato**: va misurato prima di decidere se disattivarli per profilo.

## 4. Decisioni

| Domanda | Scelta | Motivo |
|---|---|---|
| Embedding nella fase 1 | **(a) e5-base resta un container** (`embedding-service`), avviato anche da `make dev-azure-local`; LLM esterno (DeepSeek; OpenRouter quando la chiave torna valida) | lo snapshot resta valido e la fusione di #3737 non va ritarata; R1 si chiude senza toccare il RAG. Proposta dal pannello, confermata dall'utente il 2026-10-15 |
| Embedding gestito (b) | **rimandato**: `text-embedding-3-small` ridotto a 768 con `dimensions` diventa un esercizio a sé, insieme allo spazio vettoriale per profilo (ricerca §8, esercizio 2) | richiede reindicizzazione completa e ritaratura misurata con l'artifact `rag-fusion-tuning`; resta da verificare che OpenRouter inoltri `dimensions` |
| Reranker nel profilo | spento come oggi (`ResilientRetrieval:EnableReranking=false` in `appsettings.json`): `make dev-azure-local` avvia solo `embedding-service`, non `reranker-service` | il reranker è un'immagine pesante che oggi nessun percorso usa |
| Dove vivono i segreti su Azure | Key Vault, letto dall'API come sorgente `IConfiguration` già implementata (mapping `--` → `:`, `-` → `_`), con ruolo `Key Vault Secrets User` all'identità dell'API | è il percorso già scritto; i riferimenti `secretRef` di Container Apps restano un'alternativa da non mescolare |

## 5. Design: profilo `azure-parity`

Un override `infra/compose.azure-parity.yml`, composto sopra `azure-local` **solo** da target del Makefile (`make dev-azure-parity`, `make dev-azure-parity-down`), che rende l'API locale indistinguibile, per contratto, dall'app `api` del Bicep:

1. **Variabili**: l'API riceve esclusivamente le variabili dichiarate nell'`env` dell'app `api` in `containerapps.bicep`, con valori che puntano agli emulatori. Nessun `env_file` da `infra/secrets/`.
2. **Segreti**: arrivano dal Key Vault emulator (opt-in già esistente, `make dev-azure-local-keyvault`), seminato con gli stessi nomi che il Bicep o il runbook di deploy metteranno nel Key Vault vero.
3. **Gate di parità**: uno script confronta i nomi delle variabili dell'app `api` nel Bicep con quelli dell'override e fallisce se divergono; gira nel workflow `azure-line.yml`. È questo controllo, non la buona volontà, a tenere R2 vero nel tempo.
4. **Web**: costruito con l'API su un'origine diversa dalla propria, come avverrà su Container Apps (salvo la decisione di P5).
5. **Seconda replica**: un container `api-replica` con la stessa immagine e le stesse variabili (memoria `measure-on-a-second-api-container`), per lo scenario delle chiavi condivise. `docker compose --scale` non è utilizzabile perché il servizio ha `container_name` fisso.

### Scenari di accettazione

| # | Dato | Quando | Allora | Copre |
|---|---|---|---|---|
| S1 | `make dev-azure-parity`, API con le sole variabili del Bicep e i segreti dal Key Vault emulator | l'API si avvia | `curl -fsS http://localhost:8080/api/v1/health` → 200 | P3 |
| S2 | `make dev-azure-local` (embedding e5 attivo, LLM DeepSeek) | un admin carica un PDF dalla UI e lo indicizza | lo stato arriva a `Ready` e una domanda in chat su quel gioco risponde con citazioni | P1, P11 |
| S3 | due repliche (`api`, `api-replica`) con le chiavi DataProtection su Azurite | login sulla prima, richiesta autenticata alla seconda | 200, nessun errore di decrittazione del cookie nel log | P4 |
| S4 | web costruito con l'API su un'origine diversa | si naviga dashboard, chat e una sessione live | il pannello Network del browser non mostra richieste a `localhost:8080`; l'hub SignalR si connette | P5 |
| S5 | Redis che accetta solo TLS | l'API usa cache, kill switch e lock distribuiti | nessun errore di connessione Redis nel log; `/api/v1/health` non degradato per Redis | P6 |

## 6. Fasi e issue

| Fase | Contenuto | Issue | Dipende da |
|---|---|---|---|
| 0 | Integrare la prima iterazione in `azure/main`, riallinearla a `main-dev`, pubblicare `azure/main` | #4420 | — |
| 1 | `make dev-azure-local` avvia `embedding-service`; S2 con DeepSeek | #4421 | 0 |
| 2a | Chiavi DataProtection su Blob Storage (Azurite in locale); S3 | #4422 | 0 |
| 2b | Redis da connection string con TLS, costruito in un solo punto; S5 | #4423 | 0 |
| 2c | Un'origine sola per web, API e hub su Azure; nessuna origine fissata nel bundle; S4 | #4424 | 0 |
| 2d | Bicep: contratto completo dell'API, Key Vault seminato, ruoli, app `embedding`, `maxReplicas: 1` | #4425 | 0 |
| 2e | Profilo `azure-parity`, gate di parità in `azure-line.yml`; S1 | #4426 | 2a, 2b, 2d |

**Fuori perimetro** (fase 3–4, non ancora in issue): immagini `linux/amd64` su ACR (P10), export verso Application Insights (P7), backplane SignalR (P9), debito `IAmazonS3` (P8, già da tracciare per la spec del 2026-10-09), scelta della sottoscrizione (ricerca §9 #1), embedding gestito (decisione (b)).

## 7. Domande aperte

1. **P5, un'origine sola su Container Apps**: path routing dell'environment Container Apps, Front Door, oppure dominio personalizzato con API su sottodominio e cookie di dominio. Da verificare se `azurecontainerapps.io` è nella Public Suffix List: in quel caso i due FQDN di default sono cross-site e i cookie di sessione non viaggiano.
2. **P6, autenticazione di Managed Redis**: verificare se le access key sono abilitate di default nella versione API usata da `redis.bicep` o se serve Entra (`Microsoft.Azure.StackExchangeRedis`).
3. **S1, migrazioni all'avvio**: `Program.cs` esegue `MigrateAsync` all'avvio; con più repliche che partono insieme va verificato il comportamento del lock delle migrazioni di EF Core 9 con Npgsql prima di alzare le repliche.
4. **Latenza del fallback PDF** (§3, «verificato e non difettoso»): misurare quanto costano i tentativi falliti verso Unstructured e SmolDocling quando i servizi mancano.
