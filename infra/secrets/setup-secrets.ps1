# MeepleAI Secrets Setup Script with Multi-Environment Support
# Issue #2513 - Auto-generate secure secrets during setup
# Usage:
#   .\setup-secrets.ps1 -Environment dev -SaveGenerated    # Auto-generate dev secrets
#   .\setup-secrets.ps1 -Environment integration            # Setup integration (tunneled services)
#   .\setup-secrets.ps1 -Environment staging                # Interactive staging setup
#   .\setup-secrets.ps1 -Environment prod                   # Interactive prod (strict validation)

[CmdletBinding()]
param(
    [ValidateSet("dev", "integration", "staging", "prod")]
    [string]$Environment = "dev",
    [switch]$SaveGenerated
)

Write-Host ""
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host "   MeepleAI Secrets Auto-Setup" -ForegroundColor Cyan
Write-Host "   Environment: $Environment" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host ""

# ===========================================================================
# DIRECTORY SETUP
# ===========================================================================

$secretsDir = $PSScriptRoot
$envDir = Join-Path $secretsDir $Environment

# Ensure environment directory exists
if (-not (Test-Path $envDir)) {
    New-Item -ItemType Directory -Path $envDir -Force | Out-Null
    Write-Host "[INFO] Created directory: $envDir" -ForegroundColor Green
}

# Get all .secret.example files from the root secrets directory
$exampleFiles = Get-ChildItem -Path $secretsDir -Filter "*.secret.example"

if ($exampleFiles.Count -eq 0) {
    Write-Host "[ERROR] No .secret.example files found in $secretsDir" -ForegroundColor Red
    Write-Host "        Expected location: infra/secrets/" -ForegroundColor Yellow
    exit 1
}

Write-Host "[INFO] Found $($exampleFiles.Count) example files" -ForegroundColor Green
Write-Host "[INFO] Output directory: $envDir" -ForegroundColor Green
Write-Host ""

# ===========================================================================
# SECURE VALUE GENERATION FUNCTIONS
# ===========================================================================

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
    # NOTE: Avoid characters that break connection strings or shell expansion:
    # - ; = , [ ] { } ( ) < > ' " break connection strings
    # - $ breaks shell variable expansion in env files
    # Safe symbols for PostgreSQL/Redis passwords:
    $symbols = '!@#%^&*_+-~'

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
    if ($RequireSymbol -and $password -notmatch '[!@#%^&*_+\-~]') {
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

function Read-SecretInteractive {
    param(
        [string]$Prompt,
        [int]$MinLength = 0,
        [string]$Default = ""
    )
    while ($true) {
        if ($Default) {
            $input = Read-Host "$Prompt [default: $Default]"
            if ([string]::IsNullOrWhiteSpace($input)) { $input = $Default }
        } else {
            $input = Read-Host $Prompt
        }
        if ($MinLength -gt 0 -and $input.Length -lt $MinLength) {
            Write-Host "  [ERROR] Minimum $MinLength characters required (got $($input.Length))" -ForegroundColor Red
            continue
        }
        return $input
    }
}

# ===========================================================================
# ENVIRONMENT-SPECIFIC LOGIC
# ===========================================================================

switch ($Environment) {

    # -----------------------------------------------------------------------
    # DEV: Auto-generate all secrets (existing behavior, new output path)
    # -----------------------------------------------------------------------
    "dev" {
        Write-Host "[STEP 1] Generating secure values for dev..." -ForegroundColor Yellow
        Write-Host ""

        $generatedValues = @{}

        # JWT Secrets
        $generatedValues['JWT_SECRET_KEY'] = New-SecureApiKey -ByteLength 64
        Write-Host "  [OK] JWT_SECRET_KEY:           $(Mask-Secret $generatedValues['JWT_SECRET_KEY'])" -ForegroundColor Green

        # Database Credentials
        $generatedValues['POSTGRES_PASSWORD'] = New-SecurePassword -Length 20
        Write-Host "  [OK] POSTGRES_PASSWORD:        $(Mask-Secret $generatedValues['POSTGRES_PASSWORD'])" -ForegroundColor Green

        # Redis Password
        $generatedValues['REDIS_PASSWORD'] = New-SecurePassword -Length 20
        Write-Host "  [OK] REDIS_PASSWORD:           $(Mask-Secret $generatedValues['REDIS_PASSWORD'])" -ForegroundColor Green

        # Embedding Service API Key
        $generatedValues['EMBEDDING_SERVICE_API_KEY'] = New-SecureApiKey -ByteLength 32
        Write-Host "  [OK] EMBEDDING_SERVICE_API_KEY: $(Mask-Secret $generatedValues['EMBEDDING_SERVICE_API_KEY'])" -ForegroundColor Green

        # Reranker Service API Key (optional)
        $generatedValues['RERANKER_API_KEY'] = New-SecureApiKey -ByteLength 32
        Write-Host "  [OK] RERANKER_API_KEY:         $(Mask-Secret $generatedValues['RERANKER_API_KEY'])" -ForegroundColor Green

        # SmolDocling Service API Key (optional)
        $generatedValues['SMOLDOCLING_API_KEY'] = New-SecureApiKey -ByteLength 32
        Write-Host "  [OK] SMOLDOCLING_API_KEY:      $(Mask-Secret $generatedValues['SMOLDOCLING_API_KEY'])" -ForegroundColor Green

        # Unstructured Service API Key (optional)
        $generatedValues['UNSTRUCTURED_API_KEY'] = New-SecureApiKey -ByteLength 32
        Write-Host "  [OK] UNSTRUCTURED_API_KEY:     $(Mask-Secret $generatedValues['UNSTRUCTURED_API_KEY'])" -ForegroundColor Green

        # Admin Password
        $generatedValues['ADMIN_PASSWORD'] = New-SecurePassword -Length 16
        Write-Host "  [OK] ADMIN_PASSWORD:           $(Mask-Secret $generatedValues['ADMIN_PASSWORD'])" -ForegroundColor Green

        # Monitoring Passwords (optional)
        $generatedValues['GRAFANA_ADMIN_PASSWORD'] = New-SecurePassword -Length 16
        Write-Host "  [OK] GRAFANA_ADMIN_PASSWORD:   $(Mask-Secret $generatedValues['GRAFANA_ADMIN_PASSWORD'])" -ForegroundColor Green

        $generatedValues['PROMETHEUS_PASSWORD'] = New-SecurePassword -Length 16
        Write-Host "  [OK] PROMETHEUS_PASSWORD:      $(Mask-Secret $generatedValues['PROMETHEUS_PASSWORD'])" -ForegroundColor Green

        # Traefik Dashboard Password (optional)
        $generatedValues['TRAEFIK_DASHBOARD_PASSWORD'] = New-SecurePassword -Length 16
        Write-Host "  [OK] TRAEFIK_DASHBOARD_PASSWORD: $(Mask-Secret $generatedValues['TRAEFIK_DASHBOARD_PASSWORD'])" -ForegroundColor Green

        # S3 Storage Credentials (optional)
        $s3AccessBytes = New-Object byte[] 16
        $s3SecretBytes = New-Object byte[] 32
        $rngS3 = [System.Security.Cryptography.RandomNumberGenerator]::Create()
        $rngS3.GetBytes($s3AccessBytes)
        $rngS3.GetBytes($s3SecretBytes)
        $generatedValues['S3_TOKEN_KEY'] = 'change_me_r2_api_token'
        $generatedValues['S3_ACCESS_KEY'] = [BitConverter]::ToString($s3AccessBytes).Replace('-','').ToLowerInvariant()
        $generatedValues['S3_SECRET_KEY'] = [BitConverter]::ToString($s3SecretBytes).Replace('-','').ToLowerInvariant()
        $generatedValues['S3_ENDPOINT'] = 'change_me_r2_endpoint'
        Write-Host "  [..] S3_TOKEN_KEY:             (requires manual configuration from R2 dashboard)" -ForegroundColor DarkGray
        Write-Host "  [OK] S3_ACCESS_KEY:            $(Mask-Secret $generatedValues['S3_ACCESS_KEY'])" -ForegroundColor Green
        Write-Host "  [OK] S3_SECRET_KEY:            $(Mask-Secret $generatedValues['S3_SECRET_KEY'])" -ForegroundColor Green
        Write-Host "  [..] S3_ENDPOINT:              (requires manual configuration)" -ForegroundColor DarkGray

        Write-Host ""

        # Create and populate secret files in env directory
        Write-Host "[STEP 2] Creating and populating secret files in $Environment/..." -ForegroundColor Yellow
        Write-Host ""

        $copied = 0
        $skipped = 0
        $updated = 0

        foreach ($file in $exampleFiles) {
            $targetName = $file.Name -replace '\.example$', ''
            $targetPath = Join-Path $envDir $targetName

            if (Test-Path $targetPath) {
                Write-Host "  [SKIP] $targetName (already exists)" -ForegroundColor Yellow
                $skipped++
            } else {
                # Copy example file
                Copy-Item $file.FullName -Destination $targetPath

                # Read content
                $content = Get-Content $targetPath -Raw

                # Replace placeholders with generated values
                $replacements = 0

                foreach ($key in $generatedValues.Keys) {
                    $pattern = "$key=change_me[^\r\n]*"
                    if ($content -match [regex]::Escape($key)) {
                        $newLine = "$key=" + $generatedValues[$key]
                        $content = $content -replace $pattern, $newLine
                        $replacements++
                    }
                }

                # Write updated content back
                Set-Content -Path $targetPath -Value $content -NoNewline

                if ($replacements -gt 0) {
                    Write-Host "  [OK] Created: $targetName ($replacements auto-generated)" -ForegroundColor Green
                    $updated++
                } else {
                    Write-Host "  [OK] Created: $targetName (no auto-generation)" -ForegroundColor Green
                }

                $copied++
            }
        }

        Write-Host ""

        # Save generated values backup
        if ($SaveGenerated -and $generatedValues.Count -gt 0) {
            $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
            $backupFile = Join-Path $envDir ".generated-values-$timestamp.txt"

            $backupLines = @()
            $backupLines += "MeepleAI Generated Secrets ($Environment) - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
            $backupLines += "=" * 75
            $backupLines += ""
            $backupLines += "WARNING: This file contains sensitive credentials!"
            $backupLines += "  - Store securely (password manager, encrypted volume)"
            $backupLines += "  - Delete after copying to production vault"
            $backupLines += "  - Never commit to git or share via insecure channels"
            $backupLines += ""
            $backupLines += "=" * 75
            $backupLines += ""

            foreach ($key in ($generatedValues.Keys | Sort-Object)) {
                $backupLines += "$key=$($generatedValues[$key])"
            }

            $backupContent = $backupLines -join "`r`n"
            Set-Content -Path $backupFile -Value $backupContent

            Write-Host "[BACKUP] Saved to: $Environment/$(Split-Path $backupFile -Leaf)" -ForegroundColor Cyan
            Write-Host "         Keep SECURE and DELETE after copying to vault!" -ForegroundColor Yellow
            Write-Host ""
        }

        # Summary
        Write-Host "=======================================" -ForegroundColor Cyan
        Write-Host "   Summary ($Environment)" -ForegroundColor Cyan
        Write-Host "=======================================" -ForegroundColor Cyan
        Write-Host "  Total files:     $($exampleFiles.Count)" -ForegroundColor White
        Write-Host "  Created:         $copied files" -ForegroundColor Green
        Write-Host "  Auto-populated:  $updated files" -ForegroundColor Green
        Write-Host "  Skipped:         $skipped files (already existed)" -ForegroundColor Yellow
        Write-Host ""

        if ($copied -gt 0) {
            Write-Host "Files still requiring manual configuration:" -ForegroundColor Yellow
            Write-Host "  IMPORTANT: bgg.secret, openrouter.secret" -ForegroundColor Yellow
            Write-Host "  OPTIONAL:  email.secret, oauth.secret, storage.secret" -ForegroundColor Gray
            Write-Host ""
            Write-Host "Next steps:" -ForegroundColor Cyan
            Write-Host "   1. Review auto-generated values in $Environment/*.secret files" -ForegroundColor White
            Write-Host "   2. Configure optional services (BGG, OpenRouter, etc.)" -ForegroundColor White
            Write-Host "   3. Start services: cd ../.. && make dev-core" -ForegroundColor White
            Write-Host ""
        }
    }

    # -----------------------------------------------------------------------
    # INTEGRATION: Copy from staging, configure tunnel ports
    # -----------------------------------------------------------------------
    "integration" {
        Write-Host "[INFO] Integration environment uses SSH tunnels to staging services." -ForegroundColor Cyan
        Write-Host "       Local API/Web connect to tunneled Postgres, Redis." -ForegroundColor Cyan
        Write-Host ""

        $stagingDir = Join-Path $secretsDir "staging"
        $stagingSecrets = Get-ChildItem -Path $stagingDir -Filter "*.secret" -ErrorAction SilentlyContinue

        if ($stagingSecrets.Count -gt 0) {
            Write-Host "[QUESTION] Found $($stagingSecrets.Count) staging secrets. Copy to integration? (Y/n)" -ForegroundColor Yellow
            $answer = Read-Host
            if ($answer -ne 'n' -and $answer -ne 'N') {
                foreach ($secret in $stagingSecrets) {
                    $dest = Join-Path $envDir $secret.Name
                    Copy-Item $secret.FullName -Destination $dest -Force
                    Write-Host "  [OK] Copied: $($secret.Name)" -ForegroundColor Green
                }
                Write-Host ""
            }
        } else {
            Write-Host "[INFO] No staging secrets found. Generating from examples..." -ForegroundColor Yellow
            foreach ($file in $exampleFiles) {
                $targetName = $file.Name -replace '\.example$', ''
                $targetPath = Join-Path $envDir $targetName
                if (-not (Test-Path $targetPath)) {
                    Copy-Item $file.FullName -Destination $targetPath
                    Write-Host "  [OK] Created: $targetName (needs manual configuration)" -ForegroundColor Yellow
                }
            }
            Write-Host ""
        }

        # Override database/redis connection settings for tunnel
        Write-Host "[STEP] Configuring tunnel connection overrides..." -ForegroundColor Yellow

        $dbSecretPath = Join-Path $envDir "database.secret"
        if (Test-Path $dbSecretPath) {
            $content = Get-Content $dbSecretPath -Raw
            # Ensure tunnel host/port overrides are present
            if ($content -notmatch 'POSTGRES_HOST=') {
                $content += "`nPOSTGRES_HOST=host.docker.internal"
            } else {
                $content = $content -replace 'POSTGRES_HOST=[^\r\n]*', 'POSTGRES_HOST=host.docker.internal'
            }
            if ($content -notmatch 'POSTGRES_PORT=') {
                $content += "`nPOSTGRES_PORT=15432"
            } else {
                $content = $content -replace 'POSTGRES_PORT=[^\r\n]*', 'POSTGRES_PORT=15432'
            }
            Set-Content -Path $dbSecretPath -Value $content -NoNewline
            Write-Host "  [OK] database.secret: POSTGRES_HOST=host.docker.internal, POSTGRES_PORT=15432" -ForegroundColor Green
        }

        $redisSecretPath = Join-Path $envDir "redis.secret"
        if (Test-Path $redisSecretPath) {
            $content = Get-Content $redisSecretPath -Raw
            if ($content -notmatch 'REDIS_HOST=') {
                $content += "`nREDIS_HOST=host.docker.internal"
            } else {
                $content = $content -replace 'REDIS_HOST=[^\r\n]*', 'REDIS_HOST=host.docker.internal'
            }
            if ($content -notmatch 'REDIS_PORT=') {
                $content += "`nREDIS_PORT=16379"
            } else {
                $content = $content -replace 'REDIS_PORT=[^\r\n]*', 'REDIS_PORT=16379'
            }
            Set-Content -Path $redisSecretPath -Value $content -NoNewline
            Write-Host "  [OK] redis.secret: REDIS_HOST=host.docker.internal, REDIS_PORT=16379" -ForegroundColor Green
        }

        # Generate services-auth.secret for basic auth credentials (used by integration to access staging AI services)
        Write-Host ""
        Write-Host "[STEP] Generating services-auth.secret..." -ForegroundColor Yellow
        $servicesAuthPath = Join-Path $envDir "services-auth.secret"
        $integrationUser = "integration"
        $integrationPass = New-SecurePassword -Length 20
        $servicesAuthContent = @(
            "# Basic auth credentials for accessing staging AI services via Traefik"
            "SERVICES_AUTH_USER=$integrationUser"
            "SERVICES_AUTH_PASSWORD=$integrationPass"
        ) -join "`n"
        Set-Content -Path $servicesAuthPath -Value $servicesAuthContent -NoNewline
        Write-Host "  [OK] services-auth.secret: user=$integrationUser, password=$(Mask-Secret $integrationPass)" -ForegroundColor Green

        Write-Host ""
        Write-Host "=======================================" -ForegroundColor Green
        Write-Host "   Integration Setup Complete!" -ForegroundColor Green
        Write-Host "=======================================" -ForegroundColor Green
        Write-Host ""
        Write-Host "Next steps:" -ForegroundColor Cyan
        Write-Host "   1. Start SSH tunnels:  cd .. && make tunnel" -ForegroundColor White
        Write-Host "   2. Start services:     make integration" -ForegroundColor White
        Write-Host ""
    }

    # -----------------------------------------------------------------------
    # STAGING: Interactive prompts, generate htpasswd for basic auth
    # -----------------------------------------------------------------------
    "staging" {
        Write-Host "[INFO] Staging environment requires interactive configuration." -ForegroundColor Cyan
        Write-Host ""

        # Copy examples and prompt for values
        foreach ($file in $exampleFiles) {
            $targetName = $file.Name -replace '\.example$', ''
            $targetPath = Join-Path $envDir $targetName

            if (Test-Path $targetPath) {
                Write-Host "  [SKIP] $targetName (already exists)" -ForegroundColor Yellow
                continue
            }

            # Copy example as starting point
            Copy-Item $file.FullName -Destination $targetPath
            Write-Host "  [OK] Created: $targetName" -ForegroundColor Green
        }

        Write-Host ""
        Write-Host "[STEP] Configuring Traefik basic auth for staging..." -ForegroundColor Yellow
        Write-Host "       This protects /services/* endpoints for integration access." -ForegroundColor Cyan

        $traefikSecretPath = Join-Path $envDir "traefik.secret"
        $authUser = Read-SecretInteractive -Prompt "  Basic auth username" -Default "integration"
        $authPass = Read-SecretInteractive -Prompt "  Basic auth password" -MinLength 8

        # Generate htpasswd-compatible hash (bcrypt via openssl or fallback)
        $htpasswdEntry = ""
        try {
            # Try using htpasswd if available
            $htpasswdEntry = & htpasswd -nbB $authUser $authPass 2>$null
        } catch {
            # Fallback: use openssl
            try {
                $htpasswdEntry = & openssl passwd -apr1 $authPass 2>$null
                $htpasswdEntry = "${authUser}:${htpasswdEntry}"
            } catch {
                Write-Host "  [WARN] Cannot generate htpasswd hash (htpasswd/openssl not found)." -ForegroundColor Yellow
                Write-Host "         You will need to generate it manually." -ForegroundColor Yellow
                $htpasswdEntry = "${authUser}:GENERATE_HTPASSWD_HASH_HERE"
            }
        }

        # Double the $ signs for Docker Compose interpolation
        $htpasswdEscaped = $htpasswdEntry -replace '\$', '$$'

        $traefikContent = @(
            "# Traefik configuration for staging"
            "# Basic auth for integration access to /services/* endpoints"
            "INTEGRATION_BASIC_AUTH=$htpasswdEscaped"
            "TRAEFIK_DASHBOARD_PASSWORD=$authPass"
        ) -join "`n"
        Set-Content -Path $traefikSecretPath -Value $traefikContent -NoNewline
        Write-Host "  [OK] traefik.secret: INTEGRATION_BASIC_AUTH configured" -ForegroundColor Green

        Write-Host ""
        Write-Host "=======================================" -ForegroundColor Green
        Write-Host "   Staging Setup Complete!" -ForegroundColor Green
        Write-Host "=======================================" -ForegroundColor Green
        Write-Host ""
        Write-Host "Review and edit secrets in: $envDir" -ForegroundColor Cyan
        Write-Host "Deploy: make staging" -ForegroundColor Cyan
        Write-Host ""
    }

    # -----------------------------------------------------------------------
    # PROD: Interactive with strict validation (min 16 char passwords)
    # -----------------------------------------------------------------------
    "prod" {
        Write-Host "[INFO] Production environment requires strict security." -ForegroundColor Cyan
        Write-Host "       All passwords must be at least 16 characters." -ForegroundColor Cyan
        Write-Host ""

        # Copy examples
        foreach ($file in $exampleFiles) {
            $targetName = $file.Name -replace '\.example$', ''
            $targetPath = Join-Path $envDir $targetName

            if (Test-Path $targetPath) {
                Write-Host "  [SKIP] $targetName (already exists)" -ForegroundColor Yellow
                continue
            }

            Copy-Item $file.FullName -Destination $targetPath
            Write-Host "  [OK] Created: $targetName (needs configuration)" -ForegroundColor Yellow
        }

        Write-Host ""
        Write-Host "[STEP] Configuring critical production secrets..." -ForegroundColor Yellow
        Write-Host ""

        # Database
        $dbPath = Join-Path $envDir "database.secret"
        if (Test-Path $dbPath) {
            $pgPass = Read-SecretInteractive -Prompt "  PostgreSQL password (min 16 chars)" -MinLength 16
            $content = Get-Content $dbPath -Raw
            $content = $content -replace 'POSTGRES_PASSWORD=change_me[^\r\n]*', "POSTGRES_PASSWORD=$pgPass"
            Set-Content -Path $dbPath -Value $content -NoNewline
            Write-Host "  [OK] database.secret configured" -ForegroundColor Green
        }

        # Redis
        $redisPath = Join-Path $envDir "redis.secret"
        if (Test-Path $redisPath) {
            $redisPass = Read-SecretInteractive -Prompt "  Redis password (min 16 chars)" -MinLength 16
            $content = Get-Content $redisPath -Raw
            $content = $content -replace 'REDIS_PASSWORD=change_me[^\r\n]*', "REDIS_PASSWORD=$redisPass"
            Set-Content -Path $redisPath -Value $content -NoNewline
            Write-Host "  [OK] redis.secret configured" -ForegroundColor Green
        }

        # JWT
        $jwtPath = Join-Path $envDir "jwt.secret"
        if (Test-Path $jwtPath) {
            $jwtKey = New-SecureApiKey -ByteLength 64
            $content = Get-Content $jwtPath -Raw
            $content = $content -replace 'JWT_SECRET_KEY=change_me[^\r\n]*', "JWT_SECRET_KEY=$jwtKey"
            Set-Content -Path $jwtPath -Value $content -NoNewline
            Write-Host "  [OK] jwt.secret: auto-generated 64-byte key" -ForegroundColor Green
        }

        # Admin
        $adminPath = Join-Path $envDir "admin.secret"
        if (Test-Path $adminPath) {
            $adminPass = Read-SecretInteractive -Prompt "  Admin password (min 16 chars)" -MinLength 16
            $content = Get-Content $adminPath -Raw
            $content = $content -replace 'ADMIN_PASSWORD=change_me[^\r\n]*', "ADMIN_PASSWORD=$adminPass"
            Set-Content -Path $adminPath -Value $content -NoNewline
            Write-Host "  [OK] admin.secret configured" -ForegroundColor Green
        }

        Write-Host ""
        Write-Host "=======================================" -ForegroundColor Green
        Write-Host "   Production Setup Complete!" -ForegroundColor Green
        Write-Host "=======================================" -ForegroundColor Green
        Write-Host ""
        Write-Host "[WARNING] Review ALL secret files before deploying!" -ForegroundColor Red
        Write-Host "          Directory: $envDir" -ForegroundColor Cyan
        Write-Host "          Deploy: PROD_DOMAIN=your.domain make prod" -ForegroundColor Cyan
        Write-Host ""
    }
}

# ===========================================================================
# VALIDATION SUMMARY
# ===========================================================================

Write-Host "=======================================" -ForegroundColor Cyan
Write-Host "   Validation Status ($Environment)" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host ""

$criticalFiles = @('admin.secret', 'database.secret', 'jwt.secret', 'redis.secret', 'embedding-service.secret')
$allCriticalExist = $true

foreach ($criticalFile in $criticalFiles) {
    $path = Join-Path $envDir $criticalFile
    if (Test-Path $path) {
        Write-Host "  [OK] $criticalFile" -ForegroundColor Green
    } else {
        Write-Host "  [MISSING] $criticalFile (startup will fail!)" -ForegroundColor Red
        $allCriticalExist = $false
    }
}

Write-Host ""

if ($allCriticalExist) {
    Write-Host "[SUCCESS] All CRITICAL secrets configured for $Environment!" -ForegroundColor Green
    Write-Host "          Application can start successfully." -ForegroundColor Green
} else {
    Write-Host "[WARNING] Some CRITICAL secrets are missing for $Environment!" -ForegroundColor Red
    Write-Host "          Application WILL NOT START until configured." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Done!" -ForegroundColor Green
Write-Host ""
