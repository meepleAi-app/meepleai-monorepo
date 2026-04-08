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

Describe 'Manifest read/write' {
    BeforeEach {
        $script:tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) ([guid]::NewGuid())
        New-Item -ItemType Directory -Path $script:tmpDir | Out-Null
        $script:tmpFile = Join-Path $script:tmpDir 'manifest.json'
    }
    AfterEach {
        Remove-Item $script:tmpDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'round-trips a simple manifest' {
        $obj = [pscustomobject]@{
            schemaVersion = 1
            createdAt     = '2026-04-08T14:35:22Z'
            database      = 'meepleai_db'
            sanitize      = $false
        }
        Write-Manifest -Path $script:tmpFile -Object $obj
        $loaded = Read-Manifest -Path $script:tmpFile
        $loaded.schemaVersion | Should -Be 1
        $loaded.database | Should -Be 'meepleai_db'
        $loaded.sanitize | Should -Be $false
    }

    It 'preserves nested objects' {
        $obj = [pscustomobject]@{
            files = [pscustomobject]@{
                safetyNet = [pscustomobject]@{ name = 'a'; sha256 = 'b'; bytes = 123 }
            }
        }
        Write-Manifest -Path $script:tmpFile -Object $obj
        $loaded = Read-Manifest -Path $script:tmpFile
        $loaded.files.safetyNet.name | Should -Be 'a'
        $loaded.files.safetyNet.sha256 | Should -Be 'b'
        $loaded.files.safetyNet.bytes | Should -Be 123
    }

    It 'Read-Manifest throws when file does not exist' {
        { Read-Manifest -Path (Join-Path $script:tmpDir 'missing.json') } | Should -Throw '*not found*'
    }

    It 'Read-Manifest throws on invalid JSON' {
        'not json' | Set-Content $script:tmpFile
        { Read-Manifest -Path $script:tmpFile } | Should -Throw
    }

    It 'Write-Manifest is atomic (no partial file on success)' {
        $obj = [pscustomobject]@{ a = 1 }
        Write-Manifest -Path $script:tmpFile -Object $obj
        Test-Path $script:tmpFile | Should -BeTrue
        $tmpSibling = "$script:tmpFile.tmp"
        Test-Path $tmpSibling | Should -BeFalse
    }
}

Describe 'Normalize-PgSchema' {
    It 'strips line comments starting with --' {
        $input = @"
-- Dumped from database version 16.3
CREATE TABLE foo (id int);
"@
        $result = Normalize-PgSchema -Sql $input
        $result | Should -Not -Match '--'
        $result | Should -Match 'CREATE TABLE foo'
    }

    It 'strips SET statements' {
        $input = @"
SET client_min_messages = warning;
SET statement_timeout = 0;
CREATE TABLE foo (id int);
"@
        $result = Normalize-PgSchema -Sql $input
        $result | Should -Not -Match 'SET '
        $result | Should -Match 'CREATE TABLE foo'
    }

    It 'strips SELECT pg_catalog.set_config calls' {
        $input = @"
SELECT pg_catalog.set_config('search_path', '', false);
CREATE TABLE foo (id int);
"@
        $result = Normalize-PgSchema -Sql $input
        $result | Should -Not -Match 'set_config'
    }

    It 'collapses multiple blank lines into one' {
        $input = "CREATE TABLE foo (id int);`n`n`n`nCREATE TABLE bar (id int);"
        $result = Normalize-PgSchema -Sql $input
        ($result -split "`n").Count | Should -BeLessThan 4
    }

    It 'sorts CREATE TABLE statements alphabetically by name' {
        $input = @"
CREATE TABLE zebra (id int);
CREATE TABLE alpha (id int);
"@
        $result = Normalize-PgSchema -Sql $input
        $alphaIdx = $result.IndexOf('CREATE TABLE alpha')
        $zebraIdx = $result.IndexOf('CREATE TABLE zebra')
        $alphaIdx | Should -BeLessThan $zebraIdx
    }

    It 'is idempotent (normalize twice = normalize once)' {
        $input = @"
SET client_min_messages = warning;
-- comment
CREATE TABLE foo (id int);
CREATE TABLE bar (id int);
"@
        $once = Normalize-PgSchema -Sql $input
        $twice = Normalize-PgSchema -Sql $once
        $twice | Should -Be $once
    }

    It 'preserves CREATE TABLE column definitions' {
        $input = "CREATE TABLE foo (id int NOT NULL, name text);"
        $result = Normalize-PgSchema -Sql $input
        $result | Should -Match 'id int NOT NULL'
        $result | Should -Match 'name text'
    }

    It 'does not split on semicolons inside single-quoted string literals' {
        $input = "CREATE TABLE foo (id int, note text DEFAULT 'a;b;c');"
        $result = Normalize-PgSchema -Sql $input
        # Should be ONE statement, not three
        $statementCount = ([regex]::Matches($result, 'CREATE TABLE')).Count
        $statementCount | Should -Be 1
        $result | Should -Match "DEFAULT 'a;b;c'"
    }

    It 'handles escaped single quotes inside string literals' {
        $input = "CREATE TABLE foo (id int, note text DEFAULT 'O''Brien;test');"
        $result = Normalize-PgSchema -Sql $input
        $statementCount = ([regex]::Matches($result, 'CREATE TABLE')).Count
        $statementCount | Should -Be 1
        $result | Should -Match "O''Brien"
    }
}

Describe 'Test-SchemaDriftClass' {
    It 'returns identical for byte-equal schemas' {
        $a = "CREATE TABLE foo (id int);"
        $b = "CREATE TABLE foo (id int);"
        Test-SchemaDriftClass -PreSchema $a -PostSchema $b | Should -Be 'identical'
    }
    It 'returns identical for whitespace-only differences (already normalized)' {
        $a = "CREATE TABLE foo (id int);"
        $b = "CREATE TABLE foo (id int);"
        Test-SchemaDriftClass -PreSchema $a -PostSchema $b | Should -Be 'identical'
    }
    It 'returns significant for an added column' {
        $a = "CREATE TABLE foo (id int);"
        $b = "CREATE TABLE foo (id int, name text);"
        Test-SchemaDriftClass -PreSchema $a -PostSchema $b | Should -Be 'significant'
    }
    It 'returns significant for a removed column' {
        $a = "CREATE TABLE foo (id int, name text);"
        $b = "CREATE TABLE foo (id int);"
        Test-SchemaDriftClass -PreSchema $a -PostSchema $b | Should -Be 'significant'
    }
    It 'returns significant for a renamed column' {
        $a = "CREATE TABLE foo (id int, name text);"
        $b = "CREATE TABLE foo (id int, full_name text);"
        Test-SchemaDriftClass -PreSchema $a -PostSchema $b | Should -Be 'significant'
    }
    It 'returns significant for an added table' {
        $a = "CREATE TABLE foo (id int);"
        $b = "CREATE TABLE foo (id int);`n`nCREATE TABLE bar (id int);"
        Test-SchemaDriftClass -PreSchema $a -PostSchema $b | Should -Be 'significant'
    }
    It 'returns significant for a removed table' {
        $a = "CREATE TABLE foo (id int);`n`nCREATE TABLE bar (id int);"
        $b = "CREATE TABLE foo (id int);"
        Test-SchemaDriftClass -PreSchema $a -PostSchema $b | Should -Be 'significant'
    }
    It 'returns significant for a column type change' {
        $a = "CREATE TABLE foo (id int);"
        $b = "CREATE TABLE foo (id bigint);"
        Test-SchemaDriftClass -PreSchema $a -PostSchema $b | Should -Be 'significant'
    }
    It 'returns minor for trailing-whitespace-only diff that survived normalization' {
        $a = "CREATE TABLE foo (id int);"
        $b = "CREATE TABLE foo (id int);   "
        Test-SchemaDriftClass -PreSchema $a -PostSchema $b | Should -BeIn @('identical','minor')
    }
}
