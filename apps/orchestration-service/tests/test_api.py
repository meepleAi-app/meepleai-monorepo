"""
ISSUE-3495: Integration tests for FastAPI endpoints.
Tests REST API with mocked orchestrator.
"""

import pytest
from uuid import uuid4
from fastapi.testclient import TestClient


@pytest.fixture
def client():
    """Create test client."""
    # Import here to avoid loading orchestrator at module level
    from main import app
    return TestClient(app)


class TestHealthEndpoint:
    """Test suite for /health endpoint."""

    def test_health_check_returns_200(self, client):
        """Test health check returns 200 OK."""
        response = client.get("/health")

        assert response.status_code == 200
        data = response.json()
        assert "status" in data
        assert "version" in data
        assert "dependencies" in data

    def test_health_check_includes_dependencies(self, client):
        """Test health check reports dependency statuses."""
        response = client.get("/health")
        data = response.json()

        # Should check these dependencies
        assert "orchestrator" in data["dependencies"]

        # Note: Other services may be unhealthy in test environment
        # but orchestrator should be initialized
        assert data["dependencies"]["orchestrator"] in ["healthy", "uninitialized"]


class TestRootEndpoint:
    """Test suite for / endpoint."""

    def test_root_returns_service_info(self, client):
        """Test root endpoint returns service information."""
        response = client.get("/")

        assert response.status_code == 200
        data = response.json()
        assert data["service"] == "MeepleAI Orchestration Service"
        assert data["version"] == "0.1.0"
        assert "agents" in data
        assert "tutor" in data["agents"]


class TestExecuteEndpoint:
    """Test suite for /execute endpoint."""

    def test_execute_workflow_with_valid_request(self, client):
        """Test workflow execution with valid request."""
        request_data = {
            "game_id": str(uuid4()),
            "session_id": str(uuid4()),
            "query": "How do I set up the game?",
        }

        response = client.post("/execute", json=request_data)

        assert response.status_code == 200
        data = response.json()
        assert "agent_type" in data
        assert "response" in data
        assert "confidence" in data
        assert "execution_time_ms" in data
        assert data["error"] is None

    def test_execute_workflow_validates_request(self, client):
        """Test request validation."""
        # Missing required fields
        invalid_request = {
            "game_id": str(uuid4()),
            # missing session_id and query
        }

        response = client.post("/execute", json=invalid_request)

        assert response.status_code == 422  # Validation error


class TestMetricsEndpoint:
    """Test suite for /metrics endpoint."""

    def test_metrics_endpoint_returns_prometheus_format(self, client):
        """Test metrics are exported in Prometheus text format."""
        response = client.get("/metrics")

        assert response.status_code == 200
        assert "workflow_executions_total" in response.text
        assert "workflow_failures_total" in response.text
        assert "# HELP" in response.text
        assert "# TYPE" in response.text
