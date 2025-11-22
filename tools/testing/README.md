# Testing Utilities

Scripts for testing integrations, comparing quality, and validating functionality.

## Scripts

### 🇮🇹 **test-ollama-italian.sh**
**Purpose:** Test Ollama's Italian language support for board game rules

**What it does:**
- Sends sample Italian prompts to Ollama
- Tests models: llama3.2, mistral, phi
- Validates Italian response quality
- Measures response time and accuracy

**Usage:**
```bash
# Test default model (llama3.2)
bash tools/testing/test-ollama-italian.sh

# Test specific model
bash tools/testing/test-ollama-italian.sh --model mistral

# Verbose output
bash tools/testing/test-ollama-italian.sh --verbose
```

**Sample prompts:**
- "Spiega le regole di Catan in italiano"
- "Come si vince a Ticket to Ride?"
- "Quali sono le azioni disponibili in Agricola?"

**Who:** AI/ML team evaluating model quality
**When:** Selecting Ollama models, validating Italian support
**Requirements:** Ollama running locally, bash

---

### 📊 **compare-llm-quality.ps1**
**Purpose:** Compare output quality across different LLM providers

**What it does:**
- Tests same prompt against multiple LLMs (OpenRouter models, Ollama)
- Measures: response time, token count, accuracy, hallucination rate
- Generates comparison report

**Usage:**
```powershell
# Compare OpenRouter models
.\tools\testing\compare-llm-quality.ps1 -Providers "gpt-4","claude-3.5-sonnet"

# Test with custom prompt
.\tools\testing\compare-llm-quality.ps1 -Prompt "Explain Catan rules" -Providers "gpt-4","llama3.2"

# Generate HTML report
.\tools\testing\compare-llm-quality.ps1 -Report
```

**Output:**
```
LLM Quality Comparison
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Model               Time    Tokens  Accuracy  Hallucination
gpt-4-turbo         1.2s    450     98%       0.5%
claude-3.5-sonnet   0.9s    420     97%       1.2%
llama3.2 (Ollama)   2.1s    380     89%       3.5%
```

**Who:** Product team selecting LLM provider
**When:** Evaluating model performance, cost optimization
**Requirements:** PowerShell 5.1+, OpenRouter API key, Ollama (optional)

---

**Last Updated:** 2025-11-22
**Maintained by:** AI/ML team
