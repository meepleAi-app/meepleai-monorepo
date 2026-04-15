# Batch Embedding + RAG Test + Snapshot

**Data**: 2026-04-14
**Approccio**: A (Dev stack + import a gruppi + dump finale)

## Obiettivo

Embeddare tutti i ~120 PDF rulebook in `data/rulebook/`, testare il RAG manualmente per ogni gioco, e creare uno snapshot DB per il seeding.

## Stato attuale

- 120+ PDF in `data/rulebook/`
- Snapshot baseline: 14 Ready, 122 Failed, 2076 chunks
- Script `import-games.sh` esistente per la pipeline completa
- Workflow snapshot: `make seed-index` / `seed-index-dump.sh`

## Fase 1: Diagnosi fallimenti

1. Avvio stack con `make dev`
2. Query `processing_jobs` per classificare errori dei 122 falliti:
   ```sql
   SELECT pj.status, pj.error_message, pd.file_name
   FROM processing_jobs pj
   JOIN pdf_documents pd ON pd.id = pj.pdf_document_id
   WHERE pj.status = 'Failed'
   ORDER BY pj.updated_at DESC;
   ```
3. Classificazione per categoria: timeout, extraction failure, embedding service down, OOM, PDF corrotto
4. Fix mirati per ogni categoria
5. Retry: `POST /api/v1/admin/pdfs/process-pending`

## Fase 2: Grouping dei PDF

8 gruppi da ~15 PDF, ordinati per complessità crescente:

| Gruppo | Tipo | Pagine |
|--------|------|--------|
| 1 | Piccoli/semplici | < 10 pp |
| 2 | Medi classici | 10-20 pp |
| 3 | Medi strategici | 10-20 pp |
| 4 | Complessi | 20-40 pp |
| 5 | Molto complessi | 40-60 pp |
| 6 | Mega | 60+ pp |
| 7 | Card games / LCG | variabile |
| 8 | Residui + retry falliti | variabile |

Logica: partire dai piccoli per validare la pipeline, poi scalare.

### Ciclo per gruppo

```
Upload PDF (import-games.sh per ~15 giochi)
  -> Poll status fino a Ready/Failed
  -> Test RAG manuale nel browser
  -> Annota risultati
  -> Gruppo successivo
```

## Fase 3: Test RAG manuale

Per ogni gioco Ready:

1. Apri `http://localhost:3000` -> naviga al gioco
2. Avvia chat con agente RAG
3. Domanda standard: "Quanti giocatori possono giocare? Come si prepara il gioco?"
4. Verifica:
   - Risposta pertinente al gioco corretto
   - Almeno 1 citazione dal PDF corretto
   - Contenuto coerente con le regole reali

### Criteri pass/fail

- **Pass**: risposta pertinente + citazioni dal PDF corretto
- **Fail**: risposta generica, citazioni vuote/sbagliate, o errore

### Tracking

Risultati in `data/rulebook/rag-test-results.md`:

```markdown
| Gioco | Gruppo | Embedding | RAG Test | Note |
|-------|--------|-----------|----------|------|
| catan | 2 | Ready | Pass | |
```

### Fallback

Se il browser non e' pratico: `POST /api/v1/knowledge-base/ask` via curl, verifica `citations` non vuote.

## Fase 4: Snapshot finale

### Pre-condizioni

- Tasso fallimento < 15% (< 18 PDF su 120)
- Test RAG superati per tutti i giochi Ready

### Procedura

1. Verifica stato: `SELECT status, COUNT(*) FROM processing_jobs GROUP BY status;`
2. Esegui `seed-index-dump.sh` da `infra/`:
   - Produce `.dump`, `.meta.json`, `.sha256` in `data/snapshots/`
3. Aggiorna `.latest`
4. Opzionale: `make seed-index-publish` per upload su bucket S3

### Validazione

- Confronto `chunk_count` e `embedding_count` nel `.meta.json`
- Test `make dev-from-snapshot-force` su ambiente pulito per confermare restore

## Stima tempi

| Fase | Durata stimata |
|------|---------------|
| Diagnosi | 30-60 min |
| Gruppo 1 (piccoli) | 30-45 min (embed) + 15 min (test) |
| Gruppo 2-3 (medi) | 45-60 min ciascuno + 15 min test |
| Gruppo 4-5 (complessi) | 60-90 min ciascuno + 15 min test |
| Gruppo 6 (mega) | 90-120 min + 15 min test |
| Gruppo 7-8 (residui) | 60-90 min + 15 min test |
| Snapshot | 15-30 min |
| **Totale** | **8-14 ore** (distribuibili su piu' sessioni) |
