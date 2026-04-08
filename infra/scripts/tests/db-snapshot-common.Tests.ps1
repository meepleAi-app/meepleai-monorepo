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

Describe 'ConvertFrom-SecretFile' {
    BeforeEach {
        $script:tmpFile = New-TemporaryFile
    }
    AfterEach {
        if (Test-Path $script:tmpFile) { Remove-Item $script:tmpFile -Force }
    }

    It 'parses simple KEY=VALUE pairs' {
        @(
            'POSTGRES_USER=meepleai',
            'POSTGRES_PASSWORD=secret123',
            'POSTGRES_DB=meepleai_db'
        ) | Set-Content $script:tmpFile
        $result = ConvertFrom-SecretFile -Path $script:tmpFile
        $result['POSTGRES_USER'] | Should -Be 'meepleai'
        $result['POSTGRES_PASSWORD'] | Should -Be 'secret123'
        $result['POSTGRES_DB'] | Should -Be 'meepleai_db'
    }

    It 'ignores blank lines and comments starting with #' {
        @(
            '# This is a comment',
            '',
            'KEY=value',
            '   # indented comment',
            ''
        ) | Set-Content $script:tmpFile
        $result = ConvertFrom-SecretFile -Path $script:tmpFile
        $result.Count | Should -Be 1
        $result['KEY'] | Should -Be 'value'
    }

    It 'handles values containing equals signs' {
        'CONN=Host=localhost;Port=5432' | Set-Content $script:tmpFile
        $result = ConvertFrom-SecretFile -Path $script:tmpFile
        $result['CONN'] | Should -Be 'Host=localhost;Port=5432'
    }

    It 'trims whitespace around keys and values' {
        '  KEY  =  value  ' | Set-Content $script:tmpFile
        $result = ConvertFrom-SecretFile -Path $script:tmpFile
        $result['KEY'] | Should -Be 'value'
    }

    It 'throws if the file does not exist' {
        { ConvertFrom-SecretFile -Path '/nonexistent/file.secret' } | Should -Throw -ErrorId '*'
    }

    It 'returns an empty hashtable for an empty file' {
        '' | Set-Content $script:tmpFile
        $result = ConvertFrom-SecretFile -Path $script:tmpFile
        $result.Count | Should -Be 0
    }
}
