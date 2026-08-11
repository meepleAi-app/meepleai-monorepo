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

    @patch("src.main.pdf_service.extract")
    def test_extract_keeps_bbox_bearing_image_regions(self, mock_extract, client, mock_pdf_content):
        """#3435: empty-text Image/FigureCaption WITH a bbox are kept (region grounding), while
        empty-text non-Image elements and Image elements without a bbox are still dropped."""
        from types import SimpleNamespace

        def _el(text, category, page_number, points=None, system_wh=None):
            coordinates = None
            if points and system_wh:
                coordinates = SimpleNamespace(
                    points=points,
                    system=SimpleNamespace(width=system_wh[0], height=system_wh[1]),
                )
            return SimpleNamespace(
                text=text,
                category=category,
                metadata=SimpleNamespace(page_number=page_number, coordinates=coordinates),
            )

        els = [
            _el("Some narrative", "Title", 1, [(0, 0), (10, 10)], (100, 100)),   # text-bearing → kept
            _el("", "Image", 4, [(10, 50), (90, 80)], (100, 100)),              # empty Image + bbox → kept
            _el("", "FigureCaption", 5, [(5, 5), (50, 20)], (100, 100)),        # empty FigureCaption + bbox → kept
            _el("", "Header", 2, [(0, 0), (5, 5)], (100, 100)),                 # empty non-Image → dropped
            _el("", "Image", 3, None, None),                                    # Image w/o bbox → dropped
        ]
        mock_extract.return_value = ExtractionResult(
            full_text="Some narrative",
            chunks=[TextChunk(text="c", page_number=1, element_type="CompositeElement")],
            page_count=4,
            elements=els,
            tables=[],
            detected_structures=["Title"],
            extraction_duration_ms=1000,
            quality_score=QualityScore(0.85, 0.40, 0.18, 0.15, 0.12),
        )

        response = client.post(
            "/api/v1/extract",
            files={"file": ("test.pdf", mock_pdf_content, "application/pdf")},
            data={"strategy": "hi_res", "language": "ita"},
        )

        assert response.status_code == 200
        elements = response.json()["elements"]
        # empty Header + bbox-less Image dropped; empty Image + FigureCaption WITH bbox kept
        assert [e["category"] for e in elements] == ["Title", "Image", "FigureCaption"]
        image = next(e for e in elements if e["category"] == "Image")
        assert image["bbox"] is not None
        assert image["page_number"] == 4
        caption = next(e for e in elements if e["category"] == "FigureCaption")
        assert caption["bbox"] is not None

    @patch("src.main.pdf_service.extract")
    def test_extract_keeps_bbox_bearing_table_regions(self, mock_extract, client, mock_pdf_content):
        """#3565: a graphics-drawn table is labelled 'Table' by hi_res, and its text may be empty
        (or garbled) when OCR cannot read rotated labels. It must survive the filter like the other
        region categories, otherwise the image-table VLM pass (#3435) never sees a real table."""
        from types import SimpleNamespace

        def _el(text, category, page_number, points=None, system_wh=None):
            coordinates = None
            if points and system_wh:
                coordinates = SimpleNamespace(
                    points=points,
                    system=SimpleNamespace(width=system_wh[0], height=system_wh[1]),
                )
            return SimpleNamespace(
                text=text,
                category=category,
                metadata=SimpleNamespace(page_number=page_number, coordinates=coordinates),
            )

        els = [
            _el("", "Table", 5, [(6, 76), (44, 95)], (100, 100)),  # empty Table + bbox → kept
            _el("", "Table", 6, None, None),                       # Table w/o bbox → dropped
        ]
        mock_extract.return_value = ExtractionResult(
            full_text="",
            chunks=[TextChunk(text="c", page_number=1, element_type="CompositeElement")],
            page_count=6,
            elements=els,
            tables=[],
            detected_structures=["Table"],
            extraction_duration_ms=1000,
            quality_score=QualityScore(0.85, 0.40, 0.18, 0.15, 0.12),
        )

        response = client.post(
            "/api/v1/extract",
            files={"file": ("test.pdf", mock_pdf_content, "application/pdf")},
            data={"strategy": "hi_res", "language": "ita"},
        )

        assert response.status_code == 200
        elements = response.json()["elements"]
        assert [e["category"] for e in elements] == ["Table"]
        assert elements[0]["page_number"] == 5
        assert elements[0]["bbox"] is not None

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
