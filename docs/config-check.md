# Config health check

Script: `tools/check-config.ps1`

## Come funziona
- Controlla le variabili d’ambiente usate da backend (.NET) e frontend (Next.js).
- Segnala:
  - Mancanti (⚠️ Required missing)
  - Presenti ma uguali al valore di default (ℹ️ Using default)
  - Presenti con valore custom (✅ OK)
- Esce con code 1 se manca almeno una variabile `Required`.

## Variabili monitorate
| Variabile | Scope | Default se assente | Note |
| --- | --- | --- | --- |
| POSTGRES_CONNECTION_STRING / CONNECTIONSTRINGS__POSTGRES | API | — | Connessione Postgres (obbligatoria) |
| OPENROUTER_API_KEY | API | — | Chiave LLM (obbligatoria) |
| GOLDEN_DATASET_PATH | API | auto-discovery | Solo test accuracy; warning se manca |
| API_BASE_URL | Test API | http://localhost:8080 | Baseline tests manuali |
| API_HEALTH_PATH | Test API | /health/live | Baseline tests manuali |
| BASELINE_TEST_EMAIL | Test API | admin@meepleai.dev | Opzionale |
| BASELINE_TEST_PASSWORD | Test API | Admin123!ChangeMe | Opzionale |
| NEXT_PUBLIC_API_BASE | Web | http://localhost:8080 | Endpoint backend lato web |
| NEXT_PUBLIC_SITE_URL | Web | http://localhost:3000 | Per metadata/OG |
| NEXT_PUBLIC_ENABLE_PROGRESS_UI | Web | (vuoto => false) | UI progresso upload |
| NEXT_PUBLIC_ENABLE_REMOTE_LOGS | Web | true (se non 'false') | Logging remoto |
| NEXT_PUBLIC_LOG_ENDPOINT | Web | /api/v1/logs | Endpoint logging remoto |
| NEXT_PUBLIC_RETRY_ENABLED / ...RETRY_* | Web | true / 3 / 300 / 5000 | Politica retry fetch |
| NEXT_PUBLIC_CIRCUIT_BREAKER_* | Web | abilitato, soglie di default | Circuit breaker client |
| NEXT_PUBLIC_HYPERDX_API_KEY | Web | demo | Telemetria HyperDX |

## Uso
```powershell
cd D:\Repositories\meepleai-monorepo
pwsh ./tools/check-config.ps1
```

## Output atteso
- Tabella con stato per ogni variabile.
- Riassunto finale con exit code (0 OK, 1 config mancante).

## Convenzione file .env
- Usa un solo file per ambiente, nella root:
  - `.env.development`, `.env.staging`, `.env.production`
- I vecchi `.env` annidati in `apps/api` o `apps/web` sono stati rimossi. Copia eventuali valori custom nei file root.
