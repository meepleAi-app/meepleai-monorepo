# ADR-003: PDF Processing Pipeline with Vision-Language Models

**Status**: ⚠️ SUPERSEDED by ADR-003b (Unstructured Library)
**Date**: 2025-01-15
**Superseded Date**: 2025-01-15
**Deciders**: CTO, Backend Lead
**Context**: Phase 1 Rulebook Indexing

**Note**: This ADR documented the original plan to use LLMWhisperer as Stage 1. This has been replaced with Unstructured library (Apache 2.0, open source) to eliminate API costs and vendor lock-in. See [ADR-003b](./adr-003b-unstructured-pdf.md) for the current implementation.

---

## Context

Board game rulebooks present unique PDF processing challenges:
- **Multi-column layouts**: 2-3 columns with complex flow
- **Borderless tables**: Rules organized in tables without visible borders
- **Mixed content**: Text + diagrams + images + icons overlapping
- **Text overlap**: Columns bleeding into each other (OCR confusion)
- **Deep hierarchies**: Nested sections, subsections, callout boxes

**Research Foundation** (documento riga 38-42):
> "PDF processing rappresenta collo di bottiglia tecnico per rulebook complessi. Sfide specifiche: layout multi-colonna, gerarchie profondamente ramificate, contenuto misto, tabelle borderless, testo sovrapposto tra colonne."

**State of the Art**:
- Traditional OCR (Tesseract): Struggles with multi-column, low accuracy on complex layouts
- Template-based parsing: Requires template per rulebook format (not scalable)
- Vision-Language Models: Emerging solution (SmolDocling, dots.ocr) - direct image understanding

**Requirements**:
- Extract text preserving logical reading order (not spatial order)
- Handle scanned PDFs (images) and native PDFs (text layer)
- Free or low-cost for MVP (100 pages/day budget)
- Fallback chain (robust to PDF quality variations)

---

## Decision

Implement **Three-Stage Fallback Pipeline**: LLMWhisperer → SmolDocling → dots.ocr

### Stage 1: LLMWhisperer (Primary)

**Purpose**: Preserve layout formatting for LLM consumption

**Strengths**:
- Designed specifically for LLM processing (maintains structure)
- Handles multi-column layouts intelligently (reading order preserved)
- Works on native PDFs (with text layer) AND scanned PDFs
- Free tier: 100 pages/day (sufficient for MVP indexing)

**Limitations**:
- Free tier limited (100 pages/day = ~4 rulebooks/day at 24 pages avg)
- Scanned PDF quality dependent
- Fails on extremely complex layouts (fallback needed)

**Usage**:
```python
import llmwhisperer

client = llmwhisperer.LLMWhispererClient(api_key=os.getenv('LLMWHISPERER_API_KEY'))

# Extract text
result = client.extract(
    pdf_path="terraforming_mars_it.pdf",
    mode="layout_preserving",  # vs "text_only"
    timeout_seconds=120
)

print(result.text)  # Markdown with layout annotations
print(f"Quality score: {result.quality}")  # 0.0-1.0
print(f"Method: {result.extraction_method}")  # "text_layer" or "ocr"
```

**Fallback Trigger**: Timeout (>120s), API error (rate limit), quality score <0.50

---

### Stage 2: SmolDocling (Secondary)

**Purpose**: Vision-language model for direct image understanding

**Strengths**:
- 256M parameter model (lightweight, fast inference)
- Converts documents in seconds (~10s per page)
- Outputs HTML/Markdown/JSON (structured)
- Open-source (MIT license, self-hostable)
- No OCR step (vision encoder processes images directly)

**Limitations**:
- Requires GPU for reasonable speed (CPU: ~60s per page)
- Model download: ~1 GB (one-time)

**Usage**:
```python
from docling import DocumentConverter

converter = DocumentConverter()

# Extract from PDF
result = converter.convert("terraforming_mars_it.pdf")

# Access as Markdown
markdown = result.document.export_to_markdown()

# Access structured JSON
json_output = result.document.export_to_json()
print(json_output['sections'])  # Hierarchical structure
```

**Fallback Trigger**: GPU unavailable, timeout (>300s), extraction failed

---

### Stage 3: dots.ocr (Tertiary)

**Purpose**: Multilingual OCR with layout parsing

**Strengths**:
- Multilingual support (Italian included)
- Layout analysis (bounding boxes, reading order)
- HTML/JSON output (structured)
- Open-source (Apache 2.0)

**Limitations**:
- OCR quality dependent on scan quality
- Slower than vision-language models (~30s per page)

**Usage**:
```python
from dots_ocr import OCRProcessor

processor = OCRProcessor(language='it')

# Extract from scanned PDF
result = processor.process_pdf("scanned_rulebook.pdf")

# Access as HTML with bounding boxes
html = result.to_html(include_bboxes=True)

# Access as structured JSON
json_output = result.to_json()
```

**Final Fallback**: If all 3 stages fail, raise `ProcessingError` and notify admin

---

## Fallback Chain Implementation

```python
# services/pdf_processing.py
class PdfProcessingService:
    def __init__(self):
        self.llmwhisperer = LLMWhispererClient(api_key=os.getenv('LLMWHISPERER_API_KEY'))
        self.smoldocling = DocumentConverter()
        self.dots_ocr = OCRProcessor(language='it')

    async def process(self, pdf_path: str, game_id: str) -> ProcessingResult:
        logger.info(f"Processing {pdf_path} for game {game_id}")

        # Stage 1: LLMWhisperer (primary)
        try:
            logger.info("Attempting Stage 1: LLMWhisperer")
            result = self.llmwhisperer.extract(
                pdf_path=pdf_path,
                timeout_seconds=120
            )

            if result.quality >= 0.50:
                logger.info(f"Stage 1 success (quality: {result.quality})")
                return ProcessingResult(
                    text=result.text,
                    method='llmwhisperer',
                    quality=result.quality,
                    page_count=result.page_count
                )
            else:
                logger.warning(f"Stage 1 quality too low ({result.quality}), falling back")

        except Exception as e:
            logger.warning(f"Stage 1 failed: {e}, falling back to Stage 2")

        # Stage 2: SmolDocling (fallback)
        try:
            logger.info("Attempting Stage 2: SmolDocling")
            result = self.smoldocling.convert(pdf_path)
            text = result.document.export_to_markdown()

            if len(text) >= 100:  # Minimum text threshold
                logger.info(f"Stage 2 success ({len(text)} chars extracted)")
                return ProcessingResult(
                    text=text,
                    method='smoldocling',
                    quality=0.75,  # Estimated (no built-in quality score)
                    page_count=len(result.document.pages)
                )
            else:
                logger.warning(f"Stage 2 extracted insufficient text ({len(text)} chars)")

        except Exception as e:
            logger.warning(f"Stage 2 failed: {e}, falling back to Stage 3")

        # Stage 3: dots.ocr (final fallback)
        try:
            logger.info("Attempting Stage 3: dots.ocr")
            result = self.dots_ocr.process_pdf(pdf_path)
            text = result.to_text()

            if len(text) >= 100:
                logger.info(f"Stage 3 success ({len(text)} chars extracted)")
                return ProcessingResult(
                    text=text,
                    method='dots_ocr',
                    quality=0.60,  # OCR typically lower quality
                    page_count=result.page_count
                )
            else:
                raise InsufficientTextError(f"Extracted text too short: {len(text)} chars")

        except Exception as e:
            logger.error(f"Stage 3 failed: {e}")
            raise ProcessingError(f"All PDF processing stages failed for {pdf_path}") from e
```

---

## Consequences

### Positive

**✅ Robust to PDF Quality Variations**:
- Native PDFs (text layer): Stage 1 succeeds (~90% of modern rulebooks)
- Scanned PDFs (images): Stage 2/3 handle via vision/OCR
- Complex layouts: Vision-language models understand structure vs template-based OCR

**✅ Cost Control**:
- Stage 1 free tier: 100 pages/day (4 rulebooks × 25 pages avg) sufficient for MVP
- Stage 2/3 open-source: €0 ongoing cost (one-time setup, self-hosted)
- At scale (Phase 2): Self-host all stages, zero API dependency

**✅ Quality Assurance**:
- Quality scoring per stage (enables monitoring, alerts)
- Fallback chain ensures extraction success (even on poor scans)
- Manual review possible (admin dashboard shows low-quality extractions)

**✅ Future-Proof**:
- Vision-language models trend (eliminate OCR step entirely)
- Easy to add Stage 0: GPT-4 Vision (Phase 4, highest quality but expensive)

---

### Negative (Trade-offs)

**⚠️ Processing Time** (60-120s per rulebook):
- Stage 1 (LLMWhisperer): ~60s per rulebook (24 pages)
- Stage 2 (SmolDocling): ~240s (10s per page × 24)
- Stage 3 (dots.ocr): ~720s (30s per page × 24)

**Mitigation**:
- Async job queue (users don't wait, background processing)
- Progress updates (WebSocket notifications: "Elaborazione in corso... 40%")
- Acceptable for indexing (one-time cost per rulebook)

**⚠️ Free Tier Limits** (LLMWhisperer 100 pages/day):
- MVP: ~4 rulebooks/day max (25 pages avg)
- 10 games for MVP: 3 days indexing (acceptable)
- Phase 2 (50 games): 12.5 days (too slow)

**Mitigation**:
- Phase 2: Upgrade to LLMWhisperer paid tier ($29/month for 1,000 pages)
  OR self-host SmolDocling as primary (GPU instance: $50/month)

**⚠️ GPU Dependency** (SmolDocling, dots.ocr prefer GPU):
- CPU inference: 6x slower (~60s per page vs 10s)
- GPU instance cost: AWS g5.xlarge ~$1/hour (~$50/month continuous)

**Mitigation**:
- MVP (Phase 1): Use LLMWhisperer primarily, CPU fallback acceptable (infrequent)
- Phase 2: Add GPU instance for indexing jobs (cost justified by user growth)

---

## Alternatives Considered

### Alternative 1: Tesseract OCR Only (Rejected)

**Approach**: Traditional OCR (Tesseract) for all PDFs

**Pros**:
- Open-source, free, well-established
- Fast on CPU (~10s per page)

**Cons**:
- Poor multi-column handling (text extraction order wrong)
- Low accuracy on complex layouts (~60-70%)
- Requires preprocessing (deskew, denoise)
- No layout understanding (cannot distinguish header from body)

**Rejection Reason**: Insufficient quality for rulebook complexity

---

### Alternative 2: GPT-4 Vision API (Deferred to Phase 4)

**Approach**: Upload PDF pages as images to GPT-4 Vision, extract text via prompting

**Pros**:
- Highest quality (GPT-4 understands layout semantically)
- Handles tables, diagrams, mixed content excellently
- No local processing needed (API-based)

**Cons**:
- Cost: $0.01 per image (~$0.24 per 24-page rulebook)
- At scale (100 games): $24 one-time (acceptable)
- At scale (1000 games, Phase 4): $240 (acceptable for business model)
- Rate limits (50 images/min, need batching)

**Decision**: Defer to Phase 4 (add as Stage 0 for premium quality)

**Future Enhancement**:
- Premium tier: Re-index rulebooks with GPT-4 Vision (highest quality)
- Free tier: LLMWhisperer/SmolDocling (good enough for 80-90% accuracy)

---

### Alternative 3: Amazon Textract (Rejected)

**Approach**: AWS managed document analysis service

**Pros**:
- Excellent table extraction (identifies rows, columns, headers)
- Layout analysis (bounding boxes, reading order)
- Managed service (no infrastructure)

**Cons**:
- Cost: $1.50 per 1,000 pages (~$0.036 per rulebook)
- At scale (1000 games): $36 (acceptable)
- Vendor lock-in (AWS only)
- Complex pricing (different rates for forms, tables, queries)

**Rejection Reason**: Vendor lock-in + cost when open-source alternatives exist

---

## Monitoring & Quality Assurance

### Processing Quality Metrics

```python
# Prometheus metrics
pdf_processing_duration_seconds = Histogram('pdf_processing_duration_seconds', 'Processing time', ['method'])
pdf_processing_quality_score = Histogram('pdf_processing_quality_score', 'Quality score', ['method'])
pdf_processing_failures_total = Counter('pdf_processing_failures_total', 'Failures', ['method', 'reason'])
pdf_processing_method_usage = Counter('pdf_processing_method_usage', 'Method used', ['method'])
```

**Grafana Dashboard**:
- Processing time distribution (violin plot per method)
- Quality score distribution (histogram per method)
- Success rate by method (stacked bar: success, fallback, failure)
- Fallback cascade visualization (Sankey diagram)

---

### Quality Gate

**Minimum Quality Threshold**: Quality score ≥0.50 OR extracted text ≥100 characters

**Admin Review Trigger**:
- Quality <0.70: Flag for manual review (admin dashboard)
- All stages failed: Email alert to admin team
- Processing time >10min: Performance degradation alert

**Manual Intervention**:
- Admin uploads pre-processed text (fallback for problematic PDFs)
- Community contribution: Users can submit corrected text versions

---

## Migration to GPU-Accelerated Processing (Phase 2)

**Trigger**: >50 rulebooks indexed, free tier limits constrained

**Plan**:
1. Provision GPU instance (AWS g5.xlarge or equivalent)
2. Self-host SmolDocling as primary (Stage 1)
3. Keep LLMWhisperer as fallback (if SmolDocling fails)
4. dots.ocr remains final fallback (CPU-based)

**Expected Improvements**:
- Processing time: 240s → 60s per rulebook (4x faster)
- Quality: SmolDocling as primary → avg quality 0.80+ (vs 0.75 LLMWhisperer)
- Cost: $50/month GPU instance vs $29/month LLMWhisperer paid tier (acceptable)

---

## References

**Tools**:
- LLMWhisperer: https://llmwhisperer.com (free tier 100 pages/day)
- SmolDocling: https://github.com/DS4SD/docling (open-source, vision-language model)
- dots.ocr: https://github.com/TeamDots/dots-ocr (multilingual OCR)

**Research** (documento source):
- Line 38-42: PDF processing challenges and solutions
- Line 41: "Vision-language models eliminano OCR step, processando direttamente immagini PDF"

---

**ADR Metadata**:
- **ID**: ADR-003
- **Status**: Accepted
- **Date**: 2025-01-15
- **Related**: ADR-001 (RAG Architecture), ADR-002 (Embeddings)
