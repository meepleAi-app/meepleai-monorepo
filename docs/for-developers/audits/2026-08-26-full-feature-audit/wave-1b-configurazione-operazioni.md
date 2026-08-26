# Ondata 1B — Configurazione e operazioni

> **Contesti**: SystemConfiguration · Administration (configurazioni, code, infrastruttura, monitoraggio, analytics, cache, eventi)
> **Ambiente**: locale, `make dev`, stack completo · **Data**: 2026-08-26
> **Ruoli**: `test@meepleai.com` (utente) · `badsworm@gmail.com` (superadmin)

## Esito in breve

| Contesto | Coperte | Totale |
|---|---|---|
| SystemConfiguration | 36 | 64 |
| Administration | 184 | 272 |

Tre findings, tutti con causa accertata. Il più significativo è sistemico: **sei endpoint di
configurazione falliscono per query concorrenti sullo stesso `DbContext`**, e lo stesso schema
compare in altri quindici query handler.

## Letture

94 endpoint di sola lettura provati con entrambi i ruoli.

| Contesto | Provati | Conformi | Difformi |
|---|---|---|---|
| SystemConfiguration | 22 | 17 | 5 |
| Administration | 72 | 67 | 5 |

Undici endpoint sono stati saltati perché richiedono parametri non risolvibili sull'ambiente
attuale (`containerId`, `jobId`, `pdfDocumentId`, `otherId`): restano `⬜ non coperto`, perché
saltare non è verificare.

### Tre segnalazioni che non erano difetti

| Segnalazione | Realtà |
|---|---|
| `/admin/event-outbox/{failed,pending,stats}` → 403 all'utente | Corretto: sono endpoint admin. Era il tracker a classificarli self-service, perché il parser non ne aveva dedotto l'autorizzazione |
| `PATCH /admin/configurations/{id}/toggle` → 400 | Vuole `isActive` **in query string**, non nel corpo. Con quello: 200 |
| `POST /admin/configurations/validate` → 400 | Continua a chiedere `key` anche fornendolo in query. **Non chiarito**, non marcato come difetto |

## Mutazioni

Ogni ciclo ha creato una risorsa propria, l'ha modificata e l'ha eliminata. Nessuna configurazione,
regola di allerta o feature flag preesistente è stata toccata: cambiarli avrebbe alterato il
comportamento del sistema.

**Funzionano**: creazione, modifica, attivazione e cancellazione di configurazioni; creazione,
modifica e inversione di feature flag; abilitazione di un flag per tier.

### Cosa non ho eseguito, e perché

Queste mutazioni **non sono verificate** e restano scoperte nel tracker. Non è una dimenticanza:

| Endpoint | Motivo |
|---|---|
| `POST /admin/infrastructure/services/{name}/restart` · `operations/restart-service` · `secrets/restart` | Riavviano servizi reali |
| `POST /admin/storage/migrate` | Migrazione dello storage |
| `POST /admin/providers/{name}/rotate-key` | Ruota chiavi API: romperebbe le integrazioni |
| `DELETE /admin/rag-backup/snapshots/{id}` | Cancella backup |
| `DELETE /admin/sessions/{sessionId}` | Potrebbe revocare la sessione dell'audit stesso |
| `POST /admin/alert-channels/{type}/test-connection` | Invia messaggi verso l'esterno |
| `POST /admin/configurations/{import,bulk-update}` | Agiscono in massa |

Vanno provate su un ambiente sacrificabile, non su uno che serve alle ondate successive.

## Findings

### 🔍 P1 — Sei endpoint di configurazione in 500: query concorrenti sullo stesso DbContext — [#3843](https://github.com/meepleAi-app/meepleai-monorepo/issues/3843)

| Endpoint |
|---|
| `GET /admin/config/pdf-limits` |
| `GET /admin/config/chat-history-limits` |
| `GET /admin/config/game-library-limits` |
| `GET /admin/config/pdf-tier-upload-limits` |
| `GET /admin/system/pdf-upload-limits` |
| `GET /admin/system/session-limits` |

```
at Microsoft.EntityFrameworkCore.Infrastructure.Internal.ConcurrencyDetector.EnterCriticalSection()
```

`GetAllPdfLimitsQueryHandler.cs:57` esegue `await Task.WhenAll(dailyTask, weeklyTask, perGameTask)`
sullo stesso `DbContext`, che EF Core non consente. Stesso schema in `GetChatHistoryLimitsQueryHandler`
e `GetGameLibraryLimitsQueryHandler`.

**Quindici query handler** usano `Task.WhenAll`: non tutti falliscono, ma il difetto non si
manifesta in modo uniforme — qui produce un 500 esplicito, altrove può degradare in silenzio, come
già accaduto al braccio vettoriale della ricerca.

### 🔍 P1 — Disabilitare un feature flag per tier fallisce — [#3844](https://github.com/meepleAi-app/meepleai-monorepo/issues/3844)

Sullo stesso flag e sullo stesso tier, `enable` risponde 200 e `disable` risponde 500:

```
23505: duplicate key value violates unique constraint "IX_system_configurations_Key_Environment"
```

`disable` **inserisce** invece di aggiornare la riga che `enable` ha appena creato. L'asimmetria è
la prova: il primo percorso riesce perché la riga non c'è, il secondo fallisce sempre perché la
trova.

Conseguenza: una funzionalità abilitata per un tier non può più essere tolta dall'interfaccia di
amministrazione.

### 🔍 P1 — `GET /admin/analytics/pdf` in 500 — [#3839](https://github.com/meepleAi-app/meepleai-monorepo/issues/3839)

`NavigationExpandingExpressionVisitor.VisitMethodCall`: espressione LINQ non traducibile in SQL.
Stessa famiglia degli altri quattro endpoint già tracciati in quella issue, distinta da #3843.

## Cosa resta

| Area | Righe | Nota |
|---|---|---|
| Mutazioni distruttive | ~15 | Elencate sopra: richiedono un ambiente sacrificabile |
| Letture con parametri non risolvibili | 11 | Servono un container, un job, un PDF in stato adatto |
| Resto di SystemConfiguration e Administration | ~110 | Prevalentemente mutazioni su aree non ancora toccate (queue, operations, rag-backup, openrouter) |
