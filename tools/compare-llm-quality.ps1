# PowerShell script to compare Ollama vs OpenRouter quality for Italian board game Q&A
# Usage: pwsh tools/compare-llm-quality.ps1

param(
    [string]$OllamaUrl = "http://localhost:11434",
    [string]$OllamaModel = "llama3:8b",
    [string]$OpenRouterKey = $env:OPENROUTER_API_KEY
)

Write-Host "🎲 Comparing LLM Quality: Ollama vs OpenRouter" -ForegroundColor Cyan
Write-Host "=" * 60
Write-Host ""

# Test questions in Italian
$questions = @(
    "Come si muove il cavallo negli scacchi?",
    "Quante carte riceve ogni giocatore all'inizio di Tris?",
    "Cosa significa 'arrocco' negli scacchi?",
    "Come si vince a Tris?",
    "Può un pedone muoversi all'indietro negli scacchi?"
)

$results = @()

foreach ($i in 0..($questions.Length - 1)) {
    $question = $questions[$i]
    $num = $i + 1

    Write-Host "[$num/$($questions.Length)] Testing: $question" -ForegroundColor Yellow

    # Test Ollama
    Write-Host "  🤖 Ollama..." -NoNewline
    try {
        $ollamaBody = @{
            model = $OllamaModel
            prompt = "Rispondi brevemente in italiano alla seguente domanda sui giochi da tavolo: $question"
            stream = $false
            options = @{
                temperature = 0.3
                num_predict = 150
            }
        } | ConvertTo-Json

        $ollamaResponse = Invoke-RestMethod -Uri "$OllamaUrl/api/generate" `
            -Method Post `
            -ContentType "application/json" `
            -Body $ollamaBody `
            -TimeoutSec 30

        $ollamaAnswer = $ollamaResponse.response
        Write-Host " ✅" -ForegroundColor Green
    }
    catch {
        $ollamaAnswer = "ERROR: $_"
        Write-Host " ❌" -ForegroundColor Red
    }

    # Test OpenRouter (if key available)
    if ($OpenRouterKey) {
        Write-Host "  🌐 OpenRouter..." -NoNewline
        try {
            $openrouterBody = @{
                model = "openai/gpt-4o-mini"
                messages = @(
                    @{
                        role = "user"
                        content = "Rispondi brevemente in italiano alla seguente domanda sui giochi da tavolo: $question"
                    }
                )
                temperature = 0.3
                max_tokens = 150
            } | ConvertTo-Json -Depth 10

            $openrouterResponse = Invoke-RestMethod -Uri "https://openrouter.ai/api/v1/chat/completions" `
                -Method Post `
                -Headers @{
                    "Authorization" = "Bearer $OpenRouterKey"
                    "Content-Type" = "application/json"
                } `
                -Body $openrouterBody `
                -TimeoutSec 30

            $openrouterAnswer = $openrouterResponse.choices[0].message.content
            Write-Host " ✅" -ForegroundColor Green
        }
        catch {
            $openrouterAnswer = "ERROR: $_"
            Write-Host " ❌" -ForegroundColor Red
        }
    }
    else {
        $openrouterAnswer = "SKIPPED: No API key"
        Write-Host "  🌐 OpenRouter... ⏭️  (no API key)" -ForegroundColor Gray
    }

    $results += [PSCustomObject]@{
        Question = $question
        Ollama = $ollamaAnswer.Substring(0, [Math]::Min(100, $ollamaAnswer.Length))
        OpenRouter = if ($openrouterAnswer.Length -gt 100) { $openrouterAnswer.Substring(0, 100) } else { $openrouterAnswer }
    }

    Write-Host ""
    Start-Sleep -Seconds 2
}

Write-Host "=" * 60
Write-Host "📊 Results Summary" -ForegroundColor Cyan
Write-Host ""

$results | Format-Table -Wrap -AutoSize

Write-Host ""
Write-Host "✅ Comparison complete! Review responses for quality assessment." -ForegroundColor Green
Write-Host "💡 Next steps: Document findings in memory, decide on architecture" -ForegroundColor Yellow
