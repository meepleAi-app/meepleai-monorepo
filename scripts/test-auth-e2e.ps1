#!/usr/bin/env pwsh
<#
.SYNOPSIS
    E2E test script for MeepleAI authentication flow
.DESCRIPTION
    Tests the complete authentication flow including:
    - User registration with different roles
    - User login
    - Session validation
    - Role visibility
    - Logout functionality
.EXAMPLE
    .\scripts\test-auth-e2e.ps1
#>

param(
    [string]$ApiBase = "http://localhost:8080"
)

$ErrorActionPreference = "Stop"

# Color output functions
function Write-Success { param([string]$Message) Write-Host "✓ $Message" -ForegroundColor Green }
function Write-Error { param([string]$Message) Write-Host "✗ $Message" -ForegroundColor Red }
function Write-Info { param([string]$Message) Write-Host "ℹ $Message" -ForegroundColor Cyan }
function Write-Test { param([string]$Message) Write-Host "→ $Message" -ForegroundColor Yellow }

# Generate unique test data
$timestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$testUsers = @(
    @{
        Email = "admin-$timestamp@e2etest.local"
        Password = "TestPass123!"
        DisplayName = "Test Admin"
        Role = "Admin"
    },
    @{
        Email = "editor-$timestamp@e2etest.local"
        Password = "TestPass456!"
        DisplayName = "Test Editor"
        Role = "Editor"
    },
    @{
        Email = "user-$timestamp@e2etest.local"
        Password = "TestPass789!"
        DisplayName = "Test User"
        Role = "User"
    }
)

# Store session cookies
$sessionCookies = @{}

Write-Host "`n========================================" -ForegroundColor Magenta
Write-Host "  MeepleAI E2E Authentication Tests" -ForegroundColor Magenta
Write-Host "========================================`n" -ForegroundColor Magenta

Write-Info "API Base: $ApiBase"
Write-Info "Timestamp: $timestamp`n"

# Test 1: Health check
Write-Test "Test 1: API Health Check"
try {
    $response = Invoke-RestMethod -Uri "$ApiBase/" -Method Get
    if ($response.ok -eq $true) {
        Write-Success "API is healthy"
    } else {
        Write-Error "API health check failed"
        exit 1
    }
} catch {
    Write-Error "Cannot connect to API at $ApiBase"
    Write-Error $_.Exception.Message
    exit 1
}

# Test 2: Register users with different roles
Write-Test "`nTest 2: User Registration (Admin, Editor, User)"
foreach ($user in $testUsers) {
    try {
        $body = @{
            email = $user.Email
            password = $user.Password
            displayName = $user.DisplayName
            role = $user.Role
        } | ConvertTo-Json

        $response = Invoke-WebRequest -Uri "$ApiBase/auth/register" -Method Post `
            -ContentType "application/json" -Body $body -SessionVariable session

        $result = $response.Content | ConvertFrom-Json

        # Store session cookie
        $sessionCookies[$user.Email] = $session

        if ($result.user.email -eq $user.Email -and $result.user.role -eq $user.Role) {
            Write-Success "Registered $($user.Role): $($user.Email)"
            Write-Info "  → User ID: $($result.user.id)"
            Write-Info "  → Display Name: $($result.user.displayName)"
            Write-Info "  → Role: $($result.user.role)"
            Write-Info "  → Expires At: $($result.expiresAt)"
        } else {
            Write-Error "Registration response validation failed for $($user.Email)"
            exit 1
        }
    } catch {
        Write-Error "Failed to register $($user.Email)"
        Write-Error $_.Exception.Message
        exit 1
    }
}

# Test 3: Logout all users
Write-Test "`nTest 3: Logout"
foreach ($user in $testUsers) {
    try {
        $session = $sessionCookies[$user.Email]
        $response = Invoke-RestMethod -Uri "$ApiBase/auth/logout" -Method Post `
            -WebSession $session

        if ($response.ok -eq $true) {
            Write-Success "Logged out: $($user.Email)"
        } else {
            Write-Error "Logout failed for $($user.Email)"
            exit 1
        }
    } catch {
        Write-Error "Failed to logout $($user.Email)"
        Write-Error $_.Exception.Message
        exit 1
    }
}

# Test 4: Login with each user
Write-Test "`nTest 4: User Login"
foreach ($user in $testUsers) {
    try {
        $body = @{
            email = $user.Email
            password = $user.Password
        } | ConvertTo-Json

        $response = Invoke-WebRequest -Uri "$ApiBase/auth/login" -Method Post `
            -ContentType "application/json" -Body $body -SessionVariable newSession

        $result = $response.Content | ConvertFrom-Json

        # Update session cookie
        $sessionCookies[$user.Email] = $newSession

        if ($result.user.email -eq $user.Email -and $result.user.role -eq $user.Role) {
            Write-Success "Logged in $($user.Role): $($user.Email)"
            Write-Info "  → Role visible: $($result.user.role)"
        } else {
            Write-Error "Login response validation failed for $($user.Email)"
            exit 1
        }
    } catch {
        Write-Error "Failed to login $($user.Email)"
        Write-Error $_.Exception.Message
        exit 1
    }
}

# Test 5: Validate session (GET /auth/me)
Write-Test "`nTest 5: Session Validation & Role Visibility"
foreach ($user in $testUsers) {
    try {
        $session = $sessionCookies[$user.Email]
        $response = Invoke-RestMethod -Uri "$ApiBase/auth/me" -Method Get `
            -WebSession $session

        if ($response.user.email -eq $user.Email -and $response.user.role -eq $user.Role) {
            Write-Success "Session valid for $($user.Email)"
            Write-Info "  → Role: $($response.user.role) ✓"
            Write-Info "  → Display Name: $($response.user.displayName)"
        } else {
            Write-Error "Session validation failed for $($user.Email)"
            exit 1
        }
    } catch {
        Write-Error "Failed to validate session for $($user.Email)"
        Write-Error $_.Exception.Message
        exit 1
    }
}

# Test 6: Test login with wrong password
Write-Test "`nTest 6: Login with Wrong Password (should fail)"
try {
    $body = @{
        email = $testUsers[0].Email
        password = "WrongPassword123!"
    } | ConvertTo-Json

    $response = Invoke-WebRequest -Uri "$ApiBase/auth/login" -Method Post `
        -ContentType "application/json" -Body $body -ErrorAction Stop

    Write-Error "Login should have failed with wrong password"
    exit 1
} catch {
    if ($_.Exception.Response.StatusCode -eq 401) {
        Write-Success "Correctly rejected wrong password (401 Unauthorized)"
    } else {
        Write-Error "Unexpected error: $($_.Exception.Message)"
        exit 1
    }
}

# Test 7: Test accessing protected endpoint without authentication
Write-Test "`nTest 7: Access Protected Endpoint Without Auth (should fail)"
try {
    $body = @{
        gameId = "demo-chess"
        query = "How many players?"
    } | ConvertTo-Json

    $response = Invoke-WebRequest -Uri "$ApiBase/agents/qa" -Method Post `
        -ContentType "application/json" -Body $body -ErrorAction Stop

    Write-Error "Protected endpoint should require authentication"
    exit 1
} catch {
    if ($_.Exception.Response.StatusCode -eq 401) {
        Write-Success "Correctly rejected unauthenticated request (401 Unauthorized)"
    } else {
        Write-Error "Unexpected error: $($_.Exception.Message)"
        exit 1
    }
}

# Test 8: Test role-based authorization (Admin endpoint)
Write-Test "`nTest 8: Role-Based Authorization"
Write-Info "Testing /admin/seed endpoint (Admin only)"

# Test with User role (should fail)
try {
    $userSession = $sessionCookies[$testUsers[2].Email]
    $body = @{
        gameId = "test-game"
    } | ConvertTo-Json

    $response = Invoke-WebRequest -Uri "$ApiBase/admin/seed" -Method Post `
        -ContentType "application/json" -Body $body -WebSession $userSession -ErrorAction Stop

    Write-Error "User role should not access admin endpoint"
    exit 1
} catch {
    if ($_.Exception.Response.StatusCode -eq 403) {
        Write-Success "Correctly denied User role access to admin endpoint (403 Forbidden)"
    } else {
        Write-Error "Unexpected error for User role: $($_.Exception.Message)"
        exit 1
    }
}

# Test with Admin role (should succeed)
try {
    $adminSession = $sessionCookies[$testUsers[0].Email]
    $body = @{
        gameId = "test-game"
    } | ConvertTo-Json

    $response = Invoke-RestMethod -Uri "$ApiBase/admin/seed" -Method Post `
        -ContentType "application/json" -Body $body -WebSession $adminSession

    if ($response.ok -eq $true) {
        Write-Success "Admin role successfully accessed admin endpoint"
    } else {
        Write-Error "Admin endpoint returned unexpected response"
        exit 1
    }
} catch {
    Write-Error "Admin role failed to access admin endpoint: $($_.Exception.Message)"
    exit 1
}

# Test 9: Final logout test
Write-Test "`nTest 9: Final Logout & Session Invalidation"
foreach ($user in $testUsers) {
    try {
        $session = $sessionCookies[$user.Email]

        # Logout
        $response = Invoke-RestMethod -Uri "$ApiBase/auth/logout" -Method Post -WebSession $session
        Write-Success "Logged out: $($user.Email)"

        # Try to access /auth/me (should fail)
        try {
            $meResponse = Invoke-WebRequest -Uri "$ApiBase/auth/me" -Method Get `
                -WebSession $session -ErrorAction Stop
            Write-Error "Session should be invalidated after logout"
            exit 1
        } catch {
            if ($_.Exception.Response.StatusCode -eq 401) {
                Write-Success "Session correctly invalidated for $($user.Email)"
            } else {
                Write-Error "Unexpected error checking invalidated session"
                exit 1
            }
        }
    } catch {
        Write-Error "Failed logout test for $($user.Email)"
        Write-Error $_.Exception.Message
        exit 1
    }
}

# Summary
Write-Host "`n========================================" -ForegroundColor Magenta
Write-Host "  All E2E Authentication Tests Passed! ✓" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Magenta

Write-Info "Test Summary:"
Write-Info "  ✓ API Health Check"
Write-Info "  ✓ User Registration (Admin, Editor, User)"
Write-Info "  ✓ Logout Functionality"
Write-Info "  ✓ User Login"
Write-Info "  ✓ Session Validation & Role Visibility"
Write-Info "  ✓ Wrong Password Rejection"
Write-Info "  ✓ Unauthenticated Access Denial"
Write-Info "  ✓ Role-Based Authorization (Admin vs User)"
Write-Info "  ✓ Session Invalidation After Logout"

Write-Host "`nTest users created:" -ForegroundColor Cyan
foreach ($user in $testUsers) {
    Write-Host "  - $($user.Role): $($user.Email)" -ForegroundColor Gray
}