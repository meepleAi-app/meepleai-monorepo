"""Pytest configuration and fixtures"""
import pytest
from pathlib import Path
from io import BytesIO


@pytest.fixture
def mock_pdf_content() -> BytesIO:
    """Mock PDF content for testing"""
    # Minimal valid PDF structure
    pdf_content = b"""%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj
2 0 obj
<< /Type /Pages /Kids [3 0 R] /Count 1 >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R >>
endobj
4 0 obj
<< /Length 44 >>
stream
BT
/F1 12 Tf
100 700 Td
(Test PDF content) Tj
ET
endstream
endobj
xref
0 5
0000000000 65535 f
0000000009 00000 n
0000000058 00000 n
0000000115 00000 n
0000000214 00000 n
trailer
<< /Size 5 /Root 1 0 R >>
startxref
308
%%EOF
"""
    return BytesIO(pdf_content)


@pytest.fixture
def temp_pdf_file(tmp_path: Path, mock_pdf_content: BytesIO) -> Path:
    """Create temporary PDF file for testing"""
    pdf_path = tmp_path / "test.pdf"
    with open(pdf_path, "wb") as f:
        f.write(mock_pdf_content.getvalue())
    return pdf_path


@pytest.fixture
def mock_unstructured_elements():
    """Mock Unstructured element objects"""
    from dataclasses import dataclass
    from typing import Optional

    @dataclass
    class MockMetadata:
        page_number: int
        filename: Optional[str] = None

    @dataclass
    class MockElement:
        text: str
        category: str
        metadata: MockMetadata

    return [
        MockElement(text="Title of Document", category="Title", metadata=MockMetadata(page_number=1)),
        MockElement(text="This is a paragraph.", category="Paragraph", metadata=MockMetadata(page_number=1)),
        MockElement(
            text="Table data: Row 1 | Row 2",
            category="Table",
            metadata=MockMetadata(page_number=2),
        ),
    ]
