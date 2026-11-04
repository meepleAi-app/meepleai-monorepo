# Note esterne "Azure-ready" — testi raccolti dall'utente, verificati contro il repo

**Data**: 2026-10-08 · **Tipo**: note (materiale esterno, generato da un assistente AI, incollato dall'utente) · **Stato**: verificato riga per riga contro il codice; **non** è una decisione.
Si legge insieme a [`2026-10-08-azure-ai-200-provider-switch-research.md`](./2026-10-08-azure-ai-200-provider-switch-research.md), che resta il riferimento per esame, prezzi e vincoli.

Convenzione: ogni sezione riporta il testo ricevuto (condensato dove è prosa, integrale dove è codice) e poi un riquadro **Verifica contro il repo** con tre esiti: ✅ già presente · ⚠️ impreciso o datato · 💡 da tenere. I path sono relativi a `apps/api/src/Api/` salvo nota.

---

## 1. Mappatura locale (gratuito) ↔ Azure e aree prioritarie

### Testo ricevuto (condensato)

Mappa componenti:

| Componente | Locale | Azure | Pattern da adottare subito |
|---|---|---|---|
| Frontend | Next.js 16 (Docker) | Static Web Apps o App Service | `NEXT_PUBLIC_API_URL` disaccoppiata dal dominio |
| Backend API | .NET 9 Minimal API | Container Apps (scale-to-zero) | CQRS/MediatR con interfacce astratte per log e storage |
| Servizi AI Python | Docker (FastAPI, SmolDocling, reranker) | Container Apps o Azure OpenAI | client REST astratti per RAG ed embedding |
| LLM | Ollama / LM Studio / OpenRouter free | Azure OpenAI / Foundry | `Microsoft.Extensions.AI` o Semantic Kernel |
| DB + vettori | PostgreSQL 16 + pgvector | Flexible Server | EF Core + pgvector, query indipendenti dall'hosting |
| Cache | Redis | Azure Cache for Redis | `IDistributedCache` / StackExchange.Redis |
| Blob | MinIO / Localstack | Azure Blob | `IStorageService` o SDK S3/Blob |

Aree prioritarie: (A) astrazione LLM/embedding con `Microsoft.Extensions.AI` (`AddChatClient(new OllamaChatClient(...))`, poi `AzureOpenAIChatClient`); (B) storage via MinIO + `IFileStorageService` (`UploadAsync`, `GetDownloadUrlAsync`); (C) Dockerfile leggeri `python:3.11-slim`, `/health`, profilo compose unico; (D) `appsettings` + User Secrets, log JSON su console (stdout) perché Container Apps li raccoglie da solo verso Log Analytics. Compose consigliato: `pgvector/pgvector:pg16`, `redis:7-alpine`, `minio/minio`, `ollama/ollama`. Prossimi passi: verificare le interfacce storage e LLM; FinOps (Static Web Apps free, 180.000 vCPU-s di Container Apps, Postgres Burstable).

### Verifica contro il repo

| Punto | Esito | Evidenza |
|---|---|---|
| MinIO nel compose | ✅ | `infra/docker-compose.yml:670` (`minio`, profilo `storage`) + `minio-init` `:747` |
| Ollama nel compose | ✅ | `infra/docker-compose.yml:133` + `ollama-pull` `:157` (profilo `ai`) |
| `IStorageService` con upload/download URL | ✅ ma **bypassata** | `Services/Pdf/IBlobStorageService.cs` + `BlobStorageServiceFactory.cs:19-30` (`STORAGE_PROVIDER` = `local`/`s3`); le pipeline cover, il seed reader, il backup RAG, l'outbox storage e la migrazione inversa iniettano `IAmazonS3` direttamente (elenco in §4 del report principale) |
| "Salvataggio diretto su file system del container" come punto critico | ⚠️ | falso: il provider locale è una scelta esplicita della factory, S3/R2 è in uso su staging |
| Dockerfile `python:3.11-slim` + `/health` | ✅ in parte | embedding e reranker: `python:3.11-slim` multi-stage; `/health` in `apps/{embedding,reranker,orchestration}-service/main.py`. **SmolDocling** usa `nvidia/cuda:12.6.3-cudnn-runtime-ubuntu22.04` (`apps/smoldocling-service/Dockerfile:36`), non slim |
| Profilo compose unico | ⚠️ | il repo usa già profili multipli (`ai`, `ai-essential`, `storage`, `monitoring`, `automation`) e i target `make dev*`; non vanno appiattiti |
| `appsettings` + User Secrets | ⚠️ | i secret vivono in `infra/secrets/*.secret` con `make secrets-sync` da staging; `Api.csproj` non ha `UserSecretsId`. Il pattern esistente è deliberato (staging è la sorgente); non introdurre un terzo meccanismo |
| Serilog JSON su console | ⚠️ parziale | sink Console, File e Seq presenti (`Api.csproj:115-117`), configurazione in `LoggingConfiguration.ConfigureSerilog` (`Program.cs:114`). Da verificare il formatter del sink Console: per Container Apps serve `CompactJsonFormatter` (o equivalente) sullo stdout, e Seq spento nel profilo Azure |
| "Azure Cache for Redis" | ⚠️ datato | in ritiro (creazione bloccata ai nuovi clienti dal 2026-04-01): usare **Azure Managed Redis** |
| "Azure Static Web Apps" per Next.js 16 | ⚠️ | il supporto SWA a Next.js con SSR/App Router è l'offerta "hybrid", con limiti noti e stato preview; con `output: standalone` già in uso (`apps/web/Dockerfile`) la via sicura è Container Apps o App Service for Containers |
| `AddChatClient(new OllamaChatClient(...))` | ⚠️ datato | `OllamaChatClient` era nel pacchetto preview `Microsoft.Extensions.AI.Ollama`, ritirato; oggi Ollama si usa con **OllamaSharp**, che implementa `IChatClient`. Vedi §4 del report principale: MEAI sotto `ILlmClient`, non al posto |
| "Semantic Kernel" come alternativa | ⚠️ | non necessario: SK si appoggia a MEAI; aggiungerlo porta un secondo modello di agenti accanto a `orchestration-service` |
| `IDistributedCache` | ⚠️ | il repo è già oltre: `HybridCache` (L2 Redis spento in `appsettings.json:253`) + `MultiTierCache` L1/L2/L3 (`appsettings.json:261-287`). Ciò che manca per Azure è TLS/connection string in `InfrastructureServiceExtensions.cs:267-275` |
| `version: '3.8'` nel compose | ⚠️ | chiave obsoleta in Compose v2; il compose del repo non la usa |
| `NEXT_PUBLIC_API_URL` disaccoppiata | 💡 da verificare | controllare `apps/web/.env.development.example` e `next.config.js` (CSP ha due sorgenti: `next.config.js` + `proxy.ts`) |
| Log su stdout come best practice ACA | 💡 | vale; è l'esercizio 6 del report principale (OTLP + App Insights). Il log su console in JSON è un prerequisito a costo zero |

**Da tenere**: log JSON su stdout nel profilo Azure; FinOps con i grant gratuiti (già nel report principale, §7); nessun secondo meccanismo di secret.

---

## 2. Semantic caching con Redis

### Testo ricevuto

Idea: confrontare il vettore della nuova domanda con i vettori delle domande già risposte; se la similarità coseno supera una soglia (es. 0,95), restituire la risposta salvata senza chiamare LLM né DB vettoriale.

DTO:

```csharp
namespace MeepleAI.Infrastructure.Caching;

public record SemanticCacheEntry(
    string OriginalPrompt,
    float[] PromptVector,
    string AnswerText,
    List<CitationSourceDto> Citations,
    DateTime CreatedAtUtc
);

public record CitationSourceDto(string CitationTag, int PageNumber, string SectionName);
```

Servizio (StackExchange.Redis, scansione delle chiavi e coseno calcolato in app):

```csharp
public interface ISemanticCacheService
{
    Task<SemanticCacheEntry?> GetCachedResponseAsync(ReadOnlyMemory<float> queryVector, float similarityThreshold = 0.95f, CancellationToken ct = default);
    Task SetCachedResponseAsync(string prompt, ReadOnlyMemory<float> queryVector, string answer, List<CitationSourceDto> citations, TimeSpan? ttl = null, CancellationToken ct = default);
}

public class RedisSemanticCacheService : ISemanticCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private const string CachePrefix = "semantic_cache:";

    public RedisSemanticCacheService(IConnectionMultiplexer redis) => _redis = redis;

    public async Task<SemanticCacheEntry?> GetCachedResponseAsync(ReadOnlyMemory<float> queryVector, float similarityThreshold = 0.95f, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var server = _redis.GetServers().FirstOrDefault();
        if (server == null) return null;

        var keys = server.Keys(pattern: $"{CachePrefix}*").ToArray();   // scansione completa
        float bestSimilarity = 0f;
        SemanticCacheEntry? bestMatch = null;
        var queryVectorSpan = queryVector.Span;

        foreach (var key in keys)
        {
            var valueJson = await db.StringGetAsync(key);
            if (valueJson.IsNullOrEmpty) continue;
            var entry = JsonSerializer.Deserialize<SemanticCacheEntry>(valueJson.ToString());
            if (entry == null) continue;
            float similarity = ComputeCosineSimilarity(queryVectorSpan, entry.PromptVector);
            if (similarity > bestSimilarity && similarity >= similarityThreshold) { bestSimilarity = similarity; bestMatch = entry; }
        }
        return bestMatch;
    }

    public async Task SetCachedResponseAsync(string prompt, ReadOnlyMemory<float> queryVector, string answer, List<CitationSourceDto> citations, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var cacheKey = $"{CachePrefix}{Guid.NewGuid()}";
        var entry = new SemanticCacheEntry(prompt, queryVector.ToArray(), answer, citations, DateTime.UtcNow);
        await db.StringSetAsync(cacheKey, JsonSerializer.Serialize(entry), ttl ?? TimeSpan.FromHours(24));
    }

    private static float ComputeCosineSimilarity(ReadOnlySpan<float> vecA, ReadOnlySpan<float> vecB)
    {
        if (vecA.Length != vecB.Length) return 0f;
        float dot = 0f, normA = 0f, normB = 0f;
        for (int i = 0; i < vecA.Length; i++) { dot += vecA[i] * vecB[i]; normA += vecA[i] * vecA[i]; normB += vecB[i] * vecB[i]; }
        if (normA == 0f || normB == 0f) return 0f;
        return dot / ((float)Math.Sqrt(normA) * (float)Math.Sqrt(normB));
    }
}
```

Handler CQRS proposto (`AskMeepleAiQuery(UserQuery, GameId)` → embedding → cache → pgvector + rerank → LLM → `SetCachedResponseAsync` con TTL 12 h) e registrazione DI (`AddSingleton<IConnectionMultiplexer>` da `Redis:Host`, `AddScoped<ISemanticCacheService, RedisSemanticCacheService>`).

### Verifica contro il repo

| Punto | Esito | Evidenza |
|---|---|---|
| Una semantic cache esiste già | ✅ | `MultiTierCache` L1 memoria → L2 Redis → **L3 pgvector** con `L3MinSimilarityScore = 0.85` (`appsettings.json:262-273`, #3494); plugin RAG `CacheSemanticPlugin` (`BoundedContexts/KnowledgeBase/Domain/Plugins/Implementations/Cache/CacheSemanticPlugin.cs`); invalidazione all'indicizzazione in `PdfIndexingPipeline.InvalidateSemanticCacheSafelyAsync` (`:69,199`) e nei comandi `DeleteKbDocument`/`DeletePdf`/`IndexPdf` |
| Scansione `server.Keys(pattern)` + coseno in app | ⚠️ | `KEYS` è O(N) e blocca il server; con la cache che cresce il "< 10 ms" promesso diventa falso. La versione corretta è un indice vettoriale: **Redis Query Engine** (`FT.CREATE ... VECTOR HNSW`, client NRedisStack) disponibile in Redis 8 open source e in Azure Managed Redis; oppure, come già fa il repo, L3 su pgvector |
| Nessuna chiave per gioco | ⚠️ **difetto** | `GetCachedResponseAsync` ignora `GameId`: una domanda su un gioco può ricevere la risposta cachata di un altro. Il prefisso deve includere `gameId` (e lingua/tier, visto che il tier cambia modello e profondità) |
| Invalidazione solo via TTL | ⚠️ | un reindex o una nuova versione del PDF lascia in cache citazioni con pagine sbagliate per 12–24 h. Il repo invalida esplicitamente per `gameId`/`pdfDocumentId`: riusare quel contratto |
| Soglia 0,95 "= 95% di similarità" | ⚠️ | per e5 le similarità coseno sono compresse verso l'alto (molte coppie non correlate stanno sopra 0,8), quindi 0,95 non equivale a "stessa domanda". La soglia va **tarata sul golden set** di `KbQuality`/`tests/llm-eval`, misurando falsi hit (risposta sbagliata servita) e non solo hit rate. Il repo oggi usa 0,85 su L3 |
| Secondo `IConnectionMultiplexer` singleton | ⚠️ | ne esiste già uno in `Extensions/InfrastructureServiceExtensions.cs:267-322`; registrarne un altro apre due connessioni e due configurazioni |
| `IChatClient.CompleteAsync` | ⚠️ datato | nell'API GA di MEAI è `GetResponseAsync`; comunque le risposte passano da `ILlmService` (`Services/ILlmService.cs`) |
| Payload con citazioni e `FromCache` | 💡 | utile: esporre `FromCache` nella risposta e nel log della pipeline RAG permette di misurare l'hit rate in Prometheus |
| Vettore `float[]` dentro JSON | 💡 con riserva | 768 float in JSON ≈ 8 KB per voce; con Redis Query Engine si memorizza come blob FLOAT32 nel campo VECTOR |

**Da tenere**: l'idea di spostare la semantic cache da L3 pgvector a Redis con indice vettoriale è l'esercizio 7 del report principale (Managed Redis + vector index): va fatto **sopra** `IMultiTierCache` (`BoundedContexts/KnowledgeBase/Domain/Services/Caching/IMultiTierCache.cs`), non con un servizio parallelo, e con chiave per gioco, invalidazione esplicita e soglia tarata.

---

## 3. Streaming SSE con `IAsyncEnumerable<string>`

### Testo ricevuto (condensato)

- Handler MediatR che restituisce `Task<IAsyncEnumerable<string>>`, con un metodo privato `async IAsyncEnumerable<string> StreamResponseAsync(... [EnumeratorCancellation] CancellationToken ct)` che fa embedding → retrieval → `_chatClient.CompleteStreamingAsync(prompt)` → `yield return update.Text`.
- Endpoint Minimal API `POST /api/chat/stream`: `Content-Type: text/event-stream`, `Cache-Control: no-cache`, `Connection: keep-alive`; per ogni token scrive `data: {token}\n\n` (newline sostituiti con `\\n`), flush, e chiude con `data: [DONE]\n\n`.
- Hook React `useAskMeepleAiStream` che fa `fetch` POST, legge `response.body.getReader()`, fa `chunk.split("\n\n")`, estrae `data: `, ripristina i newline e concatena in uno `useState<string>`.

### Verifica contro il repo

| Punto | Esito | Evidenza |
|---|---|---|
| Streaming SSE della risposta RAG | ✅ già in produzione | `Routing/AiEndpoints.cs:139,146` (`/agents/explain/stream`, `/agents/qa/stream`), `AdminDebugChatEndpoints.cs:21`; client web `apps/web/src/lib/agent/sse-handler.ts`, `lib/api/clients/{agentsClient,chatClient,kbAskClient}.ts`; `ChatSlideOverPanel` esiste in `apps/web/src/components/chat/panel/` |
| Handler MediatR che restituisce `IAsyncEnumerable` | ✅ pattern già usato | `GetDashboardStreamQueryHandler`, `GetSessionStreamQueryHandler`, `TestRagPipelineCommandHandler`, `ReplayRagExecutionCommandHandler` |
| Ripresa dello stream | ✅ oltre la proposta | `Last-Event-ID` + replay in `GetAdminEventsStreamQueryHandler` e `ILiveSessionStreamGateway` (cfr. #3711 in `CLAUDE.md`) |
| Timeout per chunk | ✅ oltre la proposta | deadline virtuale con `TimeProvider` (#3601, `CancellationTokenSourceTimeProviderContractTests`) |
| `[DONE]` come stringa dentro `data:` | ⚠️ | SSE prevede il campo `event:` (es. `event: done`) e `id:` per la ripresa; un token che valesse letteralmente `[DONE]` chiuderebbe lo stream. Il repo usa eventi tipizzati (`RagPipelineTestEvent`, `INotification`) |
| `chunk.split("\n\n")` sul singolo `read()` | ⚠️ **difetto** | un evento può arrivare spezzato fra due `read()`: senza un buffer residuo si perdono o si corrompono token. Serve un parser incrementale (o `EventSource`/`fetch-event-source`, che però non fa POST nativamente) |
| `token.Replace("\n", "\\n")` | ⚠️ | SSE gestisce i newline con righe `data:` multiple; l'escape manuale è fragile con `\\n` letterali nel testo |
| `setMessages(prev => prev + token)` per ogni token | ⚠️ | un re-render per token; il client esistente accumula e aggiorna a batch |
| `CompleteStreamingAsync` | ⚠️ datato | in MEAI GA è `GetStreamingResponseAsync`; nel repo lo streaming passa da `ILlmService` / `ILlmClient` (stream testo e multimodale) |
| `RequireAuthorization()` sul gruppo | ⚠️ | il repo usa filtri custom come `IEndpointFilter` (es. `Filters/RequireAdminSessionFilter.cs`), non le policy ASP.NET |
| Retrieval stub nell'handler | ⚠️ | la pipeline reale è `HybridSearchService` + `ResilientRetrievalService` + plugin RAG; non va duplicata in un handler nuovo |

**Da tenere**: nulla da implementare; serve come checklist per una revisione del client SSE esistente (parser incrementale, eventi tipizzati, batch dei re-render) se qualcuno lo tocca.

---

## Sintesi

| Testo | Valore aggiunto netto | Azione |
|---|---|---|
| 1. Mappatura locale/Azure | basso: il repo è già oltre su storage, compose, cache; tre indicazioni datate (Cache for Redis, `OllamaChatClient`, SWA) | tenere "log JSON su stdout"; il resto è coperto dal report principale |
| 2. Semantic cache Redis | medio: l'idea è buona, l'implementazione no (KEYS, nessuna chiave per gioco, nessuna invalidazione, soglia non tarata) | confluisce nell'esercizio 7 del report principale, sopra `IMultiTierCache` |
| 3. SSE streaming | nullo come feature (esiste già, con ripresa e timeout virtuale); utile come checklist di difetti da non reintrodurre nel client | nessuna |
