# Deep Dive: Postman Test Failure Investigation - 2025-11-20

## Executive Summary

Indagine completata sul blocco dei test Postman/Newman. Il 20 novembre 2025 tutti i login API fallivano con `500 Internal Server Error`. La root cause reale era combinata:

1. **Lettura manuale del body** in `POST /api/v1/auth/login`, che invocava `context.Request.ReadFromJsonAsync<LoginPayload>` e consumava lo stream.
2. **Rate limiter fail-open**: il `RateLimitingMiddleware` racchiudeva `_next(context)` in un `try/catch`, rieseguendo l'endpoint dopo ogni eccezione e tentando di rileggere il body vuoto.
3. **Disallineamento risposta/cookie**: il payload `AuthResponse` serializzava `User`/`ExpiresAt` in PascalCase e il cookie non corrispondeva a quello atteso da Postman (`meepleai_session`).

**Fix applicati** (20 nov 2025, 12:52-12:58 UTC):
- Ripristinato il binding automatico `[FromBody] LoginPayload`.
- Limitato il `try/catch` del rate limiter al solo blocco di rate checking.
- Allineati DTO/cookie e aggiornati i documenti legacy.
- Rerun Newman (`newman run ... --folder "Login"`) con esito 3/3 asserzioni.

## Problem Statement

- **Endpoint**: `POST /api/v1/auth/login`
- **Expected**: `200 OK` con cookie di sessione
- **Actual**: `500 Internal Server Error` + `InvalidJsonRequestBody`
- **Severity**: Blocco totale autenticazione (32/33 test Postman falliti)

## Investigation Timeline

| Orario (UTC) | Fase | Dettagli |
|-------------|------|----------|
| 13:13 | Phase 1 – Newman baseline | `newman run tests/postman/MeepleAI-API.postman_collection.json` → 3 assertions fallite su Login, tutte le richieste autenticate bloccate. |
| 13:20 | Phase 2 – Stack health | Docker compose completo (`infra-meepleai-*`) UP & healthy; solo `/api/v1/auth/login` restituisce 500. |
| 13:25 | Phase 3 – Logs & stack trace | `docker logs infra-meepleai-api-1` mostra `InvalidJsonRequestBody` prima del MediatR handler. |
| 13:35 | Phase 4 – Middleware audit | `RateLimitingMiddleware` logga "encountered an error; allowing request" prima dell'eccezione → `_next` invocato 2 volte. |
| 13:45 | Phase 5 – Hotfix | Ripristinato `[FromBody]`, limitato `try/catch`, riallineati DTO/cookie; rebuild API container. |
| 13:50 | Phase 6 – Validation | `curl` e `newman ... --folder "Login"` restituiscono `200 OK`, cookie `meepleai_session`, p95 321 ms. |

## Error Stack Trace (pre-fix)

```
Microsoft.AspNetCore.Http.BadHttpRequestException: Failed to read parameter "LoginPayload payload"
 --> System.Text.Json.JsonException: The input does not contain any JSON tokens
    at System.Text.Json.JsonConverter`1.ReadCore(...)
    at Microsoft.AspNetCore.Http.HttpRequestJsonExtensions.ReadFromJsonAsync(...)
    at Api.Routing.AuthEndpoints... (line 88)
    at Api.Middleware.RateLimitingMiddleware.InvokeAsync(HttpContext context) : line 87
```

**Nota**: il middleware rilanciava `_next(context)` dopo aver già letto il body, provocando l'eccezione di binding.

## Technical Analysis

### Finding 1: Lettura manuale del body (Critical)
- **Evidenza**: Stack trace fermo in `ReadFromJsonAsync`; logs mostrano `payload is null`.
- **Fix**: usare il binding implicito `[FromBody] LoginPayload payload` (config JSON globale già case-insensitive).

### Finding 2: RateLimiter fail-open troppo esteso (Critical)
- **Evidenza**: log `Rate limiting middleware encountered an error; allowing request` immediatamente prima dell'eccezione JSON.
- **Fix**: il `try/catch` ora avvolge solo la chiamata a `rateLimiter.CheckRateLimitAsync`; `_next(context)` sta fuori.

### Finding 3: Risposta/cookie disallineati (High)
- **Evidenza**: Postman cerca `jsonData.user` e il cookie `meepleai_session`, ma la risposta forniva proprietà in PascalCase e non impostava correttamente quel cookie → asserzioni fallite anche con login riuscito.
- **Fix**: `[JsonPropertyName]` su `AuthResponse` + `SessionCookieConfiguration.Name = "meepleai_session"` + aggiornamento middleware Next.js e doc legacy.

## Recommended Fix Actions

1. **(Done)** Ripristinare binding automatico (`AuthEndpoints.cs`).
2. **(Done)** Limitare il `try/catch` del rate limiter (`RateLimitingMiddleware.cs`).
3. **(Done)** Allineare DTO/cookie + documentazione/Next.js (`AuthContracts.cs`, `SessionCookieConfiguration.cs`, `CookieHelpers.cs`, `apps/web/middleware.ts`, `docs/archive/*`).
4. **(Todo)** Aggiungere integration test minimo (`apps/api/tests`) + pipeline Newman nightly per cartella Authentication.
5. **(Todo)** Coprire il rate limiter con un test che garantisca una sola chiamata a `_next` e log dedicati in caso di fallback.

## Impact Assessment

_Nota: la matrice seguente descrive lo stato PRIMA dell'applicazione dei fix; dopo i fix l'autenticazione è di nuovo operativa._

### Blocked Functionality (pre-fix)
1. Login/password flow
2. Session management (`/api/v1/auth/session/*`)
3. Qualsiasi endpoint autenticato (RAG, Games, Admin)
4. Newman/Postman CI (32/33 asserzioni fallite)

### Working Functionality
- Health/ready endpoints
- Docker stack (Postgres, Redis, Qdrant, Ollama, n8n)
- Observability (Prometheus, Grafana, Jaeger, Seq)

## Performance Metrics

| Endpoint | Status pre-fix | Status post-fix |
|----------|----------------|-----------------|
| `/health` | 18.7 ms, HTTP 200 | invariato |
| `/` | 1.5 ms, HTTP 200 | invariato |
| `/api/v1/auth/login` | HTTP 500 (JSON binding) | HTTP 200, 321 ms, cookie `meepleai_session` |

## Next Actions

1. [ ] Integrare test di login camelCase in `apps/api/tests` + workflow CI.
2. [ ] Estendere `RateLimitingMiddleware` tests per assicurare `_next` non venga reinvocato.
3. [ ] Monitorare Newman nightly e Slack alert sulla cartella Authentication.
4. [ ] Documentare nel playbook (CLAUDE.md) la regola "mai leggere il body manualmente".

## Files Modified
- `apps/api/src/Api/Routing/AuthEndpoints.cs` – binding `[FromBody]`.
- `apps/api/src/Api/Middleware/RateLimitingMiddleware.cs` – `_next(context)` fuori dal `try/catch`.
- `apps/api/src/Api/Models/AuthContracts.cs` – `[JsonPropertyName]` su `AuthResponse`.
- `apps/api/src/Api/Models/SessionCookieConfiguration.cs`, `CookieHelpers.cs`, `apps/api/src/Api/Extensions/ObservabilityServiceExtensions.cs` – `meepleai_session` default.
- `apps/web/middleware.ts` – stessa cookie name sul frontend.
- `docs/archive/security-audits/*.md` + `claudedocs/postman-test-deep-dive-2025-11-20.md` – documentazione aggiornata.
- Newman evidence: `newman run tests/postman/MeepleAI-API.postman_collection.json -e tests/postman/MeepleAI-Local.postman_environment.json --folder "Login"`.

## Conclusions

1. **Root cause**: combinazione di lettura manuale del body + rate limiter fail-open.
2. **Serializer OK**: nessun problema con `ConfigureHttpJsonOptions`.
3. **Fix applicato**: login torna operativo e i test Postman passano (3/3).
4. **Debt residuo**: mancano integration test e alert automatici per regressioni simili.

## Recommendations

- Aggiungere integration test per ogni endpoint critico (login/register/logout) nel pacchetto API.
- Bloccare pattern "leggi manualmente HttpRequest" nelle review (checklist CLAUDE.md).
- Monitorare `RateLimitingMiddleware` per assicurarsi che il fail-open non mascheri eccezioni future (log dedicato + metriche).
- Rieseguire Newman nightly e allegare i report a Slack/Artifact per verificare l'allineamento dei cookie/documentazione.

**Investigation Completed**: 2025-11-20 13:50 UTC  
**Time Spent**: ~90 min  
**Status**: ✅ Login ripristinato, follow-up test/monitoring in corso
