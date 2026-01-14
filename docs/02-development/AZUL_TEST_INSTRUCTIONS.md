# Test Azul RAG End-to-End - Instructions

## Overview

After implementing batch processing for PDF embeddings, Azul (2.1MB) can now be fully processed and queried via RAG agent.

## Prerequisites

✅ **Completed** (already committed):
1. BGG authentication configured
2. Batch embedding processing implemented
3. SharedGameSeeder with Azul mapping
4. AsTracking() persistence fix
5. pdf-uploads volume configured
6. Memory optimized to 4GB

## Step-by-Step Test Procedure

### 1. Restart Development Environment

```bash
cd infra
docker compose down
docker compose up -d

# Wait for healthy status (~30 seconds)
docker compose ps
```

**Expected**:
- ✅ Admin user created
- ✅ Demo users created
- ✅ **9 SharedGames seeded** (including Azul)

### 2. Verify Azul in SharedGameCatalog

```bash
docker compose exec postgres psql -U postgres -d meepleai -c \
  "SELECT id, title, bgg_id, rules_language FROM shared_games WHERE title = 'Azul';"
```

**Expected**:
```
id                  | title | bgg_id | rules_language
--------------------|-------|--------|---------------
<guid>              | Azul  | 230802 | en
```

### 3. Add Azul to User Library

**Via Browser** (http://localhost:3000):
1. Login as: `admin@meepleai.dev` / `Admin123!ChangeMe`
2. Navigate to: Games → Add Game
3. Search: "Azul"
4. Click: "Aggiungi alla Collezione" (from catalog, not BGG)

**Via API**:
```bash
# Login
curl -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@meepleai.dev","password":"Admin123!ChangeMe"}' \
  -c cookies.txt

# Get Azul SharedGame ID
SHARED_GAME_ID=$(docker compose exec postgres psql -U postgres -d meepleai -t -c \
  "SELECT id FROM shared_games WHERE title = 'Azul';" | tr -d ' ')

# Get full details
curl "http://localhost:8080/api/v1/shared-games/${SHARED_GAME_ID}" \
  -b cookies.txt

# Add to collection (auto-creates game)
curl -X POST http://localhost:8080/api/v1/games \
  -H "Content-Type: application/json" \
  -b cookies.txt \
  -d "{
    \"title\": \"Azul\",
    \"publisher\": \"Plan B Games\",
    \"yearPublished\": 2017,
    \"minPlayers\": 2,
    \"maxPlayers\": 4,
    \"minPlayTimeMinutes\": 30,
    \"maxPlayTimeMinutes\": 45,
    \"bggId\": 230802,
    \"sharedGameId\": \"${SHARED_GAME_ID}\"
  }"

# Save game ID from response
GAME_ID="<game-id-from-response>"
```

### 4. Upload Azul PDF

```bash
# Upload PDF (2.1MB)
curl -X POST "http://localhost:8080/api/v1/ingest/pdf" \
  -b cookies.txt \
  -F "file=@data/rulebook/azul_rulebook.pdf" \
  -F "gameId=${GAME_ID}" \
  -F "language=en"

# Save PDF ID from response
PDF_ID="<pdf-id-from-response>"
```

**Expected**:
```json
{
  "success": true,
  "documentId": "<pdf-id>",
  "fileName": "azul_rulebook.pdf"
}
```

### 5. Monitor Background Processing

**Watch Logs** (in separate terminal):
```bash
docker compose logs -f api | grep -E "PDF-DEBUG|BATCH-EMBED|Background task"
```

**Expected Log Sequence**:
```
🔄 [PDF-DEBUG] ProcessPdfAsync START for PDF {pdfId}
🔍 [PDF-DEBUG-VALIDATE] START validation
✅ [PDF-DEBUG-VALIDATE] Validation passed, Status: processing
📄 [PDF-DEBUG] Step 1: Starting ExtractPdfContentAsync
✅ [PDF-DEBUG] Extraction SUCCESS: ~X chars, Y pages
✂️ [PDF-DEBUG] Step 2: Starting ChunkExtractedTextAsync
✅ [PDF-DEBUG] Chunking SUCCESS: ~10 chunks created
🧠 [BATCH-EMBED] Starting batch embedding: 10 chunks, batch size: 20
📦 [BATCH-EMBED] Processing batch 1/1: 10 chunks
✅ [BATCH-EMBED] Batch 1/1 completed: 10 embeddings
✅ [BATCH-EMBED] All batches completed: 10 total embeddings
🔍 [PDF-DEBUG] Step 4: Starting IndexInVectorStoreAsync
✅ [PDF-DEBUG] Indexing completed
🎉 [PDF-DEBUG] Step 5: Finalizing processing
✅ [PDF-DEBUG] ProcessPdfAsync COMPLETE
```

**Check Database**:
```bash
docker compose exec postgres psql -U postgres -d meepleai -c \
  "SELECT processing_status, page_count, character_count
   FROM pdf_documents WHERE id = '${PDF_ID}';"
```

**Expected**:
```
processing_status | page_count | character_count
------------------|------------|----------------
completed         | ~8-12      | ~15000-25000
```

**Check Qdrant Vectors**:
```bash
curl "http://localhost:6333/collections/meepleai_documents/points/count"
```

**Expected**: Count > 0 (10+ vectors for Azul)

### 6. Test RAG Agent

**Via API**:
```bash
curl -X POST "http://localhost:8080/api/v1/agents/qa" \
  -H "Content-Type: application/json" \
  -b cookies.txt \
  -d "{
    \"gameId\": \"${GAME_ID}\",
    \"query\": \"How do I set up Azul? What pieces do I need?\",
    \"searchMode\": \"hybrid\"
  }"
```

**Expected Response**:
```json
{
  "answer": "To set up Azul, each player takes a player board...",
  "snippets": [
    {
      "text": "Setup: Place the factory displays...",
      "page": 2,
      "score": 0.85
    }
  ],
  "confidence": 0.8,
  "totalTokens": 250
}
```

**Via Browser**:
1. Navigate to game page: `http://localhost:3000/games/${GAME_ID}`
2. Click "Chat" or "Ask Question"
3. Type: "How do I set up Azul?"
4. Verify: Answer references rulebook pages, confidence > 0.7

## Performance Expectations

**Azul PDF (2.1MB)**:
- Upload: ~2-3 seconds
- Extraction: ~5-10 seconds
- Chunking: ~1 second
- **Batch Embedding**: ~10-15 seconds (1 batch of 10 chunks)
- Indexing: ~5 seconds
- **Total**: ~25-35 seconds

**Memory Usage**:
- Peak during embeddings: ~1GB (20 chunk batch)
- Well within 4GB container limit
- Should NOT see OutOfMemoryException

## Troubleshooting

### If Seed Doesn't Create Azul

```bash
# Manually trigger seed
docker compose exec api dotnet /app/Api.dll --seed-shared-games

# Or insert manually
docker compose exec postgres psql -U postgres -d meepleai -c \
  "INSERT INTO shared_games (id, title, bgg_id, year_published, description,
    min_players, max_players, playing_time_minutes, min_age,
    image_url, thumbnail_url, status, rules_language, created_by, created_at)
   VALUES (
     gen_random_uuid(),
     'Azul',
     230802,
     2017,
     'Abstract tile-placement game',
     2, 4, 30, 8,
     'https://via.placeholder.com/400x300?text=Azul',
     'https://via.placeholder.com/150x150?text=Azul',
     1,
     'en',
     (SELECT id FROM users WHERE role = 'admin' LIMIT 1),
     NOW()
   );"
```

### If Processing Fails

**Check logs for batch processing**:
```bash
docker compose logs api | grep "BATCH-EMBED"
```

**If still OOM**:
- Verify batch size: Should be 20 (check logs)
- Verify GC between batches: Check logs for GC calls
- Increase container memory if needed (edit docker-compose.yml)

### If RAG Returns "Not specified"

**Check vectors exist**:
```bash
# Check vector count
curl http://localhost:6333/collections/meepleai_documents/points/count

# Search vectors directly
curl -X POST http://localhost:6333/collections/meepleai_documents/points/search \
  -H "Content-Type: application/json" \
  -d "{
    \"vector\": [0.1, 0.2, ...],  # Use actual embedding
    \"limit\": 5
  }"
```

**Check text chunks**:
```bash
docker compose exec postgres psql -U postgres -d meepleai -c \
  "SELECT COUNT(*) FROM text_chunks WHERE pdf_document_id = '${PDF_ID}';"
```

## Success Criteria

- [x] SharedGame "Azul" exists in catalog
- [ ] User can add Azul to library
- [ ] azul_rulebook.pdf uploads successfully
- [ ] Extraction completes (ProcessingStatus = 'completed')
- [ ] **Batch embedding completes** (no OOM, ~1 batch)
- [ ] Indexing saves vectors to Qdrant
- [ ] RAG agent answers setup questions with >0.7 confidence
- [ ] Answer references rulebook pages

## Next Steps After Success

1. **Test other PDFs**:
   - Pandemic (8.9MB) - Should use ~2 batches
   - Wingspan (4.8MB) - Should use ~1 batch
   - Ticket to Ride (1.7MB) - Should use ~1 batch

2. **Test larger PDFs** (now supported):
   - Catan (11.1MB) - Should use ~3 batches
   - Barrage (20.4MB) - Should use ~5 batches

3. **Validate memory usage**:
   ```bash
   docker stats meepleai-api --no-stream
   ```
   - Should stay under 2GB during processing

4. **Performance benchmarks**:
   - Measure time per batch
   - Compare before/after memory profiles

---

**Created**: 2026-01-14
**Related Commit**: 591e9eac4
**Status**: Ready for testing (Docker restart required)
