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

    @patch("src.main.pdf_service.extract")
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

    @patch("src.main.pdf_service.extract")
    def test_extract_serializes_raw_elements(
        self, mock_extract, client, mock_pdf_content, mock_unstructured_elements
    ):
        """Response must expose raw partition elements (with Title category)."""
        mock_extract.return_value = ExtractionResult(
            full_text="Title of Document\n\nThis is a paragraph.",
            chunks=[TextChunk(text="composite", page_number=1, element_type="CompositeElement")],
            page_count=2,
            elements=mock_unstructured_elements,
            tables=[],
            detected_structures=["Title", "Paragraph", "Table"],
            extraction_duration_ms=1200,
            quality_score=QualityScore(0.85, 0.40, 0.18, 0.15, 0.12),
        )

        response = client.post(
            "/api/v1/extract",
            files={"file": ("test.pdf", mock_pdf_content, "application/pdf")},
            data={"strategy": "fast", "language": "ita"},
        )

        assert response.status_code == 200
        elements = response.json()["elements"]
        assert [e["category"] for e in elements] == ["Title", "Paragraph", "Table"]
        assert elements[0]["text"] == "Title of Document"
        assert elements[0]["page_number"] == 1
        assert elements[2]["page_number"] == 2

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
        # Errors are wrapped under detail.error in current implementation
        assert "detail" in data and "error" in data["detail"]
        assert data["detail"]["error"]["code"] == "UNSUPPORTED_MEDIA_TYPE"

    def test_settings_default_max_file_size_is_100mb(self):
        """The default max_file_size must match the API's PdfProcessing:MaxFileSizeBytes (100MB).

        A lower Python-side cap silently 413s large rulebooks the API already accepted, so the
        two limits MUST stay aligned (staging re-index #3403 hit this: 55-65MB PDFs rejected).
        """
        from src.config.settings import Settings

        assert Settings().max_file_size == 104857600  # 100 MB

    @patch("src.main.pdf_service.extract")
    def test_extract_file_too_large(self, mock_extract, client):
        """A file exceeding the configured size limit returns 413.

        The limit is patched to a small value so the test stays fast + independent of the default.
        """
        from src.config.settings import settings

        # Arrange — cap at 1MB, upload 2MB
        with patch.object(settings, "max_file_size", 1 * 1024 * 1024):
            large_content = BytesIO(b"A" * (2 * 1024 * 1024))
            response = client.post(
                "/api/v1/extract",
                files={"file": ("large.pdf", large_content, "application/pdf")},
                data={"strategy": "fast"},
            )

        # Assert
        assert response.status_code == 413
        data = response.json()
        assert "detail" in data and "error" in data["detail"]
        assert data["detail"]["error"]["code"] == "FILE_TOO_LARGE"

    @patch("src.main.pdf_service.extract")
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
        assert "detail" in data and "error" in data["detail"]
        assert data["detail"]["error"]["code"] == "EXTRACTION_FAILED"


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
