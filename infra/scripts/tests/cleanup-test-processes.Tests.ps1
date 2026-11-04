# infra/scripts/tests/cleanup-test-processes.Tests.ps1
# Pester 5 tests for the pure rules in tools/cleanup/TestProcessCleanup.psm1 (#4310).
# Run: pwsh -c "Invoke-Pester infra/scripts/tests/cleanup-test-processes.Tests.ps1 -Output Detailed"
#
# Why they live here and not next to the script: the "Infra Scripts (bats + Pester)" job of
# dev-fast.yml runs every *.Tests.ps1 under infra/scripts/tests/. A suite anywhere else would be
# a suite no workflow executes.
#
# What they pin: cleanup-test-processes.ps1 used to kill every testhost on the machine. With
# several checkouts (git worktrees) working in parallel, one checkout's cleanup aborted the test
# runs of all the others. The rule is now "only the processes of this checkout".

BeforeAll {
    # Nested Join-Path / Split-Path on purpose: the three-argument forms need PowerShell 7, and the
    # module must also load in Windows PowerShell 5.1, which is what the pre-push hook runs.
    $repoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
    $modulePath = Join-Path (Join-Path (Join-Path $repoRoot 'tools') 'cleanup') 'TestProcessCleanup.psm1'
    Import-Module $modulePath -Force

    $script:Mine = 'D:\Repositories\wt-4244'
    $script:MyTestHost = '"D:\Repositories\wt-4244\apps\api\tests\Api.Tests\bin\Debug\net9.0\testhost.exe" --runtimeconfig "D:\Repositories\wt-4244\apps\api\tests\Api.Tests\bin\Debug\net9.0\Api.Tests.runtimeconfig.json"'
    $script:TheirTestHost = '"D:\Repositories\wt-4280\apps\api\tests\Api.Tests\bin\Debug\net9.0\testhost.exe" --runtimeconfig "D:\Repositories\wt-4280\apps\api\tests\Api.Tests\bin\Debug\net9.0\Api.Tests.runtimeconfig.json"'
    $script:MyVitest = '"C:\Program Files\nodejs\node.exe" D:\Repositories\wt-4244\apps\web\node_modules\vitest\dist\workers\forks.js'
    $script:TheirVitest = '"C:\Program Files\nodejs\node.exe" D:\Repositories\wt-4280\apps\web\node_modules\vitest\dist\workers\forks.js'
}

Describe 'TestProcessCleanup module loads' {
    It 'imports without errors' {
        Get-Module -Name 'TestProcessCleanup' | Should -Not -BeNullOrEmpty
    }
}

Describe 'Test-CommandLineBelongsToRepo' {
    It 'is true for a testhost that runs from this checkout' {
        Test-CommandLineBelongsToRepo -CommandLine $script:MyTestHost -RepoRoot $script:Mine | Should -BeTrue
    }
    It 'is false for a testhost that runs from another checkout' {
        Test-CommandLineBelongsToRepo -CommandLine $script:TheirTestHost -RepoRoot $script:Mine | Should -BeFalse
    }
    It 'is false for a sibling checkout whose name starts with this one (wt-4244 vs wt-4244b)' {
        $sibling = '"D:\Repositories\wt-4244b\apps\api\tests\Api.Tests\bin\Debug\net9.0\testhost.exe"'
        Test-CommandLineBelongsToRepo -CommandLine $sibling -RepoRoot $script:Mine | Should -BeFalse
    }
    It 'ignores slash direction, case and a trailing slash in the root' {
        Test-CommandLineBelongsToRepo -CommandLine $script:MyTestHost -RepoRoot 'd:/repositories/WT-4244/' | Should -BeTrue
    }
    It 'accepts the root the way Git Bash may print it (/d/Repositories/...)' {
        Test-CommandLineBelongsToRepo -CommandLine $script:MyTestHost -RepoRoot '/d/Repositories/wt-4244' | Should -BeTrue
    }
    It 'is false for a worktree nested inside this checkout: it is another checkout' {
        $main = 'D:\Repositories\meepleai-monorepo-main'
        $nested = 'D:\Repositories\meepleai-monorepo-main\.claude\worktrees\feature+claims'
        $nestedTestHost = '"D:\Repositories\meepleai-monorepo-main\.claude\worktrees\feature+claims\apps\api\tests\Api.Tests\bin\Debug\net9.0\testhost.exe"'
        Test-CommandLineBelongsToRepo -CommandLine $nestedTestHost -RepoRoot $main -OtherRoots @($nested) | Should -BeFalse
    }
    It 'is still true for the outer checkout own process when a nested worktree exists' {
        $main = 'D:\Repositories\meepleai-monorepo-main'
        $nested = 'D:\Repositories\meepleai-monorepo-main\.claude\worktrees\feature+claims'
        $mainTestHost = '"D:\Repositories\meepleai-monorepo-main\apps\api\tests\Api.Tests\bin\Debug\net9.0\testhost.exe"'
        Test-CommandLineBelongsToRepo -CommandLine $mainTestHost -RepoRoot $main -OtherRoots @($nested) | Should -BeTrue
    }
    It 'is false when the command line does not name the checkout at all' {
        Test-CommandLineBelongsToRepo -CommandLine '"C:\Program Files\dotnet\dotnet.exe" test --filter Category=Unit' -RepoRoot $script:Mine | Should -BeFalse
    }
    It 'is false for an empty command line' {
        Test-CommandLineBelongsToRepo -CommandLine '' -RepoRoot $script:Mine | Should -BeFalse
    }
}

Describe 'Get-TestProcessDecision' {
    Context 'testhost, the way the pre-push hook runs the cleanup (-TestHostOnly)' {
        It 'leaves the testhost of another checkout alone (#4310)' {
            $decision = Get-TestProcessDecision -ProcessName 'testhost' -CommandLine $script:TheirTestHost -RuntimeMinutes 5 -MemoryMB 550 -RepoRoot $script:Mine -TestHostOnly
            $decision.Action | Should -Be 'OtherCheckout'
        }
        It 'leaves it alone even when it has been running for a long time' {
            $decision = Get-TestProcessDecision -ProcessName 'testhost' -CommandLine $script:TheirTestHost -RuntimeMinutes 45 -MemoryMB 550 -RepoRoot $script:Mine -TestHostOnly
            $decision.Action | Should -Be 'OtherCheckout'
        }
        It 'kills the testhost of this checkout once it is older than a minute (the DLL-lock case)' {
            $decision = Get-TestProcessDecision -ProcessName 'testhost' -CommandLine $script:MyTestHost -RuntimeMinutes 5 -MemoryMB 550 -RepoRoot $script:Mine -TestHostOnly
            $decision.Action | Should -Be 'Kill'
            $decision.Reason | Should -Not -BeNullOrEmpty
        }
        It 'does not kill a testhost of this checkout that just started' {
            $decision = Get-TestProcessDecision -ProcessName 'testhost' -CommandLine $script:MyTestHost -RuntimeMinutes 0.5 -MemoryMB 550 -RepoRoot $script:Mine -TestHostOnly
            $decision.Action | Should -Be 'Active'
        }
        It 'kills every testhost when -AllCheckouts is given: the old behaviour, now explicit' {
            $decision = Get-TestProcessDecision -ProcessName 'testhost' -CommandLine $script:TheirTestHost -RuntimeMinutes 5 -MemoryMB 550 -RepoRoot $script:Mine -TestHostOnly -AllCheckouts
            $decision.Action | Should -Be 'Kill'
        }
    }

    Context 'full cleanup (no -TestHostOnly), the way posttest:coverage runs it' {
        It 'leaves a vitest worker of another checkout alone even above the memory threshold' {
            $decision = Get-TestProcessDecision -ProcessName 'node' -CommandLine $script:TheirVitest -RuntimeMinutes 3 -MemoryMB 900 -RepoRoot $script:Mine
            $decision.Action | Should -Be 'OtherCheckout'
        }
        It 'kills a vitest worker of this checkout above the memory threshold' {
            $decision = Get-TestProcessDecision -ProcessName 'node' -CommandLine $script:MyVitest -RuntimeMinutes 3 -MemoryMB 900 -RepoRoot $script:Mine
            $decision.Action | Should -Be 'Kill'
        }
        It 'leaves a vitest worker of this checkout below the memory threshold running' {
            $decision = Get-TestProcessDecision -ProcessName 'node' -CommandLine $script:MyVitest -RuntimeMinutes 3 -MemoryMB 40 -RepoRoot $script:Mine
            $decision.Action | Should -Be 'Active'
        }
        It 'kills a testhost of this checkout older than ten minutes' {
            $decision = Get-TestProcessDecision -ProcessName 'testhost' -CommandLine $script:MyTestHost -RuntimeMinutes 12 -MemoryMB 40 -RepoRoot $script:Mine
            $decision.Action | Should -Be 'Kill'
        }
        It 'honours a custom memory threshold' {
            $decision = Get-TestProcessDecision -ProcessName 'node' -CommandLine $script:MyVitest -RuntimeMinutes 3 -MemoryMB 150 -RepoRoot $script:Mine -MemoryThresholdMB 200
            $decision.Action | Should -Be 'Active'
        }
    }

    Context 'processes that are never touched' {
        It 'protects editors and build tools, whatever checkout they come from' {
            $ide = '"C:\Users\dev\AppData\Local\Programs\Microsoft VS Code\Code.exe" D:\Repositories\wt-4244 --extensionTestsPath'
            $decision = Get-TestProcessDecision -ProcessName 'node' -CommandLine $ide -RuntimeMinutes 60 -MemoryMB 900 -RepoRoot $script:Mine
            $decision.Action | Should -Be 'Protected'
        }
        It 'protects a process whose command line cannot be read' {
            $decision = Get-TestProcessDecision -ProcessName 'testhost' -CommandLine $null -RuntimeMinutes 60 -MemoryMB 900 -RepoRoot $script:Mine -TestHostOnly
            $decision.Action | Should -Be 'Protected'
        }
        It 'ignores a process of this checkout that is not a test process' {
            $devServer = '"C:\Program Files\nodejs\node.exe" D:\Repositories\wt-4244\apps\web\node_modules\next\dist\bin\next dev'
            $decision = Get-TestProcessDecision -ProcessName 'node' -CommandLine $devServer -RuntimeMinutes 60 -MemoryMB 900 -RepoRoot $script:Mine
            $decision.Action | Should -Be 'NotATestProcess'
        }
    }
}

Describe 'ConvertFrom-WorktreeList' {
    It 'returns the checkout roots from "git worktree list --porcelain"' {
        $lines = @(
            'worktree D:/Repositories/meepleai-monorepo-main',
            'HEAD 74b89f738c0ffee',
            'branch refs/heads/main-dev',
            '',
            'worktree D:/Repositories/wt-4244',
            'HEAD 200968890c0ffee',
            'branch refs/heads/feature/issue-4244',
            '',
            'worktree D:/Repositories/wt-4168g',
            'HEAD afd0cdbb3c0ffee',
            'detached'
        )
        $roots = @(ConvertFrom-WorktreeList -Lines $lines)
        $roots | Should -Be @('D:/Repositories/meepleai-monorepo-main', 'D:/Repositories/wt-4244', 'D:/Repositories/wt-4168g')
    }
    It 'returns nothing for no output' {
        @(ConvertFrom-WorktreeList -Lines @()).Count | Should -Be 0
    }
}
