import pytest
from fastapi.testclient import TestClient

from main import app, model, metrics


@pytest.fixture(autouse=True)
def reset_metrics():
    # clear counters before each test
    metrics["embed_requests_total"] = 0
    metrics["embed_failures_total"] = 0
    metrics["embed_duration_ms_sum"] = 0.0
    metrics["embed_total_chars_sum"] = 0.0
    yield


def test_metrics_increment_on_success(monkeypatch):
    client = TestClient(app)

    class FakeModel:
        def encode(self, texts, convert_to_numpy, show_progress_bar, normalize_embeddings):
            import numpy as np
            # return 2 vectors of size 4
            return np.array([[1, 0, 0, 0], [0, 1, 0, 0]])

        def get_sentence_embedding_dimension(self):
            return 4

    monkeypatch.setattr("main.model", FakeModel())

    resp = client.post("/embeddings", json={"texts": ["a", "b"], "language": "en"})
    assert resp.status_code == 200

    # Metrics should have recorded the request and chars
    assert metrics["embed_requests_total"] == 1
    assert metrics["embed_failures_total"] == 0
    assert metrics["embed_total_chars_sum"] == len("a") + len("b")

    # /metrics endpoint should expose the counters
    metrics_resp = client.get("/metrics")
    body = metrics_resp.text
    assert "embed_requests_total 1" in body
    assert "embed_failures_total 0" in body


def test_metrics_increment_on_failure(monkeypatch):
    client = TestClient(app)

    class FakeModel:
        def encode(self, *args, **kwargs):
            raise RuntimeError("boom")

        def get_sentence_embedding_dimension(self):
            return 4

    monkeypatch.setattr("main.model", FakeModel())

    resp = client.post("/embeddings", json={"texts": ["hi"], "language": "en"})
    assert resp.status_code == 500

    assert metrics["embed_requests_total"] == 1
    assert metrics["embed_failures_total"] == 1
