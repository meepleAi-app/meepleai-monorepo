# Apply all FluentAssertions fix patterns systematically

# Pattern 1: .Should().Contain("text", StringComparison.OrdinalIgnoreCase) 
(Get-Content Services/RuleCommentServiceTests.cs) -replace 'ex\.Message\.Should\(\)\.Contain\("([^"]+)", StringComparison\.OrdinalIgnoreCase\)', 'ex.Which.Message.Should().Contain("$1", StringComparison.OrdinalIgnoreCase)' | Set-Content Services/RuleCommentServiceTests.cs

# Pattern 2: c => c.Id == comment.Id.Should().Contain(result)
(Get-Content Services/RuleCommentServiceTests.cs) -replace 'c => c\.Id == ([^.]+)\.Id\.Should\(\)\.Contain\(result\)', 'result.Should().Contain(c => c.Id == $1.Id)' | Set-Content Services/RuleCommentServiceTests.cs

# Pattern 3: .Should().Contain("text", StringComparison.OrdinalIgnoreCase) in UserManagementServiceTests
(Get-Content Services/UserManagementServiceTests.cs) -replace '\.Email\.Should\(\)\.Contain\("([^"]+)", StringComparison\.OrdinalIgnoreCase\)', '.Email.Should().Contain("$1", StringComparison.OrdinalIgnoreCase)' | Set-Content Services/UserManagementServiceTests.cs
(Get-Content Services/UserManagementServiceTests.cs) -replace '\.DisplayName\.Should\(\)\.Contain\("([^"]+)", StringComparison\.OrdinalIgnoreCase\)', '.DisplayName.Should().Contain("$1", StringComparison.OrdinalIgnoreCase)' | Set-Content Services/UserManagementServiceTests.cs

# Pattern 4: stateUpdate!.message.Should().Contain("cache", StringComparison.OrdinalIgnoreCase)
(Get-Content StreamingQaServiceTests.cs) -replace '\.message\.Should\(\)\.Contain\("([^"]+)", StringComparison\.OrdinalIgnoreCase\)', '.message.Should().Contain("$1", StringComparison.OrdinalIgnoreCase)' | Set-Content StreamingQaServiceTests.cs

# Pattern 5: RecommendationReason.Should().Contain("text", StringComparison.OrdinalIgnoreCase)
(Get-Content Services/PromptEvaluationServiceTests.cs) -replace '\.RecommendationReason\.Should\(\)\.Contain\("([^"]+)", StringComparison\.OrdinalIgnoreCase\)', '.RecommendationReason.Should().Contain("$1", StringComparison.OrdinalIgnoreCase)' | Set-Content Services/PromptEvaluationServiceTests.cs

Write-Host "Pattern fixes applied successfully"
