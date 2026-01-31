# Test OpenRouter API connectivity
# Usage: pwsh tools/test-openrouter.ps1

$ErrorActionPreference = "Stop"

Write-Host "🔍 Testing OpenRouter API connectivity..." -ForegroundColor Cyan

# Read API key from env file
$envFile = "infra/env/api.env.dev"
if (Test-Path $envFile) {
    $apiKey = (Get-Content $envFile | Select-String "OPENROUTER_API_KEY").ToString().Split('=')[1]
    Write-Host "✓ API key found: ${apiKey.Substring(0, 20)}..." -ForegroundColor Green
} else {
    Write-Host "✗ Environment file not found: $envFile" -ForegroundColor Red
    exit 1
}

# Test OpenRouter API
$headers = @{
    "Authorization" = "Bearer $apiKey"
    "HTTP-Referer" = "https://meepleai.app"
    "Content-Type" = "application/json"
}

$body = @{
    model = "anthropic/claude-3.5-sonnet"
    messages = @(
        @{
            role = "system"
            content = "You are a helpful assistant."
        }
        @{
            role = "user"
            content = "Say 'Hello World' in exactly two words."
        }
    )
    temperature = 0.3
    max_tokens = 50
} | ConvertTo-Json -Depth 10

Write-Host "`n🌐 Testing OpenRouter chat completions endpoint..." -ForegroundColor Cyan

try {
    $response = Invoke-RestMethod -Uri "https://openrouter.ai/api/v1/chat/completions" `
        -Method Post `
        -Headers $headers `
        -Body $body `
        -TimeoutSec 30

    Write-Host "✓ OpenRouter API is working!" -ForegroundColor Green
    Write-Host "  Model: $($response.model)" -ForegroundColor Gray
    Write-Host "  Response: $($response.choices[0].message.content)" -ForegroundColor Gray
    Write-Host "  Tokens: prompt=$($response.usage.prompt_tokens), completion=$($response.usage.completion_tokens), total=$($response.usage.total_tokens)" -ForegroundColor Gray

    exit 0
} catch {
    Write-Host "✗ OpenRouter API call failed!" -ForegroundColor Red
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red

    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "  Response Body: $responseBody" -ForegroundColor Red
    }

    exit 1
}
