using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Infrastructure.Scheduling;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.UserNotifications.Infrastructure.Scheduling;

/// <summary>
/// #5113: the share request alerts went to <c>Role == "admin"</c> only, while the review routes admit SuperAdmin and
/// Admin (<c>AdminOnlyPolicy</c>). A superadmin could review the requests and was never told about them; where every
/// administrator is a superadmin, nobody was. The recipients are the roles that can review: admin and superadmin.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserNotifications")]
[Trait("Issue", "5113")]
public sealed class ShareRequestAlertRecipientsJobsTests : IAsyncDisposable
{
    private readonly MeepleAiDbContext _db = TestDbContextFactory.CreateInMemoryDbContext();
    private readonly Dictionary<string, Guid> _users = new(StringComparer.Ordinal);

    public async ValueTask DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task TheDigest_IsMailedToEveryoneWhoCanReview()
    {
        await SeedOneUserPerRoleAsync();
        var shareRequests = new Mock<IShareRequestRepository>();
        shareRequests
            .Setup(r => r.GetPendingStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PendingShareRequestStats { TotalPending = 1, OldestPendingAge = TimeSpan.FromDays(1) });
        var email = new Mock<IEmailService>();

        var job = new AdminShareRequestDigestJob(
            shareRequests.Object,
            _db,
            email.Object,
            PublicBaseUrlTestSupport.ProviderFor(new Dictionary<string, string?> { ["App:BaseUrl"] = "https://meepleai.test" }),
            NullLogger<AdminShareRequestDigestJob>.Instance);
        await job.Execute(JobContext());

        var recipients = email.Invocations
            .Where(i => i.Method.Name == nameof(IEmailService.SendAdminShareRequestDigestEmailAsync))
            .Select(i => (string)i.Arguments[0]);
        recipients.Should().BeEquivalentTo(EmailOf("admin"), EmailOf("superadmin"));
    }

    [Fact]
    public async Task TheStaleWarning_NotifiesEveryoneWhoCanReview()
    {
        await SeedOneUserPerRoleAsync();
        var shareRequests = new Mock<IShareRequestRepository>();
        shareRequests
            .Setup(r => r.GetPendingOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ShareRequest.Create(Guid.NewGuid(), Guid.NewGuid(), ContributionType.NewGame) });
        var notifications = new Mock<INotificationRepository>();
        var sent = new List<Notification>();
        notifications
            .Setup(r => r.AddBatchAndCommitAsync(It.IsAny<IEnumerable<Notification>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Notification>, CancellationToken>((batch, _) => sent.AddRange(batch))
            .ReturnsAsync(2);

        var job = new StaleShareRequestWarningJob(
            shareRequests.Object,
            notifications.Object,
            _db,
            new ConfigurationBuilder().Build(),
            NullLogger<StaleShareRequestWarningJob>.Instance);
        await job.Execute(JobContext());

        sent.Select(n => n.UserId).Should().BeEquivalentTo([_users["admin"], _users["superadmin"]]);
    }

    private async Task SeedOneUserPerRoleAsync()
    {
        // As the database stores them: lowercase (Role value object, TestSessionHelper).
        foreach (var role in new[] { "user", "editor", "creator", "admin", "superadmin" })
        {
            var id = Guid.NewGuid();
            _users[role] = id;
            _db.Users.Add(new UserEntity
            {
                Id = id,
                Email = EmailOf(role),
                DisplayName = role,
                PasswordHash = "hash",
                Role = role,
                CreatedAt = DateTime.UtcNow,
            });
        }

        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static string EmailOf(string role) => $"{role}-5113@example.test";

    private static IJobExecutionContext JobContext()
    {
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(c => c.FireTimeUtc).Returns(DateTimeOffset.UtcNow);
        context.SetupGet(c => c.CancellationToken).Returns(TestContext.Current.CancellationToken);
        context.SetupProperty(c => c.Result);
        return context.Object;
    }
}
