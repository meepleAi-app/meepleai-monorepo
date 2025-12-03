import types
import pytest
from fastapi.testclient import TestClient

import src.main as main


@pytest.fixture(autouse=True)
def reset_metrics():
    main.metrics["extract_requests_total"] = 0
    main.metrics["extract_failures_total"] = 0
    main.metrics["extract_duration_ms_sum"] = 0.0
    main.metrics["extract_payload_bytes_sum"] = 0.0
    yield


def _fake_result():
    r = types.SimpleNamespace()
    r.full_text = "ok"
    r.chunks = []
    r.quality_score = types.SimpleNamespace(total_score=1.0, to_dict=lambda: {})
    r.page_count = 1
    r.table_count = 0
    r.detected_structures = []
    r.extraction_duration_ms = 10
    return r


def test_metrics_success(monkeypatch):
    client = TestClient(main.app)

    monkeypatch.setattr(main.pdf_service, "extract", lambda *args, **kwargs: _fake_result())

    files = {"file": ("test.pdf", b"%PDF-1.4\n1 0 obj", "application/pdf")}
    resp = client.post("/api/v1/extract", files=files)
    assert resp.status_code == 200

    assert main.metrics["extract_requests_total"] == 1
    assert main.metrics["extract_failures_total"] == 0
    assert main.metrics["extract_payload_bytes_sum"] > 0


def test_metrics_failure(monkeypatch):
    client = TestClient(main.app)

    def raise_error(*args, **kwargs):
        raise RuntimeError("boom")

    monkeypatch.setattr(main.pdf_service, "extract", raise_error)

    files = {"file": ("test.pdf", b"%PDF-1.4\n1 0 obj", "application/pdf")}
    resp = client.post("/api/v1/extract", files=files)
    assert resp.status_code == 500

    assert main.metrics["extract_requests_total"] == 1
    assert main.metrics["extract_failures_total"] == 1
