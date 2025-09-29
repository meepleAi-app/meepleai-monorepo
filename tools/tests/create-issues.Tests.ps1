BeforeAll {
  . (Join-Path $PSScriptRoot '..' 'create-issues.ps1')
}

Describe 'Get-AllPages' {
  BeforeEach {
    Set-Variable -Name CapturedUrls -Scope Script -Value @()
    Mock -CommandName Invoke-GitHubApi -MockWith {
      param([string]$Url)
      $script:CapturedUrls += $Url
      '[]'
    } -Verifiable
  }

  It 'prefixes per_page query when the base URL lacks parameters' {
    Get-AllPages -UrlBase 'repos/example/labels' | Out-Null
    $script:CapturedUrls | Should -Not -BeNullOrEmpty
    $script:CapturedUrls[0] | Should -Match '\?per_page=100&page=1$'
    $script:CapturedUrls[0].Split('?')[1] | Should -Match '^per_page='
    Assert-VerifiableMocks
  }
}
