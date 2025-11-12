"""Integration tests for FastAPI endpoints"""
import pytest
from fastapi.testclient import TestClient
from unittest.mock import patch, Mock
from io import BytesIO

from src.main import app
from src.domain.models import ExtractionResult, QualityScore, TextChunk


@pytest.fixture
def client():
    """FastAPI test client"""
    return TestClient(app)


class TestExtractEndpoint:
    """Test /api/v1/extract endpoint"""

    @patch("src.main.pdf_service.extract_async")
    def test_extract_success(self, mock_extract, client, mock_pdf_content):
        """Test successful PDF extraction"""
        # Arrange
        mock_result = ExtractionResult(
            full_text="Test extracted text",
            chunks=[
                TextChunk(text="Chunk 1", page_number=1, element_type="Title"),
                TextChunk(text="Chunk 2", page_number=1, element_type="Paragraph"),
            ],
            page_count=1,
            elements=[],
            tables=[],
            detected_structures=["Title", "Paragraph"],
            extraction_duration_ms=1200,
            quality_score=QualityScore(
                total_score=0.85,
                text_coverage_score=0.40,
                structure_detection_score=0.18,
                table_detection_score=0.15,
                page_coverage_score=0.12,
            ),
        )
        mock_extract.return_value = mock_result

        # Act
        response = client.post(
            "/api/v1/extract",
            files={"file": ("test.pdf", mock_pdf_content, "application/pdf")},
            data={"strategy": "fast", "language": "ita"},
        )

        # Assert
        assert response.status_code == 200
        data = response.json()
        assert data["text"] == "Test extracted text"
        assert len(data["chunks"]) == 2
        assert data["quality_score"] == 0.85
        assert data["page_count"] == 1
        assert "extraction_duration_ms" in data["metadata"]

    def test_extract_missing_file(self, client):
        """Test extraction without file returns 422"""
        # Act
        response = client.post("/api/v1/extract", data={"strategy": "fast"})

        # Assert
        assert response.status_code == 422  # Validation error

    def test_extract_invalid_content_type(self, client):
        """Test extraction with non-PDF file returns 415"""
        # Arrange
        txt_content = BytesIO(b"Not a PDF file")

        # Act
        response = client.post(
            "/api/v1/extract",
            files={"file": ("test.txt", txt_content, "text/plain")},
            data={"strategy": "fast"},
        )

        # Assert
        assert response.status_code == 415
        data = response.json()
        assert "error" in data
        assert data["error"]["code"] == "UNSUPPORTED_MEDIA_TYPE"

    @patch("src.main.pdf_service.extract_async")
    def test_extract_file_too_large(self, mock_extract, client):
        """Test extraction with file exceeding size limit returns 413"""
        # Arrange
        large_content = BytesIO(b"A" * (60 * 1024 * 1024))  # 60MB (exceeds 50MB limit)

        # Act
        response = client.post(
            "/api/v1/extract",
            files={"file": ("large.pdf", large_content, "application/pdf")},
            data={"strategy": "fast"},
        )

        # Assert
        assert response.status_code == 413
        data = response.json()
        assert "error" in data
        assert data["error"]["code"] == "FILE_TOO_LARGE"

    @patch("src.main.pdf_service.extract_async")
    def test_extract_service_error(self, mock_extract, client, mock_pdf_content):
        """Test extraction service error returns 500"""
        # Arrange
        mock_extract.side_effect = Exception("Service error")

        # Act
        response = client.post(
            "/api/v1/extract",
            files={"file": ("test.pdf", mock_pdf_content, "application/pdf")},
            data={"strategy": "fast"},
        )

        # Assert
        assert response.status_code == 500
        data = response.json()
        assert "error" in data
        assert data["error"]["code"] == "EXTRACTION_FAILED"


class TestHealthEndpoint:
    """Test /health endpoint"""

    def test_health_check_success(self, client):
        """Test health check returns healthy status"""
        # Act
        response = client.get("/health")

        # Assert
        assert response.status_code == 200
        data = response.json()
        assert data["status"] in ["healthy", "unhealthy"]
        assert "timestamp" in data
        assert "checks" in data
        assert "unstructured_library" in data["checks"]


class TestRootEndpoint:
    """Test / root endpoint"""

    def test_root(self, client):
        """Test root endpoint returns service info"""
        # Act
        response = client.get("/")

        # Assert
        assert response.status_code == 200
        data = response.json()
        assert data["service"] == "PDF Extraction Microservice"
        assert data["version"] == "1.0.0"
        assert data["status"] == "running"
