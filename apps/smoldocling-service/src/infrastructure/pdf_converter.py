"""PDF to Image conversion service"""
import logging
from pathlib import Path
from typing import List, Optional
from pdf2image import convert_from_path
from PIL import Image

from ..domain.models import PageImage
from ..config import settings

logger = logging.getLogger(__name__)


class PdfToImageConverter:
    """Converts PDF pages to images for VLM processing"""

    def __init__(self):
        self.settings = settings

    def convert_pdf_to_images(
        self, pdf_path: Path, max_pages: Optional[int] = None
    ) -> List[PageImage]:
        """
        Convert PDF to list of page images

        Args:
            pdf_path: Path to PDF file
            max_pages: Maximum pages to convert (None = all)

        Returns:
            List of PageImage objects

        Raises:
            ValueError: Invalid PDF or conversion failed
        """
        logger.info(
            f"Converting PDF to images: {pdf_path.name}, dpi={self.settings.image_dpi}, "
            f"format={self.settings.image_format}"
        )

        try:
            # Convert PDF to images using pdf2image (poppler)
            pil_images = convert_from_path(
                pdf_path=str(pdf_path),
                dpi=self.settings.image_dpi,
                fmt=self.settings.image_format.lower(),
                first_page=1,
                last_page=max_pages if max_pages else None,
                thread_count=2,  # Parallel conversion
            )

            # Wrap in PageImage domain objects
            page_images = []
            for idx, pil_image in enumerate(pil_images, start=1):
                page_image = PageImage.from_pil_image(
                    page_number=idx, image=pil_image, dpi=self.settings.image_dpi
                )
                page_images.append(page_image)

            logger.info(f"Converted {len(page_images)} pages to images")
            return page_images

        except Exception as e:
            logger.error(f"Failed to convert PDF to images: {e}", exc_info=True)
            raise ValueError(f"PDF to image conversion failed: {e}")

    def estimate_memory_usage(self, page_count: int) -> int:
        """
        Estimate memory usage for PDF conversion

        Args:
            page_count: Number of pages

        Returns:
            Estimated memory in bytes
        """
        # Rough estimate: DPI 300, RGB image ~2-3MB per page
        bytes_per_page = (self.settings.image_dpi / 100) ** 2 * 1024 * 200  # Heuristic
        total_bytes = int(bytes_per_page * page_count)

        logger.debug(
            f"Estimated memory for {page_count} pages: {total_bytes / 1024 / 1024:.1f}MB"
        )

        return total_bytes
