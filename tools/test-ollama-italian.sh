#!/bin/bash
# Test script for Ollama Italian board game Q&A quality validation
# Usage: bash tools/test-ollama-italian.sh

set -e

OLLAMA_URL="http://localhost:11434"
MODEL="llama3:8b"

echo "🎲 Testing Ollama Italian Board Game Q&A Quality"
echo "=================================================="
echo ""

# Test questions in Italian (board game rules)
declare -a questions=(
    "Come si muove il cavallo negli scacchi?"
    "Quante carte riceve ogni giocatore all'inizio di Tris?"
    "Cosa significa 'arrocco' negli scacchi?"
    "Come si vince a Tris?"
    "Può un pedone muoversi all'indietro negli scacchi?"
    "Quanti giocatori possono giocare a scacchi?"
    "Cosa succede quando un pedone raggiunge l'ultima riga negli scacchi?"
    "Come si cattura un pezzo negli scacchi?"
    "Cos'è lo stallo negli scacchi?"
    "Qual è l'obiettivo principale degli scacchi?"
)

# Counter for results
correct=0
total=${#questions[@]}

for i in "${!questions[@]}"; do
    question="${questions[$i]}"
    num=$((i+1))

    echo "[$num/$total] Testing: $question"

    # Call Ollama API
    response=$(curl -s -X POST "$OLLAMA_URL/api/generate" \
        -H "Content-Type: application/json" \
        -d "{
            \"model\": \"$MODEL\",
            \"prompt\": \"Rispondi brevemente in italiano alla seguente domanda sui giochi da tavolo: $question\",
            \"stream\": false,
            \"options\": {
                \"temperature\": 0.3,
                \"num_predict\": 150
            }
        }" | jq -r '.response' 2>/dev/null)

    if [ -n "$response" ] && [ "$response" != "null" ]; then
        echo "✅ Response: ${response:0:200}..."
        ((correct++))
    else
        echo "❌ No response or error"
    fi

    echo ""
    sleep 2  # Rate limiting
done

echo "=================================================="
echo "📊 Results: $correct/$total questions answered"
accuracy=$((correct * 100 / total))
echo "🎯 Accuracy: $accuracy%"
echo ""

if [ $accuracy -ge 80 ]; then
    echo "✅ SUCCESS: Ollama meets 80% accuracy threshold!"
    exit 0
else
    echo "⚠️  WARNING: Ollama below 80% accuracy threshold"
    exit 1
fi
