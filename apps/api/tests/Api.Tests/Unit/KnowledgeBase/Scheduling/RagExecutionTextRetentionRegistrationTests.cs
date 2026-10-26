using Api.BoundedContexts.KnowledgeBase.Application.Configuration;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.DependencyInjection;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;
using Xunit;

namespace Api.Tests.Unit.KnowledgeBase.Scheduling;

/// <summary>
/// #4634: the retention of <c>rag_executions</c> is a Quartz job, and the integration hosts remove the hosted services
/// that would run it: no integration test would notice that the registration is missing, or that the configuration
/// key reaches nothing. A retention job that never runs is the defect itself, left in place.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4634")]
public sealed class RagExecutionTextRetentionRegistrationTests
{
    private static readonly JobKey JobKey = new("rag-execution-text-retention-job", "knowledge-base");

    private static ServiceProvider BuildProvider(IDictionary<string, string?>? settings = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings ?? new Dictionary<string, string?>()).Build();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddKnowledgeBaseServices(configuration);

        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddKnowledgeBaseServices_RegistersTheJob_WithADailyTrigger()
    {
        using var provider = BuildProvider();

        var quartz = provider.GetRequiredService<IOptions<QuartzOptions>>().Value;

        quartz.JobDetails.Should().Contain(j => j.Key.Equals(JobKey) && j.JobType == typeof(RagExecutionTextRetentionJob));
        quartz.Triggers.Should().Contain(t =>
            t.JobKey.Equals(JobKey) &&
            t is ICronTrigger &&
            ((ICronTrigger)t).CronExpressionString == "0 0 5 * * ?");
    }

    [Fact]
    public void TheOptions_WithNothingConfigured_AreTheDefaults()
    {
        using var provider = BuildProvider();

        var options = provider.GetRequiredService<IOptions<RagExecutionTextRetentionOptions>>().Value;

        options.RetentionDays.Should().Be(RagExecutionTextRetentionOptions.DefaultRetentionDays);
        options.BatchSize.Should().Be(RagExecutionTextRetentionOptions.DefaultBatchSize);
    }

    [Fact]
    public void TheConfigurationSection_ReachesTheOptions()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [$"{RagExecutionTextRetentionOptions.SectionKey}:RetentionDays"] = "30",
            [$"{RagExecutionTextRetentionOptions.SectionKey}:BatchSize"] = "250",
        });

        var options = provider.GetRequiredService<IOptions<RagExecutionTextRetentionOptions>>().Value;

        options.RetentionDays.Should().Be(30);
        options.BatchSize.Should().Be(250);
    }

    [Fact]
    public void TheDefaultRetention_IsDeclaredInAppsettings_AndMatchesTheCode()
    {
        // The value the code uses when the key is missing and the one written in appsettings.json must not diverge:
        // whoever reads the file has to see the expiry that really runs.
        var appsettings = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        File.Exists(appsettings).Should().BeTrue("the test host copies appsettings.json next to the assembly");

        var configuration = new ConfigurationBuilder().AddJsonFile(appsettings, optional: false).Build();

        configuration.GetSection(RagExecutionTextRetentionOptions.SectionKey).GetValue<int?>("RetentionDays")
            .Should().Be(RagExecutionTextRetentionOptions.DefaultRetentionDays);
    }
}
