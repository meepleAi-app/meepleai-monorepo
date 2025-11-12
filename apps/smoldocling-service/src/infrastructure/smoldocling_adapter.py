"""Adapter for SmolDocling VLM model"""
import logging
import torch
from typing import List, Optional
from transformers import AutoProcessor, AutoModelForVision2Seq
from docling_core.types.doc.document import DocTagsDocument

from ..domain.models import PageImage, PageExtractionResult
from ..config import settings

logger = logging.getLogger(__name__)


class SmolDoclingAdapter:
    """Adapter for SmolDocling VLM model (256M parameters)"""

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

            # Load model with specified dtype
            torch_dtype = getattr(torch, self.settings.torch_dtype)
            self.model = AutoModelForVision2Seq.from_pretrained(
                self.settings.model_name,
                torch_dtype=torch_dtype,
                cache_dir=str(self.settings.model_cache_dir),
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
            # Prepare inputs
            inputs = self.processor(
                images=page_image.image, return_tensors="pt"
            ).to(self.device)

            # Generate DocTags with VLM
            with torch.no_grad():
                generated_ids = self.model.generate(
                    **inputs,
                    max_new_tokens=self.settings.max_new_tokens,
                    do_sample=False,  # Deterministic output
                )

            # Decode output
            doctags_text = self.processor.batch_decode(
                generated_ids, skip_special_tokens=True
            )[0]

            # Convert to Markdown using Docling
            markdown_text = self._convert_to_markdown(doctags_text)

            # Extract metadata
            has_tables = "<table>" in doctags_text.lower()
            has_equations = "<equation>" in doctags_text.lower() or "$" in doctags_text

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

    def _convert_to_markdown(self, doctags_text: str) -> str:
        """
        Convert DocTags markup to Markdown

        Args:
            doctags_text: Raw DocTags output from SmolDocling

        Returns:
            Markdown-formatted text
        """
        try:
            # Use Docling library to convert
            doc_tags_doc = DocTagsDocument(text=doctags_text)
            docling_doc = doc_tags_doc.export_to_document()
            markdown = docling_doc.export_to_markdown()
            return markdown

        except Exception as e:
            logger.warning(f"Markdown conversion failed, returning raw text: {e}")
            # Fallback: return raw DocTags if conversion fails
            return doctags_text

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

        # Heuristics (rough estimates)
        if len(doctags_text) > 500:
            score += 0.1  # Substantial text extracted
        if "<table>" in doctags_text or "<equation>" in doctags_text:
            score += 0.1  # Structured elements detected
        if "</section>" in doctags_text or "</paragraph>" in doctags_text:
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
