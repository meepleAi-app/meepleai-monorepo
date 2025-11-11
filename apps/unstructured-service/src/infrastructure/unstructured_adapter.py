"""Adapter for Unstructured library"""
import logging
from pathlib import Path
from typing import List, Any, Literal
from unstructured.partition.pdf import partition_pdf
from unstructured.chunking.title import chunk_by_title

from ..config import settings

logger = logging.getLogger(__name__)


class UnstructuredAdapter:
    """Adapter for Unstructured PDF processing library"""

    def __init__(self):
        self.settings = settings

    def partition_pdf(
        self,
        file_path: Path,
        strategy: Literal["fast", "hi_res"] = "fast",
        language: str = "ita",
    ) -> List[Any]:
        """
        Extract elements from PDF using Unstructured

        Args:
            file_path: Path to PDF file
            strategy: Extraction strategy (fast or hi_res)
            language: Document language code

        Returns:
            List of Unstructured Element objects
        """
        logger.info(
            f"Starting PDF partition with strategy={strategy}, language={language}"
        )

        try:
            elements = partition_pdf(
                filename=str(file_path),
                strategy=strategy,
                languages=[language],
                infer_table_structure=True,
                include_page_breaks=True,
            )

            logger.info(f"Extracted {len(elements)} elements from PDF")
            return elements

        except Exception as e:
            logger.error(f"Failed to partition PDF: {e}", exc_info=True)
            raise

    def chunk_elements(
        self,
        elements: List[Any],
        max_characters: int = 2000,
        overlap: int = 200,
    ) -> List[Any]:
        """
        Chunk elements by title for semantic chunking

        Args:
            elements: List of Unstructured elements
            max_characters: Maximum characters per chunk
            overlap: Character overlap between chunks

        Returns:
            List of chunked elements
        """
        logger.info(
            f"Chunking {len(elements)} elements (max_chars={max_characters}, overlap={overlap})"
        )

        try:
            chunks = chunk_by_title(
                elements,
                max_characters=max_characters,
                new_after_n_chars=int(max_characters * 0.9),  # 90% soft limit
                combine_text_under_n_chars=overlap,  # Merge small chunks
                multipage_sections=False,  # Preserve page boundaries
            )

            logger.info(f"Created {len(chunks)} semantic chunks")
            return chunks

        except Exception as e:
            logger.error(f"Failed to chunk elements: {e}", exc_info=True)
            raise

    def extract_tables(self, elements: List[Any]) -> List[Any]:
        """Extract table elements from element list"""
        tables = [el for el in elements if el.category == "Table"]
        logger.debug(f"Extracted {len(tables)} tables")
        return tables

    def detect_structures(self, elements: List[Any]) -> List[str]:
        """Detect document structures (titles, headers, lists, etc.)"""
        structure_types = set()

        for element in elements:
            if hasattr(element, "category"):
                structure_types.add(element.category)

        structures = sorted(list(structure_types))
        logger.debug(f"Detected structures: {structures}")
        return structures
