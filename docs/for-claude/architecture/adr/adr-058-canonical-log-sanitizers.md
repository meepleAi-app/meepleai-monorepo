# ADR-058 — Canonical Log Sanitizer Strategy: PII Masking vs Log-Forging Protection

**Status**: Proposed
**Date**: 2026-05-15
**Deciders**: @badsworm
**Reviewers solicitati**: nessuno (decisione tecnica con scope contenuto a security tooling + 2 file utility; nessun impatto product-side, nessun cambio di contract API esterno)
**Tracking**: Issue [#1181](https://github.com/meepleAi-app/meepleai-monorepo/issues/1181) (Phase 1 — CodeQL high alerts)
**Supersedes**: —

## Context

CodeQL su `main-dev` riporta **108 alert `severity=error`** aperti (snapshot 2026-05-15), distribuiti su ~40 file:

- 107 × `cs/exposure-of-sensitive-information` (PII in operational log)
- 1 × `cs/log-forging` (input non sanitizzato in log statement)

Lo spec-panel review di #1181 (Wiegers · Fowler · Nygard · Crispin · Adzic, 2026-05-15) ha smentito la premessa originale dell'issue (*"introdurre un helper PII-safe"*) e identificato **3 sanitizer già esistenti** nel codebase, con responsabilità che si rivelano **distinte e parzialmente sovrapposte**:

| Sanitizer | Path | Visibilità | Responsabilità reale | Callers |
|-----------|------|-----------|----------------------|---------|
| `DataMasking` | `Infrastructure/Security/DataMasking.cs` | `internal static partial` | **Confidentiality**: maschera contenuto PII (email/JWT/CC/IP/connection-string) — irreversibile, GDPR-aware | 100 |
| `LogSanitizer` | `Helpers/LogSanitizer.cs` | `public static` | **Integrity**: strippa `\r\n\t` per prevenire log-forging (CWE-117) | 34 |
| `LogValueSanitizer` | `Middleware/LogValueSanitizer.cs` | `internal static` | **Integrity** + helper `SanitizePath` con URL-decode preventivo | 85 |

### Analisi dei tre helper

**`DataMasking`** è **ortogonale** agli altri due. Risolve un problema diverso (confidentiality) usando regex `GeneratedRegex` con `matchTimeoutMilliseconds: 1000` e `ExplicitCapture` (anti-ReDoS, conforme `MA0009`/`MA0023`). API ampia e ben formata: `MaskEmail`, `MaskString`, `MaskJwt`, `MaskCreditCard`, `MaskIpAddress`, `RedactConnectionString`, `SanitizeUser`, `MaskResponseBody`. Internal partial ma usato da 100 call site → de facto canonico.

**`LogSanitizer` e `LogValueSanitizer`** risolvono lo **stesso problema** (rimuovere `\r\n`) con due implementazioni quasi identiche:

```csharp
// LogSanitizer.Sanitize  (public, Helpers namespace)
return value.Replace("\r","").Replace("\n","").Replace("\t"," ");

// LogValueSanitizer.Sanitize  (internal, Middleware namespace)
return value.Replace("\r", string.Empty).Replace("\n", string.Empty);
```

Differenze osservabili:
- `LogSanitizer` strippa anche `\t` (sostituendolo con spazio)
- `LogSanitizer` ha overload `Sanitize(string?, int maxLength)` per truncate
- `LogValueSanitizer` ha helper `SanitizePath(PathString)` con URL-decode preventivo (`%0D`/`%0A` → `\r\n`)
- Visibilità divergente blocca l'uso cross-namespace senza duplicazione

Inoltre **CodeQL non riconosce nessuno dei tre come sanitizer** (`OAuthEndpoints.cs:230` chiama `LogValueSanitizer.Sanitize` ma alert #537 resta aperto; `EmailService.ShareRequests.cs:44` chiama `DataMasking.MaskEmail` ma alert #417 resta aperto). Il file `.github/codeql/codeql-config.yml:11-12` esclude globalmente `cs/log-forging` — l'alert residuo conferma che la config non è applicata o non viene letta dal workflow corrente.

## Decision

**1) Confidentiality (PII masking) — `DataMasking` è il sanitizer canonico, promosso a `public`.**

`DataMasking` resta l'unica API ufficiale per mascherare contenuto PII. L'unica modifica strutturale è:

- Cambio visibilità da `internal static partial` a `public static partial` (necessario per essere riconosciuto cross-namespace e per CodeQL `Models as Data` registration)
- Namespace invariato: `Api.Infrastructure.Security`

Le 100 chiamate esistenti restano funzionanti senza modifiche.

**2) Integrity (log-forging prevention) — `LogSanitizer` è il sanitizer canonico; `LogValueSanitizer` è deprecato.**

Consolidazione asimmetrica:

- **`LogSanitizer`** (public, 34 caller) **assorbe** `LogValueSanitizer.SanitizePath` come overload `SanitizePath(PathString)`
- **`LogValueSanitizer`** (internal, 85 caller) viene **marcato `[Obsolete]`** e diventa wrapper di `LogSanitizer` (no-op forwarder per non rompere build) per **una release**
- Codemod sweep migra gli 85 caller da `LogValueSanitizer.X` a `LogSanitizer.X` (gestito come parte di Phase 2 sweep su #1181)
- Dopo lo sweep, `LogValueSanitizer.cs` viene rimosso

Razionale per la direzione di consolidazione (`LogValueSanitizer` → `LogSanitizer`, non viceversa nonostante 85 > 34):

- `LogSanitizer` è già `public` e in namespace di utility (`Api.Helpers`) coerente
- `LogValueSanitizer` è in `Api.Middleware`, namespace funzionale errato (middleware ≠ utility)
- Il codemod è meccanico (find/replace di identificatore + `using` directive)
- La consolidazione inversa richiederebbe promuovere visibilità (internal → public) + cambio namespace, stesso cost

**3) CodeQL recognition — registrare i sanitizer come `SanitizingMethod`.**

Estendere `.github/codeql/codeql-config.yml` con una custom query suite (o `qlpack` con `Models as Data`) che dichiari:

```yaml
extensions:
  - addsTo:
      pack: codeql/csharp-all
      extensible: sanitizerModel
    data:
      - ["Api.Infrastructure.Security", "DataMasking", true, "MaskEmail", "(System.String)", "", "Argument[0]", "log-injection", "manual"]
      - ["Api.Infrastructure.Security", "DataMasking", true, "MaskString", "(System.String,System.Int32)", "", "Argument[0]", "log-injection", "manual"]
      - ["Api.Infrastructure.Security", "DataMasking", true, "MaskJwt", "(System.String)", "", "Argument[0]", "log-injection", "manual"]
      - ["Api.Infrastructure.Security", "DataMasking", true, "MaskCreditCard", "(System.String)", "", "Argument[0]", "log-injection", "manual"]
      - ["Api.Infrastructure.Security", "DataMasking", true, "MaskIpAddress", "(System.String)", "", "Argument[0]", "log-injection", "manual"]
      - ["Api.Helpers", "LogSanitizer", true, "Sanitize", "(System.String)", "", "Argument[0]", "log-injection", "manual"]
      - ["Api.Helpers", "LogSanitizer", true, "Sanitize", "(System.String,System.Int32)", "", "Argument[0]", "log-injection", "manual"]
      - ["Api.Helpers", "LogSanitizer", true, "SanitizePath", "(Microsoft.AspNetCore.Http.PathString)", "", "Argument[0]", "log-injection", "manual"]
```

Per `cs/exposure-of-sensitive-information` (CWE-359) la modellazione è equivalente con `sourceModel`/`sinkModel` appropriato — sintassi esatta da convalidare in Phase 1 contro la versione di CodeQL CLI in CI.

Verificare contestualmente perché l'exclude esistente di `cs/log-forging` (`codeql-config.yml:11-12`) non chiude l'alert #537: probabile config drift tra workflow `codeql.yml` e file di configurazione, oppure path filter che esclude `Routing/` per errore.

## Alternatives considered

### A. Unica classe sanitizer `Pii` che fonde `DataMasking` + `LogSanitizer`
**Rifiutata**. I due problemi (confidentiality vs integrity) hanno semantica radicalmente diversa: PII masking è **lossy by design** (irreversibile), log-forging stripping è **lossless** (preserva il contenuto, rimuove solo control chars). Fonderli in una API unica:
- Confonde il chiamante: `Sanitize(email)` → maschera o strippa?
- Forza chiamate doppie: per loggare un email user-provided servono ENTRAMBE le operazioni (`DataMasking.MaskEmail(LogSanitizer.Sanitize(rawInput))`)
- Vìola SRP (Fowler): due reason-to-change in una classe

### B. Introdurre `IPiiSafeLogger : ILogger` come extension method
**Rifiutata** (proposta originale dell'issue body pre-revisione). Aggiunge un quarto layer sopra `ILogger`/`Microsoft.Extensions.Logging`; richiede DI registration; non risolve il problema CodeQL recognition (che è a livello di static analysis, non runtime); duplica logica già presente nei 3 sanitizer esistenti. Costo migrazione: re-injection di ~50 servizi.

### C. Solo suppression `[SuppressMessage]` su tutti i 108 alert
**Rifiutata**. Suppression a tappeto cancella il segnale di regressione futura: il prossimo `_logger.LogInformation("{Email}", user.Email)` non viene segnalato. Inoltre i casi `EmailOutboxService.cs:70` (true positive senza sanitizer applicato) sarebbero **bug nascosti**, non false positive.

### D. Roslyn analyzer custom `MeepleAi.Analyzers.NoPiiInLog` come unica difesa
**Considerata, scope rinviato a Phase 3**. Un analyzer fornisce *prevention* migliore di CodeQL (feedback in-IDE) ma non sostituisce il fix del backlog esistente (107 alert da chiudere). Phase 3 di #1181 aggiunge l'analyzer come regression guard, non come strumento di remediation iniziale.

## Consequences

### Positive

- **Una sola API canonica per ciascuna delle due preoccupazioni** → chiamante sceglie il sanitizer in base all'intent (confidentiality → `DataMasking.Mask*`; integrity → `LogSanitizer.Sanitize`)
- **CodeQL recognition** elimina i falsi positivi residui dove il sanitizer è già applicato (stimato 3-10 alert su 108 → verifica empirica in Phase 1)
- **Riduzione cognitive load**: 3 → 2 sanitizer da memorizzare
- **Namespace coerenti**: `Api.Infrastructure.Security.DataMasking` per security, `Api.Helpers.LogSanitizer` per utility — segue la convention DDD del progetto
- **Build green durante migrazione**: `LogValueSanitizer` come forwarder `[Obsolete]` durante lo sweep evita la breaking change

### Extension policy

Nuove categorie di PII (es. phone numbers, SSN, IBAN, indirizzi fisici) si aggiungono come metodo statico in `DataMasking` (es. `MaskPhone`, `MaskIban`). **Non** si introducono nuovi sanitizer di livello superiore (no `PhoneMasking.cs`, no `IPiiMasker` interface). Stessa policy per nuove forme di control-char stripping: estendere `LogSanitizer`, non creare nuove utility namespace-level.

### Negative

- **Codemod 85-call sweep** richiesto per migrare `LogValueSanitizer` → `LogSanitizer` (mitigato: meccanico, da fare in una PR isolata di Phase 2 wave 0)
- **CodeQL extension config** è specifica della versione CLI in uso (`security-extended` query suite); modifica future-major-version potrebbe richiedere update
- **Promozione visibilità `DataMasking` internal → public** apre l'API a callers esterni futuri (test, altri assembly): accettato perché il progetto è single-assembly per ora (`Api`); rivedere se introdurremo `Api.Domain` come progetto separato

### Neutral

- L'ADR non risolve direttamente i 107 alert PII open — quello è il lavoro di **Phase 2 adoption sweep** (issue #1181). Questo ADR fissa solo la *destinazione* canonica del sweep.

## Implementation plan

Esecuzione segue le fasi già definite in #1181 (revised body 2026-05-15):

| Phase | Step | Output |
|-------|------|--------|
| 1.1 | Promuovere `DataMasking` a `public` | 1-line diff, build verde |
| 1.2 | Spostare `SanitizePath` da `LogValueSanitizer` a `LogSanitizer`; **migrare unit test esistente** preservando assertion set (incluso input `/path%0Anew-log-line` → URL-decode → control-char stripping) | Refactor, no behavior change, test verde |
| 1.3 + 2.0 | **Combinati** (vedi nota sotto): introdurre forwarder `[Obsolete]` E migrare contestualmente tutti gli ~85 call site (`LogValueSanitizer` → `LogSanitizer` + `using Api.Helpers;`); rimuovere `LogValueSanitizer.cs` quando `grep -r "LogValueSanitizer" apps/api/src/Api` ritorna 0 match | Build verde, 85 caller migrati, forwarder rimosso |
| 1.4 | Aggiornare `codeql-config.yml` con extension models (sanitizer registration); **validare config localmente con `codeql database analyze` prima di push** per evitare rottura CI | Custom suite caricata dal workflow CodeQL |
| 1.5 | Riparare exclude `cs/log-forging` non applicata (probabile config drift fra workflow e file) | Alert #537 chiuso |
| 2.a-2.f | Adoption sweep PII masking per BC (vedi #1181) | 107 alert chiusi |
| 3 | Analyzer/CI gate (vedi #1181) | Regression guard attivo |

> **Plan revision 2026-05-15**: la pianificazione originale separava Phase 1.3 (`[Obsolete]` forwarder + build warnings) da Phase 2.0 (codemod sweep in PR dedicata). Verifica empirica durante l'esecuzione ha rivelato che `Api.csproj` tratta `CS0618` come **errore** (build setting via `<TreatWarningsAsErrors>` o `WarningsAsErrors`), rendendo l'approccio "warning, migrate gradualmente" non praticabile. Le due fasi sono state quindi unificate: il forwarder `[Obsolete]` viene introdotto e i ~85 caller migrati contestualmente, in una sola PR. La decisione architetturale resta invariata; cambia solo la cadenza di esecuzione.

## Acceptance

- [ ] `DataMasking` promossa `public`, namespace invariato — verificato con `dotnet build`
- [ ] `LogSanitizer` ha overload `SanitizePath(PathString)` con stesso behavior di `LogValueSanitizer.SanitizePath` — verificato da unit test migrato (assertion set preservato, incluso il caso di URL-decode `%0D`/`%0A` seguito da stripping)
- [ ] `LogValueSanitizer.*` marcato `[Obsolete]`, 85 build warnings attesi
- [ ] `codeql-config.yml` carica custom extension con sanitizer models — **verifica positiva**: PR sintetica con `_logger.LogInformation("{Email}", DataMasking.MaskEmail(rawEmail))` non apre alert
- [ ] **Verifica negativa simmetrica**: PR sintetica con `_logger.LogInformation("{Email}", rawEmail)` (senza sanitizer) APRE alert `cs/exposure-of-sensitive-information` → conferma che la rule è ancora attiva e non globalmente disabilitata
- [ ] Alert #537 (`OAuthEndpoints.cs:230`, `cs/log-forging`) chiuso dal solo Phase 1 — verificato da CodeQL re-run su `main-dev`
- [ ] Phase 1.4 expected close count: **almeno 10 alert chiusi senza modifica di codice** (corrispondenti ai call site dove `DataMasking.Mask*` / `LogValueSanitizer.Sanitize` sono già applicati ma flaggati come falsi positivi) — count attuale da derivare con grep cross-reference fra `gh api code-scanning/alerts` e `grep "DataMasking\\|LogValueSanitizer\\|LogSanitizer" <file>:<line>` per ogni alert

## References

- Issue [#1181](https://github.com/meepleAi-app/meepleai-monorepo/issues/1181) — Phase 1 traccia l'esecuzione di questo ADR
- Spec-panel session 2026-05-15 (memoria conversazione `/sc:spec-panel 1181`)
- File esistenti:
  - `apps/api/src/Api/Infrastructure/Security/DataMasking.cs`
  - `apps/api/src/Api/Helpers/LogSanitizer.cs`
  - `apps/api/src/Api/Middleware/LogValueSanitizer.cs`
  - `.github/codeql/codeql-config.yml`
- CodeQL Models as Data: https://codeql.github.com/docs/codeql-language-guides/customizing-library-models-for-csharp/
- CWE-117 (Improper Output Neutralization for Logs)
- CWE-359 (Exposure of Private Information)
- **GDPR Art. 32 (Security of processing)** — il pattern di PII masking negli operational log implementa pseudonymization tecnica conforme all'articolo, riducendo l'esposizione di dati personali in caso di accesso non autorizzato ai log applicativi
