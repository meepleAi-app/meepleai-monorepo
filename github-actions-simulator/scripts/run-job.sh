#!/bin/bash
# run-job.sh - Execute a specific job from a workflow
set -e

WORKFLOW_FILE="${1:-.github/workflows/ci.yml}"
JOB_NAME="${2}"
EVENT_TYPE="${3:-push}"
LOG_FILE="/logs/$(date +%Y%m%d_%H%M%S)_${JOB_NAME}.log"

if [ -z "${JOB_NAME}" ]; then
    echo "❌ Error: Job name is required"
    echo "Usage: run-job.sh <workflow-file> <job-name> [event-type]"
    echo ""
    echo "Example: run-job.sh .github/workflows/ci.yml ci-api push"
    exit 1
fi

echo "🚀 Running job: ${JOB_NAME}"
echo "📄 Workflow: ${WORKFLOW_FILE}"
echo "📅 Event type: ${EVENT_TYPE}"
echo "📝 Log file: ${LOG_FILE}"
echo ""

# Navigate to repository
cd /workspace/meepleai

# Run act with specific job
act "${EVENT_TYPE}" \
    -W "${WORKFLOW_FILE}" \
    -j "${JOB_NAME}" \
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
    echo "✅ Job '${JOB_NAME}' completed successfully!"
    echo "🟢 Status: PASSED"
else
    echo "❌ Job '${JOB_NAME}' failed!"
    echo "🔴 Status: FAILED (exit code: ${EXIT_CODE})"
fi
echo "📝 Full log: ${LOG_FILE}"
echo "==============================================="

exit ${EXIT_CODE}
