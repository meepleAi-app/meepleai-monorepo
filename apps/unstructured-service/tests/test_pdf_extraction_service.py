"""Unit tests for PdfExtractionService"""
import pytest
from unittest.mock import Mock, patch, AsyncMock
from pathlib import Path
from io import BytesIO

from src.application.pdf_extraction_service import PdfExtractionService
from src.domain.models import ExtractionResult, QualityScore, TextChunk


class TestPdfExtractionService:
    """Test PDF extraction service"""

    def setup_method(self):
        """Setup test instance"""
        self.service = PdfExtractionService()

    @pytest.mark.asyncio
    @patch("src.application.pdf_extraction_service.UnstructuredAdapter")
    async def test_extract_success(self, mock_adapter_class, mock_pdf_content, mock_unstructured_elements):
        """Test successful PDF extraction"""
        # Arrange
        mock_adapter = Mock()
        mock_adapter.partition_pdf.return_value = mock_unstructured_elements
        mock_adapter.chunk_elements.return_value = mock_unstructured_elements
        mock_adapter.extract_tables.return_value = [mock_unstructured_elements[2]]
        mock_adapter.detect_structures.return_value = ["Title", "Paragraph", "Table"]
        mock_adapter_class.return_value = mock_adapter

        service = PdfExtractionService()
        service.unstructured = mock_adapter

        # Act
        result = await service.extract_async(
            file_content=mock_pdf_content, filename="test.pdf", strategy="fast", language="ita"
        )

        # Assert
        assert isinstance(result, ExtractionResult)
        assert result.page_count > 0
        assert len(result.chunks) > 0
        assert result.extraction_duration_ms > 0
        assert 0.0 <= result.quality_score.total_score <= 1.0
        mock_adapter.partition_pdf.assert_called_once()
        mock_adapter.chunk_elements.assert_called_once()

    @pytest.mark.asyncio
    async def test_extract_invalid_pdf_raises_error(self):
        """Test extraction with invalid PDF raises ValueError"""
        # Arrange
        invalid_content = BytesIO(b"Not a PDF")

        # Act & Assert
        with pytest.raises(Exception):  # FileNotFoundError or ValueError expected
            await self.service.extract_async(
                file_content=invalid_content, filename="invalid.pdf", strategy="fast"
            )

    @pytest.mark.asyncio
    @patch("src.application.pdf_extraction_service.UnstructuredAdapter")
    async def test_extract_strategy_parameter(self, mock_adapter_class, mock_pdf_content, mock_unstructured_elements):
        """Test that extraction strategy parameter is passed correctly"""
        # Arrange
        mock_adapter = Mock()
        mock_adapter.partition_pdf.return_value = mock_unstructured_elements
        mock_adapter.chunk_elements.return_value = mock_unstructured_elements
        mock_adapter.extract_tables.return_value = []
        mock_adapter.detect_structures.return_value = ["Paragraph"]
        mock_adapter_class.return_value = mock_adapter

        service = PdfExtractionService()
        service.unstructured = mock_adapter

        # Act
        await service.extract_async(
            file_content=mock_pdf_content,
            filename="test.pdf",
            strategy="hi_res",  # Test hi_res strategy
            language="eng",
        )

        # Assert
        mock_adapter.partition_pdf.assert_called_once_with(
            file_path=pytest.approx(Path, rel=1e-9),  # Path will vary
            strategy="hi_res",
            language="eng",
        )

    @pytest.mark.asyncio
    @patch("src.application.pdf_extraction_service.UnstructuredAdapter")
    async def test_extract_creates_text_chunks(self, mock_adapter_class, mock_pdf_content, mock_unstructured_elements):
        """Test that extraction creates TextChunk domain models"""
        # Arrange
        mock_adapter = Mock()
        mock_adapter.partition_pdf.return_value = mock_unstructured_elements
        mock_adapter.chunk_elements.return_value = mock_unstructured_elements
        mock_adapter.extract_tables.return_value = []
        mock_adapter.detect_structures.return_value = []
        mock_adapter_class.return_value = mock_adapter

        service = PdfExtractionService()
        service.unstructured = mock_adapter

        # Act
        result = await service.extract_async(file_content=mock_pdf_content, filename="test.pdf")

        # Assert
        assert len(result.chunks) == len(mock_unstructured_elements)
        assert all(isinstance(chunk, TextChunk) for chunk in result.chunks)
        assert all(chunk.page_number > 0 for chunk in result.chunks)
