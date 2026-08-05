# Image-Table VLM Extraction — Activation Runbook (#3435)

Operational runbook to **turn on** the async VLM image-table extraction pipeline of epic
[#3435](https://github.com/meepleAi-app/meepleai-monorepo/issues/3435) (Metà C) on an environment.
The pipeline extracts tables that live as **images** in rulebooks (not selectable text) into
retrievable, citation-groundable RAG chunks.

The feature is **code-complete and flag-gated OFF by default** — nothing runs until you follow this
runbook. Design: [`docs/superpowers/specs/2026-08-01-image-table-region-grounding-design.md`](../../superpowers/specs/2026-08-01-image-table-region-grounding-design.md).

| Sub-project | What | PR |
|---|---|---|
| SP1 | Image-region capture + auto ingestion (`pdf_image_regions`) | #3541 / #3547 |
| SP2 | Table-region router (candidate PDFs for the VLM) | #3553 |
| SP3 | smoldocling crop-discriminator `POST /api/v1/extract-image` | #3559 |
| SP4 | Async VLM table-extraction job + RAG table-chunk persistence (SP5 folded) | #3560 |
| — | Copyright-gate on the image-region viewer overlay (§5quinquies) | #3561 |
| — | Admin trigger for the table-extraction batch | #3562 |

---

## 1. What it does (pipeline recap)

```
pdf_image_regions (hi_res Image/FigureCaption bboxes, SP1)
   → GetTableRegionCandidatesQuery  (router: PDFs with ≥ N regions, SP2)
   → RunTableExtractionJob  (Quartz, [DisallowConcurrentExecution], SP4)
       per candidate PDF, per pending region:
         render page (Docnet/PDFium) + crop bbox (SkiaSharp)
         → POST /api/v1/extract-image  (smoldocling crop-discriminator, SP3)
            → is_table? (<otsl> gate) — illustrations are discarded (pre-filter / no-otsl)
         → if table: persist a table chunk
              text_chunks (ElementType='Table', bounding_boxes_json = region bbox)
              + pgvector_embeddings (source_chunk_id → the chunk, carries the region to the citation)
         → record per-region state in pdf_table_extractions (idempotency + retry-cap/dead-letter)
```

Per-region state (`pdf_table_extractions`) is keyed by a **bbox-stable region hash** (survives the
SP1 replace-by-pdf region re-seed). The job is idempotent + retry-capped; a document reindex that
wipes a produced chunk is self-healed on the next run (re-index from the cached markdown, no VLM re-run).

---

## 2. Preconditions

- [ ] **A GPU node for smoldocling.** The 256M model runs ~1.7–3s per table crop on an RTX 4070 but is
  impractical on CPU (>95s/page). The staging 8GB box is NOT adequate for the always-on service —
  run smoldocling on a GPU box (dedicated node, or a local GPU during a batch window; DC-E).
- [ ] **The corpus is indexed** (`IndexerVersion` set on Ready PDFs) — SP2's candidate selector
  requires it, and the RAG persistence reuses each PDF's existing `vector_documents` row.
- [ ] **Backend deployed with the #3435 code** (SP1–SP4 + gate, all on `main-dev`). Verify the target
  build actually contains it, e.g. `grep -c -a "run-table-extraction-batch" /app/Api.dll` in the API
  container — **merge ≠ deploy**; a flag can only activate code that is actually in the running build.
- [ ] **The SP4 migration is applied.** Staging does not auto-apply EF migrations, so
  `20260804173408_AddPdfTableExtractions` must be run manually (`pdf_table_extractions` + its two
  indexes + the `__EFMigrationsHistory` row). Check with
  `select to_regclass('pdf_table_extractions');` — a NULL means the batch will fail on every region.

---

## 3. Deploy smoldocling on GPU

The smoldocling service hosts `/api/v1/extract-image` (SP3) alongside `/api/v1/extract` etc. on
**port 8002**.

```bash
# From apps/smoldocling-service. IMPORTANT: rebuild from source — a locally-cached image predating
# #3559 returns HTTP 404 on /api/v1/extract-image. The model-download layer is cached, so the COPY src
# rebuild is fast.
docker build -t meepleai-smoldocling-service:latest apps/smoldocling-service

# GPU run (mirror of infra/compose.smoldocling.gpu.local.yml: DEVICE=cuda + nvidia reservation)
docker run -d --name meepleai-smoldocling --gpus all -e DEVICE=cuda -p 8002:8002 \
  meepleai-smoldocling-service:latest

# Wait ~50-70s for model warmup, then confirm health + the endpoint exists.
curl -s http://localhost:8002/health          # -> "status":"healthy", "model_initialized":"ok", gpu_info
curl -s -o /dev/null -w '%{http_code}\n' -X POST http://localhost:8002/api/v1/extract-image  # 422 (needs a file) — NOT 404
```

**Smoke test the endpoint** (a clean B/W table crop must return `is_table:true` + `<otsl>` markdown; a
saturated illustration must be pre-filtered):

```python
import io, httpx, numpy as np
from PIL import Image, ImageDraw
def table():
    im=Image.new("RGB",(602,242),"white"); d=ImageDraw.Draw(im)
    for r in range(5): d.line([(0,r*60),(600,r*60)],fill="black",width=2)
    for c in range(4): d.line([(c*200,0),(c*200,240)],fill="black",width=2)
    for r,row in enumerate([["Risorsa","Costo","Punti"],["Legno","2","1"],["Argilla","3","2"],["Grano","1","3"]]):
        for c,v in enumerate(row): d.text((c*200+12,r*60+22),v,fill="black")
    return im
buf=io.BytesIO(); table().save(buf,"PNG")
print(httpx.post("http://localhost:8002/api/v1/extract-image",
      files={"image":("t.png",buf.getvalue(),"image/png")},timeout=180).json())
# -> {"is_table": true, "reason": "table-otsl", "markdown": "| Risorsa | Costo | Punti | ...", "bbox":[...], ...}
```

Point the backend at it: config key **`PdfProcessing:Extractor:SmolDocling:ApiUrl`** (default
`http://smoldocling-service:8002`).

---

## 4. Ensure image-regions are seeded (SP1)

The VLM job only sees PDFs that already have rows in `pdf_image_regions`. If the corpus was seeded
during SP1 validation you can skip this; otherwise enable seeding and run a batch:

```
# config
PdfProcessing:ImageRegionSeeding:Enabled = true       # default false
PdfProcessing:ImageRegionSeeding:IntervalMinutes = 30 # Quartz auto-fire (or trigger manually below)
```

```bash
# manual trigger (admin session)
POST /api/v1/admin/pdfs/maintenance/seed-image-regions-batch   {"batchSize": 3}
```

hi_res is ~200-300s/PDF and memory-heavy — seed on a box with headroom, small batches. Verify:

```sql
SELECT COUNT(*) AS regions, COUNT(DISTINCT pdf_document_id) AS pdfs FROM pdf_image_regions;
SELECT element_type, COUNT(*) FROM pdf_image_regions GROUP BY element_type;  -- 'Image' | 'FigureCaption'
```

> **Reality check**: most detected regions are illustrations/photos/cards, NOT tables. The `<otsl>`
> gate (SP3) and the colorfulness pre-filter discard non-tables, so a batch may report mostly
> `not_table`. To validate the happy path you need a PDF known to contain a real image-table (see the
> [golden set](../testing/image-table-golden-set.md)).

Confirm the router sees candidates (read-only, no side effects):

```
GET /api/v1/admin/pdfs/maintenance/table-region-candidates?minImageRegions=1&limit=50
```

---

## 5. Enable + run the table-extraction pass (SP4)

```
PdfProcessing:TableExtraction:Enabled            = true   # default false — THE master switch
PdfProcessing:TableExtraction:BatchSize          = 10     # regions processed per run
PdfProcessing:TableExtraction:DelayMs            = 200    # pacing between regions (throttles the GPU)
PdfProcessing:TableExtraction:MaxAttempts        = 3      # per-region retries before dead-letter
PdfProcessing:TableExtraction:IntervalMinutes    = 30     # Quartz cadence (set to 1 for a fast test)
PdfProcessing:TableExtraction:VlmTimeoutSeconds  = 120    # /extract-image HTTP timeout
```

Trigger on demand (recommended for the first run so you can inspect the result immediately):

```
POST /api/v1/admin/pdfs/maintenance/run-table-extraction-batch   {"batchSize": 10}
# -> { "enabled": true, "processed": N, "extracted": X, "notTable": Y, "failed": Z }
```

…or let the Quartz job fire on its interval (no-op while the flag is off).

---

## 6. Verify

```sql
-- Per-region outcomes
SELECT status, COUNT(*) FROM pdf_table_extractions GROUP BY status;
-- 'extracted' | 'not_table' | 'failed' | 'dead_letter' | 'pending'

-- Extracted table chunks are in the RAG corpus (retrievable + groundable)
SELECT COUNT(*) FROM text_chunks WHERE element_type = 'Table';

-- Each table chunk MUST have a pgvector row wired via source_chunk_id (else it's FTS-only, no vector recall)
SELECT COUNT(*) AS table_chunks,
       COUNT(*) FILTER (WHERE e.source_chunk_id IS NOT NULL) AS with_embedding
FROM text_chunks tc
LEFT JOIN pgvector_embeddings e ON e.source_chunk_id = tc.id
WHERE tc.element_type = 'Table';

-- The region bbox rode onto the chunk (draws the citation region)
SELECT id, page_number, LEFT(content, 60) AS md, bounding_boxes_json
FROM text_chunks WHERE element_type = 'Table' LIMIT 5;
```

Then ask a question whose answer is in an extracted table and confirm the grounded answer cites the
table + highlights its region (SP6 is by-construction: the table chunk's `bounding_boxes_json` flows
through `source_chunk_id` → `GroundedAnswerService` Full-gate → `CitationDto.Regions` → the FE
`PdfBBoxOverlay`, exactly like a narrative citation). Quality-validate against the
[golden set](../testing/image-table-golden-set.md).

---

## 7. Monitoring

Prometheus counters (§8 NFR4):

```promql
# Give-ups (a region exhausted its retry budget) — investigate the PDF / the VLM service
increase(meepleai_table_vlm_total{outcome="dead_letter"}[1h]) > 0

# Broadly-failing VLM pass (service down / GPU OOM / timeouts)
rate(meepleai_table_vlm_total{outcome="failed"}[15m]) / rate(meepleai_table_vlm_total[15m]) > 0.5

# Throughput
sum by (outcome) (increase(meepleai_table_vlm_total[1h]))   # extracted | not_table | failed | dead_letter
```

`outcome` semantics mirror `meepleai_image_region_seed_total`: **`failed`** is transient/retry-eligible,
**`dead_letter`** is terminal. Alert on `dead_letter`, not `failed`.

---

## 8. Rollout / re-extract existing corpus (SP7)

Turning the feature on only processes candidate PDFs going forward. To backfill table content into the
**existing** corpus, bump the indexer version so table-heavy PDFs re-run:

1. Bump **`IndexerVersionRegistry`** (the corpus now carries table content — a new semantic version).
2. Re-extract the table-heavy PDFs (from the SP2 candidate list) via the reindex path — targeted, not
   the whole corpus:
   ```
   POST /api/v1/admin/pdfs/{pdfId}/reindex        # ReindexDocumentCommand, per candidate PDF
   ```
   or the corpus drain loop from the [corpus reindex runbook](./2026-07-26-corpus-reindex-runbook.md).
3. The table-extraction job re-runs against the re-seeded regions. **Idempotency**: a reindex deletes a
   PDF's `text_chunks` (incl. the table chunk) + all its embeddings; the SP4 job **self-heals** —
   `pdf_table_extractions` still holds the extracted markdown, so the next run re-indexes the table
   from cache **without** re-cropping / re-calling the VLM (only regions whose chunk is actually gone).

---

## 9. Rollback

- Set `PdfProcessing:TableExtraction:Enabled = false`. The Quartz job becomes a one-config-check no-op.
- Persisted table chunks + `pdf_table_extractions` rows **remain** (they are valid RAG content). To
  remove them, delete `text_chunks WHERE element_type='Table'` + their pgvector rows (by
  `source_chunk_id`) for the affected PDFs, and the `pdf_table_extractions` rows.
- The image-region seeding (SP1) is a separate flag (`PdfProcessing:ImageRegionSeeding:Enabled`).

---

## 10. Gotchas

- **Stale smoldocling image** → 404 on `/api/v1/extract-image`. Rebuild from source (§3).
- **GPU OOM / init failure** → the endpoint degrades to a 200 non-table (`reason: init-failed`, R5); the
  job records a transient failure and retries → dead-letters after `MaxAttempts`. Not a 500.
- **Most regions are illustrations** → high `not_table` is normal; the pre-filter + `<otsl>` gate are
  doing their job. `duration_ms: 0` on a `prefilter-colorful` region means the VLM was correctly
  skipped.
- **Copyright**: table chunks inherit the parent PDF's tier — a `Protected` PDF's verbatim table +
  region are auto-hidden at answer time (Full-gate). The image-region viewer overlay is likewise
  Full-gated (#3561). No per-chunk tier wiring needed.
- **`no_repeat_ngram_size` is forbidden** in the discriminator (it fabricates spurious tables). If you
  tune the VLM, keep the repetition early-stop, not `no_repeat`.

---

**Golden-set validation**: [`image-table-golden-set.md`](../testing/image-table-golden-set.md) · **Design**:
[§10](../../superpowers/specs/2026-08-01-image-table-region-grounding-design.md).
