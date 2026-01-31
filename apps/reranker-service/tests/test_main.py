"""
Tests for Cross-Encoder Reranking Service

ADR-016 Phase 4: Unit and integration tests for the reranker service.
"""

import pytest
from fastapi.testclient import TestClient
from unittest.mock import patch, MagicMock
import numpy as np

# Import after mocking to avoid model loading
@pytest.fixture
def mock_model():
    """Mock the CrossEncoder model."""
    with patch("main.CrossEncoder") as mock:
        mock_instance = MagicMock()
        # Return scores that rank chunk-2 higher than chunk-1
        mock_instance.predict.return_value = np.array([0.78, 0.94])
        mock.return_value = mock_instance
        yield mock_instance


@pytest.fixture
def client(mock_model):
    """Create test client with mocked model."""
    # Import here to ensure mock is in place before model loading
    import main
    main.model = mock_model
    main.model_loaded = True
    return TestClient(main.app)


class TestHealthEndpoint:
    """Tests for /health endpoint."""

    def test_health_returns_status(self, client):
        """Health endpoint returns model status."""
        response = client.get("/health")
        assert response.status_code == 200
        data = response.json()
        assert "status" in data
        assert "model_loaded" in data
        assert "model_name" in data
        assert data["model_loaded"] is True

    def test_health_shows_device(self, client):
        """Health endpoint shows device information."""
        response = client.get("/health")
        data = response.json()
        assert "device" in data
        assert data["device"] == "cpu"


class TestRerankEndpoint:
    """Tests for /rerank endpoint."""

    def test_rerank_returns_sorted_results(self, client, mock_model):
        """Rerank endpoint returns chunks sorted by rerank score."""
        request = {
            "query": "How do I set up the game?",
            "chunks": [
                {"id": "chunk-1", "content": "Place tokens", "score": 0.85},
                {"id": "chunk-2", "content": "Each player takes cards", "score": 0.82},
            ]
        }

        response = client.post("/rerank", json=request)
        assert response.status_code == 200
        data = response.json()

        assert "results" in data
        assert len(data["results"]) == 2
        # chunk-2 should be ranked first (score 0.94 > 0.78)
        assert data["results"][0]["id"] == "chunk-2"
        assert data["results"][1]["id"] == "chunk-1"

    def test_rerank_preserves_metadata(self, client, mock_model):
        """Rerank endpoint preserves chunk metadata."""
        request = {
            "query": "test query",
            "chunks": [
                {"id": "c1", "content": "text", "score": 0.5, "metadata": {"page": 5}},
                {"id": "c2", "content": "text2", "score": 0.4, "metadata": {"page": 10}},
            ]
        }

        response = client.post("/rerank", json=request)
        data = response.json()

        # Check metadata is preserved
        for result in data["results"]:
            assert "metadata" in result
            assert "page" in result["metadata"]

    def test_rerank_empty_chunks(self, client):
        """Rerank endpoint handles empty chunks list."""
        request = {
            "query": "test query",
            "chunks": []
        }

        response = client.post("/rerank", json=request)
        assert response.status_code == 200
        data = response.json()
        assert data["results"] == []
        assert data["processing_time_ms"] == 0.0

    def test_rerank_top_k(self, client, mock_model):
        """Rerank endpoint respects top_k parameter."""
        mock_model.predict.return_value = np.array([0.5, 0.9, 0.7])

        request = {
            "query": "test",
            "chunks": [
                {"id": "c1", "content": "text1", "score": 0.5},
                {"id": "c2", "content": "text2", "score": 0.4},
                {"id": "c3", "content": "text3", "score": 0.3},
            ],
            "top_k": 2
        }

        response = client.post("/rerank", json=request)
        data = response.json()

        assert len(data["results"]) == 2

    def test_rerank_includes_processing_time(self, client, mock_model):
        """Rerank endpoint includes processing time in response."""
        request = {
            "query": "test",
            "chunks": [{"id": "c1", "content": "text", "score": 0.5}]
        }
        mock_model.predict.return_value = np.array([0.8])

        response = client.post("/rerank", json=request)
        data = response.json()

        assert "processing_time_ms" in data
        assert data["processing_time_ms"] >= 0

    def test_rerank_includes_model_name(self, client, mock_model):
        """Rerank endpoint includes model name in response."""
        request = {
            "query": "test",
            "chunks": [{"id": "c1", "content": "text", "score": 0.5}]
        }
        mock_model.predict.return_value = np.array([0.8])

        response = client.post("/rerank", json=request)
        data = response.json()

        assert "model" in data
        assert "bge-reranker" in data["model"].lower()


class TestRootEndpoint:
    """Tests for root endpoint."""

    def test_root_returns_service_info(self, client):
        """Root endpoint returns service information."""
        response = client.get("/")
        assert response.status_code == 200
        data = response.json()

        assert "service" in data
        assert "version" in data
        assert "endpoints" in data


class TestValidation:
    """Tests for request validation."""

    def test_rerank_requires_query(self, client):
        """Rerank endpoint requires query field."""
        request = {
            "chunks": [{"id": "c1", "content": "text", "score": 0.5}]
        }

        response = client.post("/rerank", json=request)
        assert response.status_code == 422  # Validation error

    def test_rerank_requires_chunks(self, client):
        """Rerank endpoint requires chunks field."""
        request = {
            "query": "test"
        }

        response = client.post("/rerank", json=request)
        assert response.status_code == 422  # Validation error

    def test_chunk_requires_id_and_content(self, client):
        """Chunk validation requires id and content."""
        request = {
            "query": "test",
            "chunks": [{"score": 0.5}]  # Missing id and content
        }

        response = client.post("/rerank", json=request)
        assert response.status_code == 422  # Validation error


class TestModelNotLoaded:
    """Tests for model unavailability scenarios."""

    def test_rerank_fails_when_model_not_loaded(self):
        """Rerank returns 503 when model not loaded."""
        import main

        # Create client with model not loaded
        main.model = None
        main.model_loaded = False
        client = TestClient(main.app)

        request = {
            "query": "test",
            "chunks": [{"id": "c1", "content": "text", "score": 0.5}]
        }

        response = client.post("/rerank", json=request)
        assert response.status_code == 503
