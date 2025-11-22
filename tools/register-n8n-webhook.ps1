# N8N-01: Register n8n Webhook Configuration
# This script registers the n8n webhook in the MeepleAI database
# using the /admin/n8n endpoints

param(
    [Parameter(Mandatory=$false)]
    [string]$ApiBaseUrl = "http://localhost:8080",

    [Parameter(Mandatory=$false)]
    [string]$N8nBaseUrl = "http://localhost:5678",

    [Parameter(Mandatory=$false)]
    [string]$WebhookUrl = "http://n8n:5678/webhook/explain",

    [Parameter(Mandatory=$false)]
    [string]$AdminEmail = "admin@meepleai.dev",

    [Parameter(Mandatory=$false)]
    [string]$AdminPassword = "Demo123!",

    [Parameter(Mandatory=$false)]
    [string]$N8nApiKey,

    [Parameter(Mandatory=$false)]
    [string]$ConfigName = "Agent Explain Webhook"
)

$ErrorActionPreference = "Stop"

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host "N8N-01: n8n Webhook Registration" -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host ""

# Generate n8n API key if not provided
if (-not $N8nApiKey) {
    Write-Host "⚠️  n8n API key not provided. Using placeholder." -ForegroundColor Yellow
    Write-Host "   Update the configuration later with a real API key from n8n settings." -ForegroundColor Yellow
    $N8nApiKey = "PLACEHOLDER-UPDATE-VIA-N8N-SETTINGS"
}

# Step 1: Authenticate as admin
Write-Host "Step 1: Authenticating as admin..." -ForegroundColor Cyan

$loginPayload = @{
    email = $AdminEmail
    password = $AdminPassword
} | ConvertTo-Json

try {
    $authResponse = Invoke-WebRequest `
        -Uri "$ApiBaseUrl/auth/login" `
        -Method POST `
        -ContentType "application/json" `
        -Body $loginPayload `
        -SessionVariable session

    if ($authResponse.StatusCode -eq 200) {
        # Extract session cookie
        $sessionCookie = $session.Cookies.GetCookies("$ApiBaseUrl") | Where-Object { $_.Name -eq "session" }

        if ($sessionCookie) {
            $adminSession = $sessionCookie.Value
            Write-Host "✓ Authenticated as admin" -ForegroundColor Green
        } else {
            Write-Host "✗ No session cookie found" -ForegroundColor Red
            exit 1
        }
    }
} catch {
    Write-Host "✗ Authentication failed: $_" -ForegroundColor Red
    Write-Host "   Make sure the API is running and admin credentials are correct" -ForegroundColor Yellow
    exit 1
}

# Step 2: Check if webhook configuration already exists
Write-Host ""
Write-Host "Step 2: Checking existing configurations..." -ForegroundColor Cyan

try {
    $headers = @{
        "Cookie" = "session=$adminSession"
    }

    $configsResponse = Invoke-WebRequest `
        -Uri "$ApiBaseUrl/admin/n8n" `
        -Method GET `
        -Headers $headers

    if ($configsResponse.StatusCode -eq 200) {
        $configs = ($configsResponse.Content | ConvertFrom-Json).configs
        $existingConfig = $configs | Where-Object { $_.name -eq $ConfigName }

        if ($existingConfig) {
            Write-Host "✓ Found existing configuration: $($existingConfig.id)" -ForegroundColor Yellow
            Write-Host "  Name: $($existingConfig.name)" -ForegroundColor Gray
            Write-Host "  BaseUrl: $($existingConfig.baseUrl)" -ForegroundColor Gray
            Write-Host "  WebhookUrl: $($existingConfig.webhookUrl)" -ForegroundColor Gray
            Write-Host "  IsActive: $($existingConfig.isActive)" -ForegroundColor Gray

            $updateExisting = Read-Host "Update existing configuration? (y/n)"
            if ($updateExisting -eq "y") {
                $configId = $existingConfig.id
                $isUpdate = $true
            } else {
                Write-Host "✓ Keeping existing configuration" -ForegroundColor Green
                exit 0
            }
        } else {
            Write-Host "✓ No existing configuration found" -ForegroundColor Green
            $isUpdate = $false
        }
    }
} catch {
    Write-Host "✗ Failed to check existing configurations: $_" -ForegroundColor Red
    exit 1
}

# Step 3: Create or update configuration
Write-Host ""
if ($isUpdate) {
    Write-Host "Step 3: Updating webhook configuration..." -ForegroundColor Cyan

    $updatePayload = @{
        name = $ConfigName
        baseUrl = $N8nBaseUrl
        apiKey = $N8nApiKey
        webhookUrl = $WebhookUrl
        isActive = $true
    } | ConvertTo-Json

    try {
        $updateResponse = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/admin/n8n/$configId" `
            -Method PUT `
            -ContentType "application/json" `
            -Headers $headers `
            -Body $updatePayload

        if ($updateResponse.StatusCode -eq 200) {
            $config = $updateResponse.Content | ConvertFrom-Json
            Write-Host "✓ Configuration updated successfully" -ForegroundColor Green
        }
    } catch {
        Write-Host "✗ Failed to update configuration: $_" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "Step 3: Creating webhook configuration..." -ForegroundColor Cyan

    $createPayload = @{
        name = $ConfigName
        baseUrl = $N8nBaseUrl
        apiKey = $N8nApiKey
        webhookUrl = $WebhookUrl
    } | ConvertTo-Json

    try {
        $createResponse = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/admin/n8n" `
            -Method POST `
            -ContentType "application/json" `
            -Headers $headers `
            -Body $createPayload

        if ($createResponse.StatusCode -eq 200) {
            $config = $createResponse.Content | ConvertFrom-Json
            $configId = $config.id
            Write-Host "✓ Configuration created successfully" -ForegroundColor Green
        }
    } catch {
        Write-Host "✗ Failed to create configuration: $_" -ForegroundColor Red
        Write-Host "   Response: $($_.Exception.Message)" -ForegroundColor Yellow
        exit 1
    }
}

# Step 4: Test connection to n8n
Write-Host ""
Write-Host "Step 4: Testing n8n connection..." -ForegroundColor Cyan

try {
    $testResponse = Invoke-WebRequest `
        -Uri "$ApiBaseUrl/admin/n8n/$configId/test" `
        -Method POST `
        -Headers $headers

    if ($testResponse.StatusCode -eq 200) {
        $testResult = $testResponse.Content | ConvertFrom-Json

        if ($testResult.success) {
            Write-Host "✓ Connection test successful" -ForegroundColor Green
            Write-Host "  Latency: $($testResult.latencyMs)ms" -ForegroundColor Gray
        } else {
            Write-Host "⚠️  Connection test failed" -ForegroundColor Yellow
            Write-Host "  Message: $($testResult.message)" -ForegroundColor Yellow
            Write-Host "  This is expected if n8n is not running yet" -ForegroundColor Gray
        }
    }
} catch {
    Write-Host "⚠️  Could not test connection: $_" -ForegroundColor Yellow
    Write-Host "   Make sure n8n is running and the API key is correct" -ForegroundColor Gray
}

# Step 5: Display configuration details
Write-Host ""
Write-Host "Step 5: Configuration details" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Configuration ID: $configId" -ForegroundColor White
Write-Host "  Name: $ConfigName" -ForegroundColor White
Write-Host "  n8n Base URL: $N8nBaseUrl" -ForegroundColor White
Write-Host "  Webhook URL: $WebhookUrl" -ForegroundColor White
Write-Host "  Status: Active" -ForegroundColor Green
Write-Host ""

# Step 6: Summary
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host "Registration Complete!" -ForegroundColor Green
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Start n8n: docker compose up -d n8n" -ForegroundColor White
Write-Host "2. Import workflow in n8n UI: infra/n8n/workflows/agent-explain-orchestrator.json" -ForegroundColor White
Write-Host "3. Activate the workflow in n8n" -ForegroundColor White
Write-Host "4. Update n8n API key if using placeholder:" -ForegroundColor White
Write-Host "   PUT $ApiBaseUrl/admin/n8n/$configId" -ForegroundColor Gray
Write-Host "5. Test webhook:" -ForegroundColor White
Write-Host "   POST $WebhookUrl" -ForegroundColor Gray
Write-Host "   Body: { \"gameId\": \"tic-tac-toe\", \"topic\": \"winning conditions\" }" -ForegroundColor Gray
Write-Host ""
