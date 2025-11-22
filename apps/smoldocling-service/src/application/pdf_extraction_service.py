"""PDF extraction application service using SmolDocling VLM"""
import logging
import time
from pathlib import Path
from typing import BinaryIO

from ..domain.models import (
    PdfDocument,
    ExtractionResult,
    TextChunk,
    PageExtractionResult,
)
from ..infrastructure import (
    PdfToImageConverter,
    SmolDoclingAdapter,
    FileStorageService,
)
from ..application.quality_calculator import QualityScoreCalculator
from ..config import settings

logger = logging.getLogger(__name__)


class PdfExtractionService:
    """Application service for SmolDocling-based PDF extraction"""

    def __init__(self):
        self.pdf_converter = PdfToImageConverter()
        self.vlm_adapter = SmolDoclingAdapter()
        self.file_storage = FileStorageService()
        self.quality_calculator = QualityScoreCalculator()
        self.settings = settings

    async def extract_async(
        self,
        file_content: BinaryIO,
        filename: str,
        language: str = "ita",
    ) -> ExtractionResult:
        """
        Extract text from PDF using SmolDocling VLM

        Pipeline:
        1. Save PDF to temp file
        2. Convert PDF → Images (page-by-page)
        3. Process each image with SmolDocling VLM
        4. Aggregate results
        5. Calculate quality score
        6. Cleanup

        Args:
            file_content: Binary PDF content
            filename: Original filename
            language: Document language (informational - VLM handles detection)

        Returns:
            ExtractionResult with text, markdown, and metadata

        Raises:
            ValueError: Invalid PDF or parameters
            RuntimeError: Extraction failed
        """
        temp_path = None
        start_time = time.time()

        try:
            # Step 1: Save to temporary storage
            temp_path = self.file_storage.save_temp_file(file_content, filename)
            file_size = self.file_storage.get_file_size(temp_path)

            # Step 2: Validate PDF
            pdf_doc = PdfDocument(
                file_path=temp_path, file_size=file_size, language=language
            )
            pdf_doc.validate()

            logger.info(
                f"Starting SmolDocling extraction: file={filename}, size={file_size}, language={language}"
            )

            # Step 3: Initialize VLM model (lazy loading)
            if not self.vlm_adapter._is_initialized:
                logger.info("Initializing SmolDocling model (first request)...")
                self.vlm_adapter.initialize()

            # Step 4: Convert PDF to images
            logger.info(f"Converting PDF to images (DPI={self.settings.image_dpi})")
            page_images = self.pdf_converter.convert_pdf_to_images(
                pdf_path=temp_path, max_pages=self.settings.max_pages_per_request
            )

            page_count = len(page_images)
            logger.info(f"Converted {page_count} pages to images")

            # Step 5: Process each page with VLM
            page_results: List[PageExtractionResult] = []

            for page_image in page_images:
                logger.debug(f"Processing page {page_image.page_number} with SmolDocling VLM")

                page_result = self.vlm_adapter.process_page(page_image)
                page_results.append(page_result)

                logger.debug(
                    f"Page {page_image.page_number}: {page_result.char_count} chars, "
                    f"tables={page_result.has_tables}, confidence={page_result.confidence_score:.2f}"
                )

            # Step 6: Aggregate results
            full_text = "\n\n".join(
                page.markdown_text for page in page_results if not page.is_empty
            )
            markdown_text = "\n\n---\n\n".join(  # Page separator
                f"<!-- Page {page.page_number} -->\n{page.markdown_text}"
                for page in page_results
                if not page.is_empty
            )

            # Step 7: Create text chunks (simple page-based chunking for now)
            text_chunks = []
            for page_result in page_results:
                if not page_result.is_empty:
                    chunk = TextChunk(
                        text=page_result.markdown_text,
                        page_number=page_result.page_number,
                        element_type="Page",  # VLM processes full pages
                        metadata={
                            "char_count": page_result.char_count,
                            "has_tables": page_result.has_tables,
                            "has_equations": page_result.has_equations,
                            "confidence": page_result.confidence_score,
                        },
                    )
                    text_chunks.append(chunk)

            # Step 8: Calculate quality score
            quality_score = self.quality_calculator.calculate(
                full_text=full_text,
                page_results=page_results,
                expected_page_count=page_count,
            )

            # Log quality warning if below threshold
            if not quality_score.meets_threshold(self.settings.quality_threshold):
                logger.warning(
                    f"Quality score below threshold: {quality_score.total_score:.2f} < {self.settings.quality_threshold}"
                )

            # Step 9: Calculate duration
            duration_ms = int((time.time() - start_time) * 1000)

            # Step 10: Create result
            result = ExtractionResult(
                full_text=full_text,
                markdown_text=markdown_text,
                chunks=text_chunks,
                page_results=page_results,
                page_count=page_count,
                extraction_duration_ms=duration_ms,
                quality_score=quality_score,
            )

            logger.info(
                f"SmolDocling extraction completed: pages={page_count}, chunks={len(text_chunks)}, "
                f"tables={result.has_tables}, equations={result.has_equations}, "
                f"quality={quality_score.total_score:.2f}, duration={duration_ms}ms"
            )

            return result

        except Exception as e:
            logger.error(f"SmolDocling extraction failed: {e}", exc_info=True)
            raise

        finally:
            # Cleanup temporary file
            if temp_path:
                self.file_storage.cleanup_temp_file(temp_path)
