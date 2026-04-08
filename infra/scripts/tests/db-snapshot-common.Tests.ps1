# infra/scripts/tests/db-snapshot-common.Tests.ps1
# Pester 5 tests for pure functions in db-snapshot-common.psm1
# Run: pwsh -c "Invoke-Pester infra/scripts/tests/db-snapshot-common.Tests.ps1 -Output Detailed"

BeforeAll {
    $modulePath = Join-Path $PSScriptRoot '..' 'db-snapshot-common.psm1'
    Import-Module $modulePath -Force
}

Describe 'db-snapshot-common module loads' {
    It 'imports without errors' {
        Get-Module -Name 'db-snapshot-common' | Should -Not -BeNullOrEmpty
    }
}

Describe 'Test-LocalhostHost' {
    It 'returns true for "localhost"' {
        Test-LocalhostHost -PgHost 'localhost' | Should -BeTrue
    }
    It 'returns true for "127.0.0.1"' {
        Test-LocalhostHost -PgHost '127.0.0.1' | Should -BeTrue
    }
    It 'returns false for "staging.meepleai.app"' {
        Test-LocalhostHost -PgHost 'staging.meepleai.app' | Should -BeFalse
    }
    It 'returns false for "postgres" (docker internal name)' {
        Test-LocalhostHost -PgHost 'postgres' | Should -BeFalse
    }
    It 'returns false for empty string' {
        Test-LocalhostHost -PgHost '' | Should -BeFalse
    }
    It 'is case-insensitive for "LOCALHOST"' {
        Test-LocalhostHost -PgHost 'LOCALHOST' | Should -BeTrue
    }
}
