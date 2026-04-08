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
