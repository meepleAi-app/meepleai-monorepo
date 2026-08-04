"""Adapter for SmolDocling VLM model"""
import logging
import torch
from typing import List, Optional
from transformers import AutoProcessor, AutoModelForImageTextToText
from docling_core.types.doc.document import DoclingDocument, DocTagsDocument

from ..domain.models import PageImage, PageExtractionResult, STRUCTURE_TAGS
from ..config import settings

logger = logging.getLogger(__name__)


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
            # Build the chat prompt (official SmolDocling recipe). The Idefics3Processor
            # only emits input_ids when a text prompt is supplied (processing_idefics3.py:
            # input_ids is added inside `if text is not None:`); calling it images-only
            # yields no input_ids, so generation cannot align the <image> tokens and the
            # prompt-trim below would KeyError (issue #3435).
            messages = [
                {
                    "role": "user",
                    "content": [
                        {"type": "image"},
                        {"type": "text", "text": self._PROMPT_TEXT},
                    ],
                }
            ]
            prompt = self.processor.apply_chat_template(
                messages, add_generation_prompt=True
            )

            # Prepare inputs (text + image so input_ids and the image tokens are aligned)
            inputs = self.processor(
                text=prompt, images=[page_image.image], return_tensors="pt"
            ).to(self.device)

            # Some processor outputs (rows/cols) are unused by the current model
            # and would surface as "Unused model_kwargs" warnings/errors.
            # Filter them out before generation.
            filtered_inputs = {
                key: value
                for key, value in inputs.items()
                if key not in {"rows", "cols"}
            }

            # Generate DocTags with VLM
            with torch.no_grad():
                generated_ids = self.model.generate(
                    **filtered_inputs,
                    max_new_tokens=self.settings.max_new_tokens,
                    do_sample=False,  # Deterministic output
                )

            # Trim the prompt (image/instruction placeholder tokens) so only the newly
            # generated DocTags are decoded — matches the official SmolDocling recipe
            # (generated_ids[:, prompt_length:]). If the model generated nothing beyond
            # the prompt, the slice is empty and decode yields "" (clean degradation, R5)
            # rather than leaking prompt/instruction text into doctags_text.
            prompt_len = filtered_inputs["input_ids"].shape[1]
            generated_ids = generated_ids[:, prompt_len:]

            # Decode WITH special tokens: DocTags markup (<loc_*>, <otsl>, <fcel>, ...)
            # is flagged `special` in the tokenizer, so skip_special_tokens=True would
            # strip the location coordinates and table structure this feature needs
            # (issue #3435, spec §5ter). We decode everything, then remove only the true
            # chat/control tokens in _clean_doctags().
            raw_doctags = self.processor.batch_decode(
                generated_ids, skip_special_tokens=False
            )[0]
            doctags_text = self._clean_doctags(raw_doctags)

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

    def _convert_to_markdown(self, doctags_text: str) -> str:
        """
        Convert DocTags markup to Markdown

        Args:
            doctags_text: Raw DocTags output from SmolDocling

        Returns:
            Markdown-formatted text, or "" if conversion fails (never raw DocTags markup).
        """
        try:
            doctags_doc = DocTagsDocument.from_doctags_and_image_pairs(
                doctags=[doctags_text],
                images=None,
            )
            docling_doc = DoclingDocument.load_from_doctags(doctags_doc)
            return docling_doc.export_to_markdown()

        except Exception as e:
            # Do NOT fall back to raw DocTags: doctags_text now carries markup
            # (<loc_*>, <otsl>, <fcel>, ...) that would pollute the RAG corpus and the
            # /preprocess extracted_text handed to the LLM (issue #3435). Return "" so the
            # page is treated as empty (is_empty) instead of ingesting markup as content.
            logger.warning("Markdown conversion failed, dropping page content: %s", e)
            return ""

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
