using Api.Services.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Resend;

namespace Api.Extensions;

/// <summary>
/// DI registration for the <see cref="IEmailSender"/> abstraction (Issue #1629).
/// </summary>
/// <remarks>
/// The choice between Resend and SMTP is driven by the <c>EMAIL_PROVIDER</c> env var
/// (also accepts <c>Email:Provider</c> in <c>appsettings</c>). When <c>resend</c> is
/// selected but no API key is present, the resolver falls back to SMTP and registers a
/// hosted service that logs a startup warning so the misconfiguration is visible.
/// </remarks>
internal static class EmailSenderServiceExtensions
{
    public static IServiceCollection AddEmailSender(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var provider = (configuration["EMAIL_PROVIDER"]
            ?? configuration["Email:Provider"]
            ?? "smtp").Trim();

        var resendApiKey = configuration["RESEND_API_KEY"]
            ?? configuration["Email:Resend:ApiKey"];

        var providerIsResend = string.Equals(provider, "resend", StringComparison.OrdinalIgnoreCase);
        var useResend = providerIsResend && !string.IsNullOrWhiteSpace(resendApiKey);

        if (useResend)
        {
            services.AddHttpClient<ResendClient>();
            services.Configure<ResendClientOptions>(o =>
            {
                o.ApiToken = resendApiKey!;
            });
            services.AddTransient<IResend, ResendClient>();
            services.AddScoped<IEmailSender, ResendEmailSender>();
        }
        else
        {
            // SMTP fallback (also used when EMAIL_PROVIDER is unset or RESEND_API_KEY missing).
            services.AddScoped<IEmailSender, SmtpEmailSender>();

            if (providerIsResend)
            {
                services.AddHostedService<ResendMisconfigurationWarningService>();
            }
        }

        return services;
    }
}

/// <summary>
/// Logs a single warning at startup when <c>EMAIL_PROVIDER=resend</c> was requested but
/// no <c>RESEND_API_KEY</c> was configured. Keeps the failure mode loud so silent
/// fallbacks to SMTP do not surprise operators.
/// </summary>
internal sealed class ResendMisconfigurationWarningService : IHostedService
{
    private readonly ILogger<ResendMisconfigurationWarningService> _logger;

    public ResendMisconfigurationWarningService(ILogger<ResendMisconfigurationWarningService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "EMAIL_PROVIDER=resend was requested but RESEND_API_KEY is not configured; falling back to SMTP transport.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
