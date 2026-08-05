"""Adapter for SmolDocling VLM model"""
import logging
import time
import torch
from typing import List, Optional, Tuple
from transformers import (
    AutoProcessor,
    AutoModelForImageTextToText,
    StoppingCriteria,
    StoppingCriteriaList,
)
from docling_core.types.doc.document import DoclingDocument, DocTagsDocument

from ..domain.models import (
    PageImage,
    PageExtractionResult,
    CropExtractionResult,
    STRUCTURE_TAGS,
)
from ..config import settings

logger = logging.getLogger(__name__)


class RepetitionStoppingCriteria(StoppingCriteria):
    """Stop generation when the VLM degenerates into a repetition loop.

    On a decorative illustration (not a table) SmolDocling collapses into emitting the same
    token hundreds of times (~19s wasted before the caller discards the crop for lack of
    ``<otsl>``; issue #3435 de-risk "Opzione B"). We watch the *trailing window* of generated
    ids and stop once it collapses to ``<= max_distinct`` distinct token ids.

    This is deliberately NOT ``no_repeat_ngram_size``: forcing the model off its repeated
    token makes it *fabricate a spurious OTSL table* from an illustration — a proven false
    positive that would corrupt the RAG corpus (issue #3435). Early-stop only caps the wasted
    compute; the ``<otsl>`` gate stays authoritative. Assumes batch size 1 (one crop).
    """

    def __init__(self, window: int = 24, max_distinct: int = 2, prompt_len: int = 0):
        self.window = max(1, window)
        self.max_distinct = max(1, max_distinct)
        self.prompt_len = prompt_len  # set by _run_inference so the prompt is excluded
        self.stopped = False

    def __call__(self, input_ids, scores, **kwargs) -> bool:
        seq = input_ids[0]
        # Only inspect GENERATED tokens: the prompt front-loads many identical image
        # placeholder ids, which would false-trip the "uniform tail" check otherwise.
        generated_len = int(seq.shape[0]) - self.prompt_len
        if generated_len < self.window:
            return False
        tail = seq[-self.window:].tolist()
        if len(set(tail)) <= self.max_distinct:
            self.stopped = True
            return True
        return False


class SmolDoclingAdapter:
    """Adapter for SmolDocling VLM model (256M parameters)"""

    # True chat/control special tokens to strip from the decoded output. These are
    # NOT DocTags markup — the DocTags tokens (<loc_*>, <otsl>, <fcel>, <doctag>, ...)
    # must be preserved for region-grounding + table structure (issue #3435, spec §5ter).
    _CONTROL_TOKENS = (
        "<|im_start|>",
        "<|im_end|>",
        "<|endoftext|>",
        "<end_of_utterance>",
        "<fake_token_around_image>",
        "<image>",
    )

    # Instruction handed to the VLM via the chat template (official SmolDocling recipe).
    _PROMPT_TEXT = "Convert this page to docling."

    # "Page has structured layout" signal — single source of truth in domain.models.
    _STRUCTURE_TAGS = STRUCTURE_TAGS

    def __init__(self):
        self.settings = settings
        self.device = self._get_device()
        self.processor = None
        self.model = None
        self._is_initialized = False

    def _get_device(self) -> str:
        """Determine device (cuda or cpu) based on configuration and availability"""
        if self.settings.device == "auto":
            device = "cuda" if torch.cuda.is_available() else "cpu"
        else:
            device = self.settings.device

        logger.info(f"Using device: {device}")
        if device == "cuda":
            logger.info(
                f"CUDA available: GPU count={torch.cuda.device_count()}, "
                f"GPU name={torch.cuda.get_device_name(0)}"
            )

        return device

    def initialize(self) -> None:
        """
        Initialize SmolDocling model and processor (lazy loading)
        Called on first use or during startup warmup
        """
        if self._is_initialized:
            logger.debug("Model already initialized, skipping")
            return

        logger.info(f"Loading SmolDocling model: {self.settings.model_name}")

        try:
            # Load processor
            self.processor = AutoProcessor.from_pretrained(
                self.settings.model_name, cache_dir=str(self.settings.model_cache_dir)
            )

            # Load model with specified dtype. Prefer SDPA attention: on this VLM it is
            # ~30% faster than the default eager path at the long sequence lengths a full
            # rulebook page produces (#3435 SP3 GPU benchmark: 25→33 tok/s on an RTX 4070),
            # with no output change, and it needs no extra dependency (unlike
            # flash_attention_2, which is not installed). Fall back to eager if the
            # installed transformers/model can't wire SDPA.
            torch_dtype = getattr(torch, self.settings.torch_dtype)
            load_kwargs = {
                "torch_dtype": torch_dtype,
                "cache_dir": str(self.settings.model_cache_dir),
            }
            try:
                self.model = AutoModelForImageTextToText.from_pretrained(
                    self.settings.model_name,
                    attn_implementation="sdpa",
                    **load_kwargs,
                ).to(self.device)
            except (ValueError, ImportError) as exc:
                logger.warning(
                    "SDPA attention unavailable (%s); falling back to eager attention", exc
                )
                self.model = AutoModelForImageTextToText.from_pretrained(
                    self.settings.model_name, **load_kwargs
                ).to(self.device)

            # Set to eval mode
            self.model.eval()

            self._is_initialized = True
            logger.info("SmolDocling model loaded successfully")

            # Log memory usage if GPU
            if self.device == "cuda":
                memory_allocated = torch.cuda.memory_allocated(0) / 1024**2  # MB
                logger.info(f"GPU memory allocated: {memory_allocated:.1f}MB")

        except Exception as e:
            logger.error(f"Failed to initialize SmolDocling model: {e}", exc_info=True)
            raise RuntimeError(f"Model initialization failed: {e}")

    def _run_inference(
        self,
        page_image: PageImage,
        stopping_criteria: Optional[StoppingCriteriaList] = None,
        max_new_tokens: Optional[int] = None,
    ) -> str:
        """Run one VLM forward pass and return the cleaned DocTags text.

        Shared core of ``process_page`` (full page) and ``generate_crop_doctags`` (crop). It
        builds the official SmolDocling chat prompt, generates deterministically, trims the
        prompt, decodes WITH special tokens (so DocTags markup survives), and strips only the
        chat/control tokens.

        - The Idefics3Processor only emits ``input_ids`` when a text prompt is supplied
          (``processing_idefics3.py``: ``input_ids`` is added inside ``if text is not None:``);
          an images-only call yields no ``input_ids`` and breaks the <image>-token alignment
          and the prompt trim (issue #3435, §5quater).
        - Decode with ``skip_special_tokens=False``: the DocTags tokens (``<loc_*>``,
          ``<otsl>``, ``<fcel>``, ...) are flagged *special*, so skipping them would strip the
          location coordinates and table structure this feature needs (issue #3435, §5ter).
        - The prompt trim (``generated_ids[:, prompt_len:]``) means a model that generated
          nothing beyond the prompt decodes to ``""`` (clean degradation, R5) instead of
          leaking prompt/instruction text into the DocTags.
        """
        messages = [
            {
                "role": "user",
                "content": [
                    {"type": "image"},
                    {"type": "text", "text": self._PROMPT_TEXT},
                ],
            }
        ]
        prompt = self.processor.apply_chat_template(messages, add_generation_prompt=True)

        # text + image so input_ids and the image tokens are aligned
        inputs = self.processor(
            text=prompt, images=[page_image.image], return_tensors="pt"
        ).to(self.device)

        # Some processor outputs (rows/cols) are unused by the current model and would
        # surface as "Unused model_kwargs" warnings/errors. Filter them out before generation.
        filtered_inputs = {
            key: value for key, value in inputs.items() if key not in {"rows", "cols"}
        }

        prompt_len = filtered_inputs["input_ids"].shape[1]

        # Tell any repetition early-stop where the prompt ends so it inspects only generated
        # tokens (the prompt front-loads many identical image placeholder ids).
        if stopping_criteria is not None:
            for criterion in stopping_criteria:
                if isinstance(criterion, RepetitionStoppingCriteria):
                    criterion.prompt_len = prompt_len

        generate_kwargs = {
            "max_new_tokens": max_new_tokens or self.settings.max_new_tokens,
            "do_sample": False,  # Deterministic output
        }
        if stopping_criteria is not None:
            generate_kwargs["stopping_criteria"] = stopping_criteria

        with torch.no_grad():
            generated_ids = self.model.generate(**filtered_inputs, **generate_kwargs)

        generated_ids = generated_ids[:, prompt_len:]

        raw_doctags = self.processor.batch_decode(
            generated_ids, skip_special_tokens=False
        )[0]
        return self._clean_doctags(raw_doctags)

    def process_page(self, page_image: PageImage) -> PageExtractionResult:
        """
        Process a single page image with SmolDocling VLM

        Args:
            page_image: PageImage domain object

        Returns:
            PageExtractionResult with extracted text and metadata

        Raises:
            RuntimeError: Model not initialized or inference failed
        """
        if not self._is_initialized:
            raise RuntimeError(
                "SmolDocling model not initialized. Call initialize() first."
            )

        logger.debug(f"Processing page {page_image.page_number} with SmolDocling VLM")

        try:
            doctags_text = self._run_inference(page_image)

            # Convert to Markdown using Docling
            markdown_text = self._convert_to_markdown(doctags_text)

            # Extract metadata. SmolDocling emits tables in OTSL (<otsl>) and formulas as
            # <formula> — never <table>/<equation>. The old `"$" in doctags_text` heuristic
            # false-positived on any rulebook price ("pay $5"), so it is dropped (#3435).
            has_tables = self._detect_has_tables(doctags_text)
            has_equations = "<formula>" in doctags_text.lower()

            # Calculate confidence (placeholder - SmolDocling doesn't return confidence scores)
            # In production, could use token probability if needed
            confidence_score = self._estimate_confidence(doctags_text)

            result = PageExtractionResult(
                page_number=page_image.page_number,
                doctags_text=doctags_text,
                markdown_text=markdown_text,
                char_count=len(markdown_text),
                has_tables=has_tables,
                has_equations=has_equations,
                confidence_score=confidence_score,
            )

            logger.debug(
                f"Page {page_image.page_number} processed: {result.char_count} chars, "
                f"tables={has_tables}, equations={has_equations}"
            )

            return result

        except Exception as e:
            logger.error(
                f"Failed to process page {page_image.page_number}: {e}", exc_info=True
            )
            # Return empty result instead of failing
            return PageExtractionResult(
                page_number=page_image.page_number,
                doctags_text="",
                markdown_text="",
                char_count=0,
                has_tables=False,
                has_equations=False,
                confidence_score=0.0,
            )

    def _clean_doctags(self, raw: str) -> str:
        """
        Remove chat/control special tokens from a DocTags decode while PRESERVING
        the DocTags markup (location + structure tokens).

        The output is decoded with ``skip_special_tokens=False`` so that DocTags tokens
        survive; this strips only the true control tokens (``<|im_end|>``,
        ``<end_of_utterance>``, image placeholders, ...) — see ``_CONTROL_TOKENS``.

        Args:
            raw: Raw decoded text (special tokens NOT skipped)

        Returns:
            DocTags text with control tokens removed, whitespace-trimmed
        """
        text = raw
        for control in self._CONTROL_TOKENS:
            text = text.replace(control, "")
        return text.strip()

    @staticmethod
    def _detect_has_tables(doctags_text: str) -> bool:
        """
        Detect whether the DocTags output contains a table.

        SmolDocling renders tables in OTSL (``<otsl>``/``<fcel>``), NOT as ``<table>``;
        looking for ``<table>`` (the previous behaviour) never matched (issue #3435).
        """
        return "<otsl>" in doctags_text.lower()

    def _build_docling_doc(self, doctags_text: str) -> Optional[DoclingDocument]:
        """Parse cleaned DocTags into a DoclingDocument (no image) or return None on failure.

        Uses ``images=None``: the DocTags ``<loc_*>`` grid alone yields ``prov[].bbox`` already
        normalized to [0,1] top-left (issue #3435 §5ter), so no page raster is needed.
        """
        try:
            doctags_doc = DocTagsDocument.from_doctags_and_image_pairs(
                doctags=[doctags_text], images=None
            )
            return DoclingDocument.load_from_doctags(doctags_doc)
        except Exception as e:
            logger.warning("DocTags parse failed: %s", e)
            return None

    def _convert_to_markdown(self, doctags_text: str) -> str:
        """
        Convert DocTags markup to Markdown, or "" if conversion fails (never raw DocTags markup).

        Do NOT fall back to raw DocTags: doctags_text carries markup (<loc_*>, <otsl>, <fcel>,
        ...) that would pollute the RAG corpus and the /preprocess extracted_text handed to the
        LLM (issue #3435). Returning "" makes the page count as empty (is_empty) instead of
        ingesting markup as content.
        """
        doc = self._build_docling_doc(doctags_text)
        if doc is None:
            logger.warning("Markdown conversion failed, dropping page content")
            return ""
        try:
            return doc.export_to_markdown()
        except Exception as e:
            logger.warning("Markdown export failed, dropping page content: %s", e)
            return ""

    # -- Crop-table discrimination (issue #3435 SP3, Option B) -----------------------------

    def generate_crop_doctags(self, page_image: PageImage) -> Tuple[str, bool]:
        """Run plain-mode VLM inference on a single crop with a repetition early-stop.

        Returns ``(cleaned_doctags, degenerated)``. On any *inference* failure returns
        ``("", False)`` so the caller treats the crop as a non-table (clean degradation, R5) —
        it does not propagate transient generation errors. It DOES raise ``RuntimeError`` for
        the uninitialized-model precondition, matching ``process_page`` (a programmer error, not
        an inference failure). Uses the same plain prompt as full pages (NO
        ``no_repeat_ngram_size`` — it fabricates spurious tables from illustrations, a proven
        false positive; issue #3435).
        """
        if not self._is_initialized:
            raise RuntimeError(
                "SmolDocling model not initialized. Call initialize() first."
            )
        criterion = RepetitionStoppingCriteria(
            window=self.settings.crop_rep_stop_window,
            max_distinct=self.settings.crop_rep_stop_max_distinct,
        )
        try:
            doctags = self._run_inference(
                page_image,
                stopping_criteria=StoppingCriteriaList([criterion]),
                max_new_tokens=self.settings.crop_max_new_tokens,
            )
            return doctags, criterion.stopped
        except Exception as e:
            logger.error(f"Crop inference failed: {e}", exc_info=True)
            return "", False

    def extract_crop(self, page_image: PageImage) -> CropExtractionResult:
        """Discriminate a single crop with the VLM: plain mode + early-stop -> <otsl> gate ->
        markdown + bbox.

        Returns a CropExtractionResult with ``prefiltered=False`` and ``colorfulness=0.0`` —
        those belong to the CropDiscriminator, which sets ``colorfulness`` and owns the cheap
        pre-filter that may reject a crop *before* this method runs.
        """
        start = time.time()

        def _elapsed_ms() -> int:
            return int((time.time() - start) * 1000)

        doctags, degenerated = self.generate_crop_doctags(page_image)

        if not doctags:
            return CropExtractionResult(
                is_table=False,
                reason="degenerate-earlystop" if degenerated else "empty-output",
                markdown="", bbox=None, doctags="", confidence=0.0,
                prefiltered=False, degenerated=degenerated, colorfulness=0.0,
                duration_ms=_elapsed_ms(),
            )

        if not self._detect_has_tables(doctags):
            return CropExtractionResult(
                is_table=False,
                reason="degenerate-earlystop" if degenerated else "no-otsl",
                markdown="", bbox=None, doctags=doctags,
                confidence=self._estimate_confidence(doctags),
                prefiltered=False, degenerated=degenerated, colorfulness=0.0,
                duration_ms=_elapsed_ms(),
            )

        markdown, bbox = self.extract_table_markdown_and_bbox(doctags)
        if not markdown:
            # OTSL present but docling could not rebuild it -> discard (never inject markup).
            return CropExtractionResult(
                is_table=False, reason="conversion-failed",
                markdown="", bbox=None, doctags=doctags,
                confidence=self._estimate_confidence(doctags),
                prefiltered=False, degenerated=degenerated, colorfulness=0.0,
                duration_ms=_elapsed_ms(),
            )

        return CropExtractionResult(
            is_table=True, reason="table-otsl", markdown=markdown, bbox=bbox,
            doctags=doctags, confidence=self._estimate_confidence(doctags),
            prefiltered=False, degenerated=degenerated, colorfulness=0.0,
            duration_ms=_elapsed_ms(),
        )

    def extract_table_markdown_and_bbox(
        self, doctags_text: str
    ) -> Tuple[str, Optional[Tuple[float, float, float, float]]]:
        """Rebuild a crop's table markdown and its bbox from one DoclingDocument.

        Returns ``("", None)`` if docling cannot parse/export — the caller then discards the
        crop rather than inject unstructured markup.
        """
        doc = self._build_docling_doc(doctags_text)
        if doc is None:
            return "", None
        try:
            markdown = doc.export_to_markdown()
        except Exception as e:
            logger.warning("Table markdown export failed for crop: %s", e)
            return "", None
        bbox = self._extract_table_bbox(doc)
        return (markdown or ""), bbox

    def _extract_table_bbox(
        self, docling_doc: DoclingDocument
    ) -> Optional[Tuple[float, float, float, float]]:
        """First table's bbox as (x0,y0,x1,y1) in [0,1] top-left, or None.

        ``load_from_doctags(images=None)`` yields ``prov[].bbox`` already normalized to [0,1]
        with a top-left origin (issue #3435 §5ter). MVP is 1:1 (one table per region, IA-6):
        with >1 table we take the first and log.
        """
        tables = getattr(docling_doc, "tables", None) or []
        if not tables:
            return None
        if len(tables) > 1:
            logger.info(
                "Crop yielded %d tables; using the first (MVP 1:1, IA-6)", len(tables)
            )
        prov = getattr(tables[0], "prov", None) or []
        if not prov:
            return None
        bbox = getattr(prov[0], "bbox", None)
        if bbox is None:
            return None
        return self._normalize_bbox(bbox)

    @staticmethod
    def _normalize_bbox(bbox) -> Optional[Tuple[float, float, float, float]]:
        """Coerce a docling BoundingBox to (x0,y0,x1,y1) in [0,1] top-left.

        Robust to docling-core attribute drift (l/t/r/b vs x0/y0/x1/y1) and to a BOTTOMLEFT
        coordinate origin (flipped to TOPLEFT).
        """
        def _get(obj, *names):
            for name in names:
                value = getattr(obj, name, None)
                if value is not None:
                    return float(value)
            return None

        left = _get(bbox, "l", "x0", "left")
        right = _get(bbox, "r", "x1", "right")
        top = _get(bbox, "t", "y0", "top")
        bottom = _get(bbox, "b", "y1", "bottom")
        if None in (left, right, top, bottom):
            return None

        origin = getattr(bbox, "coord_origin", None)
        origin_name = getattr(origin, "name", str(origin)) if origin is not None else ""
        if origin_name.upper().startswith("BOTTOM"):
            top, bottom = 1.0 - top, 1.0 - bottom

        x0, x1 = sorted((left, right))
        y0, y1 = sorted((top, bottom))
        return (x0, y0, x1, y1)

    def _estimate_confidence(self, doctags_text: str) -> float:
        """
        Estimate confidence score based on output characteristics

        Since SmolDocling doesn't return explicit confidence scores,
        we estimate based on output quality indicators.

        Args:
            doctags_text: Generated DocTags text

        Returns:
            Estimated confidence (0-1)
        """
        if not doctags_text or len(doctags_text.strip()) == 0:
            return 0.0

        score = 0.7  # Base score for non-empty output

        # Heuristics (rough estimates). Tags aligned with SmolDocling's real DocTags
        # vocabulary: OTSL tables (<otsl>) and formulas (<formula>), and structure tags
        # verified in the tokenizer vocab — the old <table>/<equation>/</section> checks
        # never matched real output (issue #3435).
        if len(doctags_text) > 500:
            score += 0.1  # Substantial text extracted
        if self._detect_has_tables(doctags_text) or "<formula>" in doctags_text.lower():
            score += 0.1  # Structured elements detected
        if any(tag in doctags_text for tag in self._STRUCTURE_TAGS):
            score += 0.1  # Proper structure

        return min(score, 1.0)

    def cleanup(self) -> None:
        """
        Cleanup model resources (free GPU memory)
        """
        if self.model is not None and self.device == "cuda":
            del self.model
            del self.processor
            torch.cuda.empty_cache()
            logger.info("Model resources freed")
