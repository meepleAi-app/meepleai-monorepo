#!/bin/bash

# POC Test Script: Agent Search Strategy Comparison
# Tests 3 strategies (RetrievalOnly, SingleModel, MultiModelConsensus) with sample questions

API_URL="http://localhost:8080/api/v1/agents/chat/ask"
RESULTS_FILE="poc-test-results-$(date +%Y%m%d-%H%M%S).md"

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Test Questions (game rules related - adjust based on your PDF content)
declare -a QUESTIONS=(
  "How do I start the game?"
  "What are the winning conditions?"
  "How many players can play?"
  "What is the game setup?"
  "How do turns work?"
)

# Strategies to test
declare -a STRATEGIES=("RetrievalOnly" "SingleModel" "MultiModelConsensus")

echo "=================================="
echo "POC: Agent Search Strategy Testing"
echo "=================================="
echo ""
echo "Testing ${#STRATEGIES[@]} strategies with ${#QUESTIONS[@]} questions"
echo "Results will be saved to: $RESULTS_FILE"
echo ""

# Initialize results file
cat > "$RESULTS_FILE" << 'EOF'
# POC Test Results: Agent Search Strategy Comparison

**Date**: $(date)
**API Endpoint**: http://localhost:8080/api/v1/agents/chat/ask

## Test Configuration

- **Strategies**: RetrievalOnly, SingleModel, MultiModelConsensus
- **Questions**: 5 game-related questions
- **Metrics**: Tokens, Cost, Latency, Chunks Retrieved

---

## Results

EOF

# Function to test a single question with a strategy
test_strategy() {
  local strategy=$1
  local question=$2
  local question_num=$3

  echo -e "${BLUE}[Q${question_num}]${NC} Testing ${YELLOW}${strategy}${NC}: \"${question}\""

  # Make API request
  response=$(curl -s -X POST "$API_URL" \
    -H "Content-Type: application/json" \
    -d "{\"question\": \"$question\", \"strategy\": \"$strategy\"}" \
    2>&1)

  # Check if request failed
  if [ $? -ne 0 ]; then
    echo -e "${RED}✗ Failed${NC}: $response"
    echo "### Question ${question_num}: ${question}" >> "$RESULTS_FILE"
    echo "**Strategy**: ${strategy}" >> "$RESULTS_FILE"
    echo "**Status**: ❌ FAILED" >> "$RESULTS_FILE"
    echo "" >> "$RESULTS_FILE"
    return 1
  fi

  # Parse JSON response (requires jq, fallback to grep if not available)
  if command -v jq &> /dev/null; then
    tokens=$(echo "$response" | jq -r '.tokenUsage.totalTokens // "N/A"')
    cost=$(echo "$response" | jq -r '.costBreakdown.totalCost // "N/A"')
    latency=$(echo "$response" | jq -r '.latencyMs // "N/A"')
    chunks=$(echo "$response" | jq -r '.retrievedChunks | length // "N/A"')
    provider=$(echo "$response" | jq -r '.costBreakdown.provider // "N/A"')
    answer_preview=$(echo "$response" | jq -r '.answer // "No answer (retrieval only)"' | head -c 100)
  else
    # Fallback parsing without jq
    tokens=$(echo "$response" | grep -o '"totalTokens":[0-9]*' | grep -o '[0-9]*' || echo "N/A")
    cost=$(echo "$response" | grep -o '"totalCost":[0-9.]*' | grep -o '[0-9.]*' || echo "N/A")
    latency=$(echo "$response" | grep -o '"latencyMs":[0-9]*' | grep -o '[0-9]*' || echo "N/A")
    chunks=$(echo "$response" | grep -o '"retrievedChunks":\[[^]]*\]' | grep -o '\[' | wc -l || echo "N/A")
    provider="N/A"
    answer_preview="(jq not installed, install for full output)"
  fi

  # Display results
  echo -e "  ${GREEN}✓ Success${NC}"
  echo -e "    Tokens: ${tokens} | Cost: \$${cost} | Latency: ${latency}ms | Chunks: ${chunks}"
  echo -e "    Provider: ${provider}"

  # Write to results file
  cat >> "$RESULTS_FILE" << EOF
### Question ${question_num}: ${question}

**Strategy**: ${strategy}

| Metric | Value |
|--------|-------|
| Tokens | ${tokens} |
| Cost | \$${cost} |
| Latency | ${latency}ms |
| Chunks Retrieved | ${chunks} |
| Provider | ${provider} |

**Answer Preview**:
\`\`\`
${answer_preview}
\`\`\`

---

EOF

  echo ""
}

# Run tests
for question_num in "${!QUESTIONS[@]}"; do
  question="${QUESTIONS[$question_num]}"
  actual_num=$((question_num + 1))

  echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
  echo -e "${YELLOW}Question $actual_num/$((${#QUESTIONS[@]})):${NC} \"$question\""
  echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
  echo ""

  for strategy in "${STRATEGIES[@]}"; do
    test_strategy "$strategy" "$question" "$actual_num"
    sleep 1  # Rate limiting
  done
done

# Summary
echo ""
echo -e "${GREEN}=================================="
echo -e "Test Completed!"
echo -e "==================================${NC}"
echo ""
echo "Results saved to: $RESULTS_FILE"
echo ""
echo "Next steps:"
echo "  1. Open $RESULTS_FILE to view detailed results"
echo "  2. Compare tokens, cost, and latency across strategies"
echo "  3. Test the UI at http://localhost:3000/poc/agent-chat"
echo ""
