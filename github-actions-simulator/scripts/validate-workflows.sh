#!/bin/bash
# validate-workflows.sh - Validate all GitHub Actions workflows
set -e

echo "🔍 GitHub Actions Workflow Validator"
echo "====================================="
echo ""

cd /workspace/meepleai

WORKFLOW_DIR=".github/workflows"
TOTAL=0
PASSED=0
FAILED=0

echo "📁 Scanning workflows in ${WORKFLOW_DIR}..."
echo ""

for workflow in ${WORKFLOW_DIR}/*.yml ${WORKFLOW_DIR}/*.yaml; do
    if [ -f "${workflow}" ]; then
        TOTAL=$((TOTAL + 1))
        echo "📄 Validating: $(basename ${workflow})"

        # Validate with actionlint
        if actionlint "${workflow}" 2>&1; then
            echo "   ✅ Valid"
            PASSED=$((PASSED + 1))
        else
            echo "   ❌ Invalid"
            FAILED=$((FAILED + 1))
        fi
        echo ""
    fi
done

# Summary
echo "====================================="
echo "📊 Validation Summary:"
echo "   Total workflows: ${TOTAL}"
echo "   ✅ Passed: ${PASSED}"
echo "   ❌ Failed: ${FAILED}"
echo "====================================="

if [ ${FAILED} -gt 0 ]; then
    echo "🔴 Some workflows have issues!"
    exit 1
else
    echo "🟢 All workflows are valid!"
    exit 0
fi
