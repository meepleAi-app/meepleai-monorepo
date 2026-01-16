<#
.SYNOPSIS
    MeepleAI Secrets Setup Script with Auto-Generated Secure Values

.DESCRIPTION
    Copies all .secret.example files to .secret files and automatically generates
    secure values for API keys, passwords, and secrets instead of leaving placeholders.

.EXAMPLE
    .\setup-secrets.ps1

.EXAMPLE
    .\setup-secrets.ps1 -SaveGenerated

.PARAMETER SaveGenerated
    If specified, saves all generated values to .generated-values.txt for backup

.NOTES
    Author: MeepleAI Team
    Issue: #2513 - Auto-generate secure secrets during setup
    Security: Uses cryptographically secure random generation
#>

[CmdletBinding()]
param(
    [switch]$SaveGenerated
)

Write-Host ""
Write-Host "═══════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   MeepleAI Secrets Auto-Setup" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Change to secrets directory
$secretsDir = $PSScriptRoot
Set-Location $secretsDir

# Get all .secret.example files
$exampleFiles = Get-ChildItem -Filter "*.secret.example"

if ($exampleFiles.Count -eq 0) {
    Write-Host "❌ No .secret.example files found in current directory" -ForegroundColor Red
    Write-Host "   Expected location: infra/secrets/" -ForegroundColor Yellow
    exit 1
}

Write-Host "📋 Found $($exampleFiles.Count) example files" -ForegroundColor Green
Write-Host ""

# ═══════════════════════════════════════════════════════════════════════════
# SECURE VALUE GENERATION FUNCTIONS
# ═══════════════════════════════════════════════════════════════════════════

function New-SecureApiKey {
    param([int]$ByteLength = 32)
    $bytes = New-Object byte[] $ByteLength
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($bytes)
    return [Convert]::ToBase64String($bytes)
}

function New-SecurePassword {
    param(
        [int]$Length = 16,
        [bool]$RequireUppercase = $true,
        [bool]$RequireDigit = $true,
        [bool]$RequireSymbol = $true
    )

    $lowercase = 'abcdefghijklmnopqrstuvwxyz'
    $uppercase = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'
    $digits = '0123456789'
    $symbols = '!@#$%^&*()_+-=[]{}|;:,.<>?'

    $chars = $lowercase.ToCharArray()
    if ($RequireUppercase) { $chars += $uppercase.ToCharArray() }
    if ($RequireDigit) { $chars += $digits.ToCharArray() }
    if ($RequireSymbol) { $chars += $symbols.ToCharArray() }

    $password = -join (1..$Length | ForEach-Object {
        $chars | Get-Random
    })

    # Ensure requirements are met
    if ($RequireUppercase -and $password -notmatch '[A-Z]') {
        $password = $password.Substring(0, $Length - 1) + ($uppercase.ToCharArray() | Get-Random)
    }
    if ($RequireDigit -and $password -notmatch '\d') {
        $password = $password.Substring(0, $Length - 1) + ($digits.ToCharArray() | Get-Random)
    }
    if ($RequireSymbol -and $password -notmatch '[!@#$%^&*()_+\-=\[\]{}|;:,.<>?]') {
        $password = $password.Substring(0, $Length - 1) + ($symbols.ToCharArray() | Get-Random)
    }

    return $password
}

function Mask-Secret {
    param([string]$Value, [int]$ShowChars = 8)
    if ($Value.Length -le $ShowChars) {
        return "***"
    }
    $start = $Value.Substring(0, [Math]::Min($ShowChars, $Value.Length))
    return "$start***"
}

# ═══════════════════════════════════════════════════════════════════════════
# GENERATE SECURE VALUES
# ═══════════════════════════════════════════════════════════════════════════

Write-Host "🔐 Generating secure values..." -ForegroundColor Yellow
Write-Host ""

$generatedValues = @{}

# JWT Secrets
$generatedValues['JWT_SECRET_KEY'] = New-SecureApiKey -ByteLength 64
Write-Host "  ✅ JWT_SECRET_KEY:           $(Mask-Secret $generatedValues['JWT_SECRET_KEY'])" -ForegroundColor Green

# Database Credentials
$generatedValues['POSTGRES_PASSWORD'] = New-SecurePassword -Length 20
Write-Host "  ✅ POSTGRES_PASSWORD:        $(Mask-Secret $generatedValues['POSTGRES_PASSWORD'])" -ForegroundColor Green

# Redis Password
$generatedValues['REDIS_PASSWORD'] = New-SecurePassword -Length 20
Write-Host "  ✅ REDIS_PASSWORD:           $(Mask-Secret $generatedValues['REDIS_PASSWORD'])" -ForegroundColor Green

# Qdrant API Key
$generatedValues['QDRANT_API_KEY'] = New-SecureApiKey -ByteLength 32
Write-Host "  ✅ QDRANT_API_KEY:           $(Mask-Secret $generatedValues['QDRANT_API_KEY'])" -ForegroundColor Green

# Embedding Service API Key
$generatedValues['EMBEDDING_SERVICE_API_KEY'] = New-SecureApiKey -ByteLength 32
Write-Host "  ✅ EMBEDDING_SERVICE_API_KEY: $(Mask-Secret $generatedValues['EMBEDDING_SERVICE_API_KEY'])" -ForegroundColor Green

# Reranker Service API Key (optional)
$generatedValues['RERANKER_API_KEY'] = New-SecureApiKey -ByteLength 32
Write-Host "  ✅ RERANKER_API_KEY:         $(Mask-Secret $generatedValues['RERANKER_API_KEY'])" -ForegroundColor Green

# SmolDocling Service API Key (optional)
$generatedValues['SMOLDOCLING_API_KEY'] = New-SecureApiKey -ByteLength 32
Write-Host "  ✅ SMOLDOCLING_API_KEY:      $(Mask-Secret $generatedValues['SMOLDOCLING_API_KEY'])" -ForegroundColor Green

# Unstructured Service API Key (optional)
$generatedValues['UNSTRUCTURED_API_KEY'] = New-SecureApiKey -ByteLength 32
Write-Host "  ✅ UNSTRUCTURED_API_KEY:     $(Mask-Secret $generatedValues['UNSTRUCTURED_API_KEY'])" -ForegroundColor Green

# Admin Password
$generatedValues['ADMIN_PASSWORD'] = New-SecurePassword -Length 16
Write-Host "  ✅ ADMIN_PASSWORD:           $(Mask-Secret $generatedValues['ADMIN_PASSWORD'])" -ForegroundColor Green

# Monitoring Passwords (optional)
$generatedValues['GRAFANA_ADMIN_PASSWORD'] = New-SecurePassword -Length 16
Write-Host "  ✅ GRAFANA_ADMIN_PASSWORD:   $(Mask-Secret $generatedValues['GRAFANA_ADMIN_PASSWORD'])" -ForegroundColor Green

$generatedValues['PROMETHEUS_PASSWORD'] = New-SecurePassword -Length 16
Write-Host "  ✅ PROMETHEUS_PASSWORD:      $(Mask-Secret $generatedValues['PROMETHEUS_PASSWORD'])" -ForegroundColor Green

# Traefik Dashboard Password (optional)
$generatedValues['TRAEFIK_DASHBOARD_PASSWORD'] = New-SecurePassword -Length 16
Write-Host "  ✅ TRAEFIK_DASHBOARD_PASSWORD: $(Mask-Secret $generatedValues['TRAEFIK_DASHBOARD_PASSWORD'])" -ForegroundColor Green

Write-Host ""

# ═══════════════════════════════════════════════════════════════════════════
# COPY AND POPULATE SECRET FILES
# ═══════════════════════════════════════════════════════════════════════════

Write-Host "📝 Creating and populating secret files..." -ForegroundColor Yellow
Write-Host ""

$copied = 0
$skipped = 0
$updated = 0

foreach ($file in $exampleFiles) {
    $targetName = $file.Name -replace '\.example$', ''
    $targetPath = Join-Path $secretsDir $targetName

    if (Test-Path $targetPath) {
        Write-Host "  ⏭️  Skipped: $targetName (already exists)" -ForegroundColor Yellow
        $skipped++
    } else {
        # Copy example file
        Copy-Item $file.FullName -Destination $targetPath

        # Read content
        $content = Get-Content $targetPath -Raw

        # Replace placeholders with generated values
        $replacements = 0

        foreach ($key in $generatedValues.Keys) {
            # Match pattern: KEY_NAME=change_me...
            $pattern = "$key=change_me[^\r\n]*"

            if ($content -match [regex]::Escape($key)) {
                $newLine = "$key=$($generatedValues[$key])"
                $content = $content -replace $pattern, $newLine
                $replacements++
            }
        }

        # Write updated content back
        Set-Content -Path $targetPath -Value $content -NoNewline

        if ($replacements -gt 0) {
            Write-Host "  ✅ Created: $targetName ($replacements values auto-generated)" -ForegroundColor Green
            $updated++
        } else {
            Write-Host "  ✅ Created: $targetName (no auto-generation needed)" -ForegroundColor Green
        }

        $copied++
    }
}

Write-Host ""

# ═══════════════════════════════════════════════════════════════════════════
# SAVE GENERATED VALUES (OPTIONAL BACKUP)
# ═══════════════════════════════════════════════════════════════════════════

if ($SaveGenerated -and $generatedValues.Count -gt 0) {
    $backupFile = Join-Path $secretsDir ".generated-values-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"

    $backupContent = @"
MeepleAI Generated Secrets - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
═══════════════════════════════════════════════════════════════════════════

⚠️  SECURITY WARNING: This file contains sensitive credentials!
    • Store securely (password manager, encrypted volume)
    • Delete after copying to production vault
    • Never commit to git or share via insecure channels

═══════════════════════════════════════════════════════════════════════════

"@

    foreach ($key in $generatedValues.Keys | Sort-Object) {
        $backupContent += "$key=$($generatedValues[$key])`n"
    }

    Set-Content -Path $backupFile -Value $backupContent
    Write-Host "💾 Saved generated values to: $(Split-Path $backupFile -Leaf)" -ForegroundColor Cyan
    Write-Host "   ⚠️  Keep this file SECURE and DELETE after copying to vault!" -ForegroundColor Yellow
    Write-Host ""
}

# ═══════════════════════════════════════════════════════════════════════════
# SUMMARY
# ═══════════════════════════════════════════════════════════════════════════

Write-Host "═══════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   Summary" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  📦 Total files:     $($exampleFiles.Count)" -ForegroundColor White
Write-Host "  ✅ Created:         $copied files" -ForegroundColor Green
Write-Host "  🔐 Auto-populated:  $updated files" -ForegroundColor Green
Write-Host "  ⏭️  Skipped:         $skipped files (already existed)" -ForegroundColor Yellow
Write-Host ""

if ($copied -gt 0) {
    Write-Host "═══════════════════════════════════════" -ForegroundColor Green
    Write-Host "   ✅ Setup Complete!" -ForegroundColor Green
    Write-Host "═══════════════════════════════════════" -ForegroundColor Green
    Write-Host ""

    if ($updated -gt 0) {
        Write-Host "🎉 $updated files have been auto-populated with secure values!" -ForegroundColor Green
        Write-Host ""
    }

    Write-Host "📋 Files still requiring manual configuration:" -ForegroundColor Yellow
    Write-Host ""

    Write-Host "  🟡 IMPORTANT (Optional features):" -ForegroundColor Yellow
    Write-Host "     • bgg.secret           → BoardGameGeek username/password" -ForegroundColor White
    Write-Host "     • openrouter.secret    → OpenRouter API key (get from openrouter.ai)" -ForegroundColor White
    Write-Host ""

    Write-Host "  🟢 OPTIONAL (Advanced features):" -ForegroundColor Gray
    Write-Host "     • email.secret         → SMTP credentials for email notifications" -ForegroundColor White
    Write-Host "     • oauth.secret         → Google/GitHub OAuth client IDs" -ForegroundColor White
    Write-Host "     • storage.secret       → S3 credentials for cloud storage" -ForegroundColor White
    Write-Host ""

    Write-Host "💡 Quick edit: notepad <filename>.secret" -ForegroundColor Cyan
    Write-Host ""

    Write-Host "🚀 Next steps:" -ForegroundColor Cyan
    Write-Host "   1. Review auto-generated values in .secret files" -ForegroundColor White
    Write-Host "   2. Configure optional services (BGG, OpenRouter, etc.)" -ForegroundColor White
    Write-Host "   3. Start services: cd ../.. && docker compose up -d" -ForegroundColor White
    Write-Host ""

} else {
    Write-Host "ℹ️  All secret files already exist. No changes made." -ForegroundColor Cyan
    Write-Host ""
    Write-Host "   To regenerate, delete existing .secret files first:" -ForegroundColor Yellow
    Write-Host "   Remove-Item *.secret" -ForegroundColor Gray
    Write-Host ""
}

# ═══════════════════════════════════════════════════════════════════════════
# VALIDATION SUMMARY
# ═══════════════════════════════════════════════════════════════════════════

Write-Host "═══════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   Validation Status" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$criticalFiles = @('admin.secret', 'database.secret', 'jwt.secret', 'qdrant.secret', 'redis.secret', 'embedding-service.secret')
$allCriticalExist = $true

foreach ($criticalFile in $criticalFiles) {
    if (Test-Path $criticalFile) {
        Write-Host "  ✅ $criticalFile" -ForegroundColor Green
    } else {
        Write-Host "  ❌ $criticalFile (MISSING - startup will fail!)" -ForegroundColor Red
        $allCriticalExist = $false
    }
}

Write-Host ""

if ($allCriticalExist) {
    Write-Host "✅ All CRITICAL secrets configured!" -ForegroundColor Green
    Write-Host "   Application can start successfully." -ForegroundColor Green
} else {
    Write-Host "⚠️  Some CRITICAL secrets are missing!" -ForegroundColor Red
    Write-Host "   Application WILL NOT START until all critical secrets are configured." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Done! ✨" -ForegroundColor Green
Write-Host ""
