"""File storage service for temporary PDF files (reused from Unstructured)"""
import logging
import uuid
from pathlib import Path
from typing import BinaryIO

from ..config import settings

logger = logging.getLogger(__name__)


class FileStorageService:
    """Service for managing temporary file storage"""

    def __init__(self):
        self.temp_dir = settings.temp_dir
        self._ensure_temp_dir()

    def _ensure_temp_dir(self) -> None:
        """Create temp directory if it doesn't exist"""
        self.temp_dir.mkdir(parents=True, exist_ok=True)
        logger.debug(f"Temp directory ready: {self.temp_dir}")

    def save_temp_file(
        self, file_content: BinaryIO, filename: str = "document.pdf"
    ) -> Path:
        """
        Save uploaded file to temporary storage

        Args:
            file_content: Binary file content
            filename: Original filename (for logging)

        Returns:
            Path to saved temporary file
        """
        # Generate unique filename
        unique_id = uuid.uuid4().hex[:8]
        temp_filename = f"pdf_{unique_id}.pdf"
        temp_path = self.temp_dir / temp_filename

        try:
            # Write file content
            with open(temp_path, "wb") as temp_file:
                content = file_content.read()
                temp_file.write(content)

            file_size = temp_path.stat().st_size
            logger.info(
                f"Saved temp file: {temp_filename} (original: {filename}, size: {file_size} bytes)"
            )

            return temp_path

        except Exception as e:
            logger.error(f"Failed to save temp file: {e}", exc_info=True)
            # Cleanup on error
            if temp_path.exists():
                temp_path.unlink()
            raise

    def cleanup_temp_file(self, file_path: Path) -> None:
        """
        Delete temporary file

        Args:
            file_path: Path to temporary file
        """
        try:
            if file_path.exists():
                file_path.unlink()
                logger.debug(f"Deleted temp file: {file_path.name}")
            else:
                logger.warning(f"Temp file not found for cleanup: {file_path}")

        except Exception as e:
            # Log but don't fail - cleanup is best-effort
            logger.warning(f"Failed to delete temp file {file_path}: {e}")

    def get_file_size(self, file_path: Path) -> int:
        """Get file size in bytes"""
        return file_path.stat().st_size if file_path.exists() else 0
