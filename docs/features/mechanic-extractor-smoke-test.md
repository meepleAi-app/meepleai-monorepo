# Mechanic Extractor — Smoke Test Checklist

**Versione:** 1.0 (2026-04-20)
**Branch:** `feature/mechanic-extractor-review-tokens`
**Ambito:** Validazione manuale end-to-end di editor, AI assist, review page, token tracking, finalize e concorrenza `RowVersion`.
**Ambiente consigliato:** `make dev` (locale) con DB snapshot seedato.
**Durata stimata:** 25–35 min.

---

## Prerequisiti

### Infrastruttura

- [ ] `make dev` attivo (API + Web + Postgres + Redis + LLM)
- [ ] Snapshot DB caricato con almeno **1 `SharedGame` + 1 `PdfDocument` linkato** (es. Puerto Rico dal seed 2026-04-15 o batch `make seed-index`)
- [ ] LLM provider configurato (DeepSeek o OpenRouter) — verifica con: `curl -s http://localhost:8080/health | jq .checks.llm`
- [ ] Storage provider: `local` accettabile per smoke test (R2 non richiesto)

### Credenziali test

- [ ] Utente admin seedato con ruolo `Admin`
- [ ] Sessione browser attiva su `http://localhost:3000/admin`

### Identificatori da annotare prima di iniziare

Eseguire sul DB (`docker exec -it meepleai-postgres psql -U meepleai`):

```sql
SELECT sg."Id" AS shared_game_id, sg."Name", pd."Id" AS pdf_document_id, pd."FileName"
FROM shared_games sg
JOIN pdf_documents pd ON pd."SharedGameId" = sg."Id"
WHERE pd."ProcessingStatus" = 'Completed'
LIMIT 3;
```

Annotare:
- `SHARED_GAME_ID = _____________________________`
- `PDF_DOCUMENT_ID = ___________________________`
- `ADMIN_USER_ID = _____________________________`
- `GAME_TITLE = ________________________________`

---

## Test Cases

### TC-01 — Accesso editor e caricamento stato vuoto

**Azione**
1. Navigare a `http://localhost:3000/admin/knowledge-base/mechanic-extractor`
2. Dal dropdown selezionare il game annotato
3. Selezionare il PDF linkato

**Atteso**
- [ ] Pagina carica senza errori console
- [ ] 6 tab visibili nell'ordine: Summary · Mechanics · Victory · Resources · Phases · FAQ
- [ ] Network tab mostra `GET /api/v1/admin/mechanic-extractor/draft?sharedGameId=...&pdfDocumentId=...` → **404** (nessun draft esistente)
- [ ] Editor vuoto, tutti i textarea attivi

---

### TC-02 — Creazione draft con auto-save

**Azione**
1. Tab **Summary**: inserire note libere (min 20 caratteri)
2. Attendere trigger auto-save (timer o blur)
3. Tab **Mechanics**: inserire note
4. Ripetere per Victory, Resources, Phases, FAQ

**Atteso**
- [ ] `POST /api/v1/admin/mechanic-extractor/draft` → **200 OK** con body `MechanicDraftDto`
- [ ] `totalTokensUsed = 0`, `estimatedCostUsd = 0` (nessun AI call ancora)
- [ ] Indicatore "Saved" visibile in UI
- [ ] DB check:
  ```sql
  SELECT "Id", "SummaryNotes", "RowVersion", "TotalTokensUsed"
  FROM mechanic_drafts
  WHERE "SharedGameId" = '<SHARED_GAME_ID>';
  ```
  Una riga presente, `RowVersion` non vuoto.

Annotare `DRAFT_ID = _____________________________`

---

### TC-03 — AI Assist su sezione Summary

**Azione**
1. Tab **Summary** con note umane inserite (TC-02)
2. Click bottone "AI Assist"

**Atteso**
- [ ] `POST /api/v1/admin/mechanic-extractor/ai-assist` → **200 OK** con `AiAssistResultDto`
- [ ] Body risposta contiene: `generatedText`, `tokensUsed > 0`, `estimatedCostUsd > 0`
- [ ] UI mostra preview in pannello laterale con badge token (es. "852 tokens · $0.0012")
- [ ] DB check — colonne aggiornate:
  ```sql
  SELECT "TotalTokensUsed", "EstimatedCostUsd"
  FROM mechanic_drafts
  WHERE "Id" = '<DRAFT_ID>';
  ```
  `TotalTokensUsed > 0`, `EstimatedCostUsd > 0`.

**🔴 Validazione Copyright Firewall (critica)**
- [ ] Il prompt AI inviato **NON** deve contenere testo estratto dal PDF (solo `humanNotes` + `gameTitle`)
- [ ] Verifica via log API:
  ```bash
  docker logs meepleai-api --tail=100 | grep -i "ai-assist\|prompt"
  ```
  Il prompt deve referenziare solo `HumanNotes`, mai `PdfContent`/`ExtractedText`.

---

### TC-04 — Accept draft AI

**Azione**
1. Dal preview di TC-03, click "Accept"

**Atteso**
- [ ] `POST /api/v1/admin/mechanic-extractor/accept-draft` → **200 OK**
- [ ] Textarea Summary si aggiorna con `acceptedDraft` (contenuto AI)
- [ ] Badge verde "Accepted" appare accanto al tab

---

### TC-05 — Ripeti AI Assist + Accept per altre sezioni

Eseguire TC-03 + TC-04 per: **Mechanics**, **Victory**, **Resources**, **Phases**, **FAQ**.

**Atteso**
- [ ] 6 sezioni con "Accepted" badge
- [ ] `TotalTokensUsed` cumulativo aggiornato ad ogni AI call (verifica SQL)

---

### TC-06 — Concorrenza RowVersion (409 Conflict)

**Azione**
1. Apri **tab A** sul draft corrente
2. Apri **tab B** sullo stesso draft (stesso URL)
3. In tab A modifica Summary → salva (auto-save)
4. In tab B modifica Summary (senza refresh) → salva

**Atteso**
- [ ] Tab A: `POST /draft` → **200 OK**
- [ ] Tab B: `POST /draft` → **409 Conflict** (RowVersion mismatch)
- [ ] UI tab B mostra errore "Draft modified elsewhere, refresh required"

---

### TC-07 — Review page con stats

**Azione**
1. Dall'editor click "Preview & Export" (o navigare a `/admin/knowledge-base/mechanic-extractor/review?sharedGameId=<ID>&pdfDocumentId=<ID>`)

**Atteso**
- [ ] Pagina review carica senza errori
- [ ] Stats bar visibile: `6 sezioni completate`, `N meccaniche`, `M risorse`, `X Token AI utilizzati`
- [ ] 6 sezioni renderizzate con contenuti AI accettati
- [ ] Footer copyright notice presente
- [ ] Nessun testo dal PDF originale visibile (solo contenuti generati dal flow Variant C)

---

### TC-08 — Export PDF (window.print)

**Azione**
1. Dalla review page click "Export PDF" (o `Ctrl+P`)

**Atteso**
- [ ] Finestra stampa si apre con layout ottimizzato (header, sezioni, footer)
- [ ] Salvataggio come PDF produce file leggibile
- [ ] Paginazione corretta (no overflow, no tagli di sezioni)

---

### TC-09 — Finalize → RulebookAnalysis

**Azione**
1. Dalla editor page click "Finalize"
2. Confermare nel dialog (se presente)

**Atteso**
- [ ] `POST /api/v1/admin/mechanic-extractor/finalize` → **201 Created** con header `Location: /api/v1/admin/mechanic-extractor/analysis/<ID>`
- [ ] Body risposta con `id`, `sharedGameId`, `pdfDocumentId`
- [ ] DB check:
  ```sql
  SELECT "Id", "GenerationSource", "CreatedAt"
  FROM rulebook_analyses
  WHERE "SharedGameId" = '<SHARED_GAME_ID>'
  ORDER BY "CreatedAt" DESC LIMIT 1;
  ```
  Record presente con `GenerationSource = 'Manual'` (Variant C).

---

### TC-10 — Token tracking nel time

**Azione**
1. Dopo TC-05 e TC-09, verificare totali persistiti

**Atteso**
- [ ] Review page mostra `TotalTokensUsed` identico al valore DB
- [ ] `EstimatedCostUsd` coerente con pricing DeepSeek/OpenRouter (~$0.14/M input tokens)
- [ ] Nessun overflow/reset token counter

---

### TC-11 — Admin-only enforcement

**Azione**
1. Logout admin, login come user non-admin (es. ruolo `User`)
2. Navigare a `/admin/knowledge-base/mechanic-extractor`

**Atteso**
- [ ] Redirect o 403 Forbidden dal middleware
- [ ] `GET /api/v1/admin/mechanic-extractor/draft` → **403** (filter `RequireAdminSessionFilter` attivo)

---

### TC-12 — Edge case: game senza PDF

**Azione**
1. Login admin
2. Editor → selezionare un game **senza** PDF linkato

**Atteso**
- [ ] Dropdown PDF vuoto o disabilitato
- [ ] Nessun crash, nessuna chiamata `/draft` inviata
- [ ] Messaggio UI "No PDF available for this game"

---

## Known Limitations (non blockers per smoke)

- Review page usa `window.print()` → fedeltà export dipende dal browser (Chrome consigliato)
- Auto-save timer non esplicito (check codice per soglia debounce)
- `EstimatedCostUsd` dipende da tariffa LLM provider corrente (verifica env `LLM_PROVIDER`)
- Session note + house rules non inclusi nel flow Mechanic Extractor (bounded context diverso)

---

## Rollback (se smoke fallisce)

Se un test critico fallisce in staging:

```bash
# 1. Backup pre-test (se non fatto)
cd /opt/meepleai/repo/infra && bash scripts/backup.sh

# 2. Rimuovere draft corrotti
docker exec meepleai-postgres psql -U meepleai -c \
  "DELETE FROM mechanic_drafts WHERE \"SharedGameId\" = '<ID>';"

# 3. Rimuovere analysis finalizzate spurie
docker exec meepleai-postgres psql -U meepleai -c \
  "DELETE FROM rulebook_analyses WHERE \"SharedGameId\" = '<ID>' AND \"GenerationSource\" = 'Manual';"
```

---

## Sign-off

| Campo | Valore |
|-------|--------|
| Eseguito da | _____________________ |
| Data | _____________________ |
| Ambiente | dev / staging |
| Build/commit | `git rev-parse HEAD` → ________________ |
| Esito globale | ☐ PASS · ☐ FAIL · ☐ PARTIAL |
| Note | ______________________________________ |

### Criteri PASS

- Tutti i TC-01 → TC-12 con esito atteso
- Nessuna regressione su token tracking o copyright firewall (TC-03, TC-10)
- Finalize produce `RulebookAnalysis` valida (TC-09)

### Criteri FAIL

- Qualsiasi 5xx non gestito dal backend
- Token counter sballato (negativo o non coerente)
- Prompt AI contiene testo del PDF (violazione Variant C)
- 409 RowVersion non gestito lato client (tab B in stato inconsistente)
