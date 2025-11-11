# PowerShell script to fix test entity ID assignments from string to Guid

$testFiles = Get-ChildItem -Path "tests\Api.Tests" -Filter "*.cs" -Recurse

foreach ($file in $testFiles) {
    $content = Get-Content $file.FullName -Raw

    # Skip if already processed or no matches
    if (-not $content) { continue }

    $modified = $false

    # Fix Role enum: UserRole.Admin -> "admin", UserRole.Editor -> "editor", UserRole.User -> "user"
    if ($content -match 'Role = UserRole\.(Admin|Editor|User)') {
        $content = $content -replace 'Role = UserRole\.Admin', 'Role = "admin"'
        $content = $content -replace 'Role = UserRole\.Editor', 'Role = "editor"'
        $content = $content -replace 'Role = UserRole\.User', 'Role = "user"'
        $modified = $true
        Write-Host "Fixed Role enum in $($file.Name)" -ForegroundColor Green
    }

    # Fix common test ID patterns
    # Pattern 1: Id = "test-id" -> Id = Guid.NewGuid()
    if ($content -match 'Id = "[^"]*"') {
        # Don't replace if it's in a comment or attribute
        $lines = $content -split "`n"
        $newLines = @()

        foreach ($line in $lines) {
            # Skip if it's a string property for DTOs (EvaluationId, DatasetId, etc which should remain strings)
            if ($line -match '(EvaluationId|DatasetId|TestCaseId|Name|Email|Description)\s*=\s*"') {
                $newLines += $line
                continue
            }

            # Fix entity Id properties
            if ($line -match '^\s*Id\s*=\s*"[^"]*"') {
                $line = $line -replace 'Id\s*=\s*"[^"]*"', 'Id = Guid.NewGuid()'
                $modified = $true
            }

            # Fix FK properties ending with Id (UserId, GameId, etc)
            if ($line -match '(UserId|GameId|TemplateId|VersionId|ApiKeyId|SessionId|AgentId|ConfigId|CreatedByUserId|UpdatedByUserId)\s*=\s*"[^"]*"') {
                $line = $line -replace '(UserId|GameId|TemplateId|VersionId|ApiKeyId|SessionId|AgentId|ConfigId|CreatedByUserId|UpdatedByUserId)\s*=\s*"[^"]*"', '$1 = Guid.NewGuid()'
                $modified = $true
            }

            $newLines += $line
        }

        if ($modified) {
            $content = $newLines -join "`n"
            Write-Host "Fixed ID assignments in $($file.Name)" -ForegroundColor Green
        }
    }

    # Fix Scopes array: Scopes = new[] { "read" } -> Scopes = "read"
    if ($content -match 'Scopes\s*=\s*new\[\]\s*{') {
        $content = $content -replace 'Scopes\s*=\s*new\[\]\s*{\s*"([^"]+)"\s*}', 'Scopes = "$1"'
        $modified = $true
        Write-Host "Fixed Scopes property in $($file.Name)" -ForegroundColor Green
    }

    # Save if modified
    if ($modified) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        Write-Host "Saved changes to $($file.Name)" -ForegroundColor Cyan
    }
}

Write-Host "`nPhase 1 complete! Re-run 'dotnet build' to check remaining errors." -ForegroundColor Yellow
