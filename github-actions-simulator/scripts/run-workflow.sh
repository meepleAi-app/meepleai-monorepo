#!/bin/bash
# run-workflow.sh - Execute a GitHub Actions workflow
set -e

WORKFLOW_FILE="${1:-.github/workflows/ci.yml}"
EVENT_TYPE="${2:-push}"
LOG_FILE="/logs/$(date +%Y%m%d_%H%M%S)_$(basename ${WORKFLOW_FILE} .yml).log"

echo "🚀 Running workflow: ${WORKFLOW_FILE}"
echo "📅 Event type: ${EVENT_TYPE}"
echo "📝 Log file: ${LOG_FILE}"
echo ""

# Navigate to repository
cd /workspace/meepleai

# Run act with logging
act "${EVENT_TYPE}" \
    -W "${WORKFLOW_FILE}" \
    --secret-file /workspace/.secrets \
    --env-file /workspace/.actrc \
    --artifact-server-path /artifacts \
    --cache-server-path /cache \
    --container-architecture linux/amd64 \
    --verbose \
    2>&1 | tee "${LOG_FILE}"

EXIT_CODE=${PIPESTATUS[0]}

# Summary
echo ""
echo "==============================================="
if [ ${EXIT_CODE} -eq 0 ]; then
    echo "✅ Workflow completed successfully!"
    echo "🟢 Status: PASSED"
else
    echo "❌ Workflow failed!"
    echo "🔴 Status: FAILED (exit code: ${EXIT_CODE})"
fi
echo "📝 Full log: ${LOG_FILE}"
echo "📦 Artifacts: /artifacts"
echo "==============================================="

exit ${EXIT_CODE}
