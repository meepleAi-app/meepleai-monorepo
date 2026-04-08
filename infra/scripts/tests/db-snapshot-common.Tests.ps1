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

Describe 'Get-PostgresConfig' {
    BeforeEach {
        $script:tmpFile = New-TemporaryFile
    }
    AfterEach {
        if (Test-Path $script:tmpFile) { Remove-Item $script:tmpFile -Force }
    }

    It 'returns config with defaults when only credentials are present' {
        @(
            'POSTGRES_USER=meepleai',
            'POSTGRES_PASSWORD=secret'
        ) | Set-Content $script:tmpFile
        $cfg = Get-PostgresConfig -SecretPath $script:tmpFile
        $cfg.Host | Should -Be 'localhost'
        $cfg.Port | Should -Be 5432
        $cfg.Db | Should -Be 'meepleai_db'
        $cfg.User | Should -Be 'meepleai'
        $cfg.Password | Should -Be 'secret'
    }

    It 'overrides defaults with explicit secret values' {
        @(
            'POSTGRES_HOST=otherhost',
            'POSTGRES_PORT=6543',
            'POSTGRES_DB=other_db',
            'POSTGRES_USER=user',
            'POSTGRES_PASSWORD=pw'
        ) | Set-Content $script:tmpFile
        $cfg = Get-PostgresConfig -SecretPath $script:tmpFile
        $cfg.Host | Should -Be 'otherhost'
        $cfg.Port | Should -Be 6543
        $cfg.Db | Should -Be 'other_db'
        $cfg.User | Should -Be 'user'
        $cfg.Password | Should -Be 'pw'
    }

    It 'throws when POSTGRES_USER is missing' {
        @(
            'POSTGRES_PASSWORD=secret'
        ) | Set-Content $script:tmpFile
        { Get-PostgresConfig -SecretPath $script:tmpFile } | Should -Throw '*POSTGRES_USER*'
    }

    It 'throws when POSTGRES_PASSWORD is missing' {
        @(
            'POSTGRES_USER=meepleai'
        ) | Set-Content $script:tmpFile
        { Get-PostgresConfig -SecretPath $script:tmpFile } | Should -Throw '*POSTGRES_PASSWORD*'
    }
}

Describe 'Assert-LocalhostOnly' {
    It 'does not throw for localhost config' {
        $cfg = [pscustomobject]@{ Host = 'localhost'; Port = 5432; Db = 'meepleai_db'; User = 'u'; Password = 'p' }
        { Assert-LocalhostOnly -Config $cfg } | Should -Not -Throw
    }
    It 'does not throw for 127.0.0.1 config' {
        $cfg = [pscustomobject]@{ Host = '127.0.0.1'; Port = 5432; Db = 'meepleai_db'; User = 'u'; Password = 'p' }
        { Assert-LocalhostOnly -Config $cfg } | Should -Not -Throw
    }
    It 'throws for staging.meepleai.app config' {
        $cfg = [pscustomobject]@{ Host = 'staging.meepleai.app'; Port = 5432; Db = 'meepleai_db'; User = 'u'; Password = 'p' }
        { Assert-LocalhostOnly -Config $cfg } | Should -Throw '*staging.meepleai.app*'
    }
    It 'throws and includes the host name in the error' {
        $cfg = [pscustomobject]@{ Host = 'prod.example.com'; Port = 5432; Db = 'meepleai_db'; User = 'u'; Password = 'p' }
        { Assert-LocalhostOnly -Config $cfg } | Should -Throw '*prod.example.com*'
    }
}

Describe 'Get-RequiredDiskSpaceBytes' {
    It 'returns DB size + 64 MB overhead + 10% safety margin when no volume' {
        $required = Get-RequiredDiskSpaceBytes -DbSizeBytes 100000000 -VolumeSizeBytes 0
        $expected = [math]::Ceiling((100000000 + 67108864) * 1.1)
        $required | Should -Be $expected
    }
    It 'includes volume size when provided' {
        $required = Get-RequiredDiskSpaceBytes -DbSizeBytes 100000000 -VolumeSizeBytes 50000000
        $expected = [math]::Ceiling((100000000 + 50000000 + 67108864) * 1.1)
        $required | Should -Be $expected
    }
    It 'returns at least the overhead + safety margin for zero db' {
        $required = Get-RequiredDiskSpaceBytes -DbSizeBytes 0 -VolumeSizeBytes 0
        $expected = [math]::Ceiling((0 + 67108864) * 1.1)
        $required | Should -Be $expected
    }
    It 'throws for negative DB size' {
        { Get-RequiredDiskSpaceBytes -DbSizeBytes -1 -VolumeSizeBytes 0 } | Should -Throw '*non-negative*'
    }
}
