"""Test API schemas for coverage."""

from uuid import uuid4
from datetime import datetime, UTC

from src.api.schemas import ExecuteWorkflowRequest, ExecuteWorkflowResponse, HealthResponse


def test_execute_workflow_request_creation():
    """Test request schema creation."""
    req = ExecuteWorkflowRequest(
        game_id=uuid4(),
        session_id=uuid4(),
        query="Test query"
    )

    assert req.query == "Test query"
    assert req.board_state is None


def test_execute_workflow_response_creation():
    """Test response schema creation."""
    resp = ExecuteWorkflowResponse(
        agent_type="tutor",
        response="Test response",
        confidence=0.9,
        citations=["source1"],
        execution_time_ms=100.0,
        session_id=uuid4()
    )

    assert resp.agent_type == "tutor"
    assert resp.confidence == 0.9


def test_health_response_creation():
    """Test health schema creation."""
    health = HealthResponse(
        status="healthy",
        version="0.1.0",
        dependencies={"redis": "healthy"}
    )

    assert health.status == "healthy"
    assert "redis" in health.dependencies
