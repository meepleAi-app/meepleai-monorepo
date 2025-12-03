"""PDF extraction application service"""
import logging
import time
from pathlib import Path
from typing import BinaryIO, Literal

from ..domain.models import (
    PdfDocument,
    ExtractionResult,
    TextChunk,
)
from ..infrastructure import UnstructuredAdapter, FileStorageService
from ..application.quality_calculator import QualityScoreCalculator
from ..config import settings

logger = logging.getLogger(__name__)


class PdfExtractionService:
    """Application service for PDF text extraction"""

    def __init__(self):
        self.unstructured = UnstructuredAdapter()
        self.file_storage = FileStorageService()
        self.quality_calculator = QualityScoreCalculator()
        self.settings = settings

    def extract(
        self,
        file_content: BinaryIO,
        filename: str,
        strategy: Literal["fast", "hi_res"] = "fast",
        language: str = "ita",
    ) -> ExtractionResult:
        """
        Extract text from PDF asynchronously

        Args:
            file_content: Binary PDF content
            filename: Original filename
            strategy: Extraction strategy (fast or hi_res)
            language: Document language

        Returns:
            ExtractionResult with text, chunks, and quality score

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

            # Enforce configured max file size before any heavy work
            if file_size > self.settings.max_file_size:
                raise ValueError(
                    f"PDF too large: {file_size} bytes exceeds limit of {self.settings.max_file_size} bytes"
                )

            # Step 2: Validate PDF
            pdf_doc = PdfDocument(
                file_path=temp_path, file_size=file_size, language=language
            )
            pdf_doc.validate()

            logger.info(
                f"Starting extraction: file={filename}, size={file_size}, "
                f"strategy={strategy}, language={language}"
            )

            # Step 3: Extract elements using Unstructured
            elements = self.unstructured.partition_pdf(
                file_path=temp_path, strategy=strategy, language=language
            )

            # Step 4: Chunk text semantically
            chunks = self.unstructured.chunk_elements(
                elements,
                max_characters=self.settings.chunk_max_characters,
                overlap=self.settings.chunk_overlap,
            )

            # Step 5: Extract full text
            full_text = "\n\n".join(el.text for el in elements if hasattr(el, "text"))

            # Step 6: Extract metadata
            tables = self.unstructured.extract_tables(elements)
            detected_structures = self.unstructured.detect_structures(elements)

            # Step 7: Get page count (handle empty chunks edge case)
            page_numbers = [
                el.metadata.page_number
                for el in elements
                if hasattr(el, "metadata") and hasattr(el.metadata, "page_number") and el.metadata.page_number is not None
            ]
            page_count = max(page_numbers) if page_numbers else 1

            # Ensure page_count is never None (defensive programming)
            if page_count is None or page_count < 1:
                page_count = 1

            # Step 8: Convert chunks to domain models
            text_chunks = []
            for chunk in chunks:
                if hasattr(chunk, "text") and hasattr(chunk, "metadata"):
                    text_chunk = TextChunk(
                        text=chunk.text,
                        page_number=getattr(chunk.metadata, "page_number", 1),
                        element_type=getattr(chunk, "category", None),
                        metadata={
                            "char_count": len(chunk.text),
                        },
                    )
                    text_chunks.append(text_chunk)

            # Step 9: Calculate quality score
            quality_score = self.quality_calculator.calculate(
                full_text=full_text,
                elements=elements,
                page_count=page_count,
                tables=tables,
                detected_structures=detected_structures,
            )

            # Log quality warning if below threshold
            if not quality_score.meets_threshold(self.settings.quality_threshold):
                logger.warning(
                    f"Quality score below threshold: {quality_score.total_score:.2f} < {self.settings.quality_threshold}"
                )

            # Step 10: Calculate duration
            duration_ms = int((time.time() - start_time) * 1000)

            # Step 11: Create result
            result = ExtractionResult(
                full_text=full_text,
                chunks=text_chunks,
                page_count=page_count,
                elements=elements,
                tables=tables,
                detected_structures=detected_structures,
                extraction_duration_ms=duration_ms,
                quality_score=quality_score,
            )

            logger.info(
                f"Extraction completed: pages={page_count}, chunks={len(text_chunks)}, "
                f"tables={len(tables)}, quality={quality_score.total_score:.2f}, "
                f"duration={duration_ms}ms"
            )

            return result

        except Exception as e:
            logger.error(f"Extraction failed: {e}", exc_info=True)
            raise

        finally:
            # Cleanup temporary file
            if temp_path:
                self.file_storage.cleanup_temp_file(temp_path)
