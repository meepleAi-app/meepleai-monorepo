using Microsoft.Extensions.Options;
using System.Globalization;

namespace Api.Middleware;

/// <summary>
/// Middleware that adds HTTP security headers to all responses.
/// Implements OWASP security best practices to protect against common vulnerabilities:
/// - XSS (Cross-Site Scripting)
/// - Clickjacking
/// - MIME sniffing attacks
/// - Protocol downgrade attacks
/// - Referrer information leaks
/// - Unauthorized browser feature access
///
/// Issue #1447: Implements 7 critical security headers for OWASP compliance.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;
    private readonly IHostEnvironment _environment;
    private readonly SecurityHeadersOptions _options;

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        ILogger<SecurityHeadersMiddleware> logger,
        IHostEnvironment environment,
        IOptions<SecurityHeadersOptions> options)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers immediately before processing the request
        // This ensures headers are present in all scenarios (including tests)
        AddSecurityHeaders(context);

        await _next(context);
    }

    private void AddSecurityHeaders(HttpContext context)
    {
        try
        {
            var headers = context.Response.Headers;

            // 1. Content-Security-Policy (CSP) - Prevents XSS and injection attacks
            // Allows React frontend functionality while maintaining security
            if (_options.EnableCsp && !headers.ContainsKey("Content-Security-Policy"))
            {
                headers.Append("Content-Security-Policy", _options.CspPolicy);
            }

            // 2. Strict-Transport-Security (HSTS) - Forces HTTPS
            // Skip in development/localhost to avoid certificate issues
            if (_options.EnableHsts && ShouldEnableHsts(context))
            {
                headers.Append("Strict-Transport-Security", _options.HstsPolicy);
            }

            // 3. X-Frame-Options - Prevents clickjacking attacks
            if (_options.EnableXFrameOptions && !headers.ContainsKey("X-Frame-Options"))
            {
                headers.Append("X-Frame-Options", _options.XFrameOptionsPolicy);
            }

            // 4. X-Content-Type-Options - Prevents MIME sniffing
            if (_options.EnableXContentTypeOptions && !headers.ContainsKey("X-Content-Type-Options"))
            {
                headers.Append("X-Content-Type-Options", _options.XContentTypeOptionsPolicy);
            }

            // 5. X-XSS-Protection - Browser XSS filter (legacy support)
            if (_options.EnableXssProtection && !headers.ContainsKey("X-XSS-Protection"))
            {
                headers.Append("X-XSS-Protection", _options.XssProtectionPolicy);
            }

            // 6. Referrer-Policy - Controls referrer information
            if (_options.EnableReferrerPolicy && !headers.ContainsKey("Referrer-Policy"))
            {
                headers.Append("Referrer-Policy", _options.ReferrerPolicyValue);
            }

            // 7. Permissions-Policy - Limits browser features
            if (_options.EnablePermissionsPolicy && !headers.ContainsKey("Permissions-Policy"))
            {
                headers.Append("Permissions-Policy", _options.PermissionsPolicyValue);
            }
        }
        catch (Exception ex)
        {
            // Log error but don't fail the request - security headers are important but shouldn't break the application
            _logger.LogWarning(ex,
                "Failed to add security headers to response. Request will continue without security headers. Path: {Path}",
                context.Request.Path);
        }
    }

    /// <summary>
    /// Determines if HSTS should be enabled based on environment and request context.
    /// HSTS is skipped for:
    /// - Development environment
    /// - Localhost requests
    /// - Non-HTTPS requests (HSTS only makes sense for HTTPS)
    /// </summary>
    private bool ShouldEnableHsts(HttpContext context)
    {
        // Skip HSTS in development
        if (_environment.IsDevelopment())
        {
            return false;
        }

        // Skip HSTS for localhost (even in production deployments)
        var host = context.Request.Host.Host;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("[::1]", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Only enable HSTS for HTTPS requests
        if (!context.Request.IsHttps)
        {
            return false;
        }

        return true;
    }
}

/// <summary>
/// Configuration options for security headers middleware.
/// Provides sensible defaults while allowing customization via appsettings.json.
/// </summary>
public class SecurityHeadersOptions
{
    /// <summary>
    /// Configuration section name for appsettings.json binding.
    /// </summary>
    public const string SectionName = "SecurityHeaders";

    // Feature flags for each header
    public bool EnableCsp { get; set; } = true;
    public bool EnableHsts { get; set; } = true;
    public bool EnableXFrameOptions { get; set; } = true;
    public bool EnableXContentTypeOptions { get; set; } = true;
    public bool EnableXssProtection { get; set; } = true;
    public bool EnableReferrerPolicy { get; set; } = true;
    public bool EnablePermissionsPolicy { get; set; } = true;

    // Header policies with secure defaults

    /// <summary>
    /// Content-Security-Policy header value.
    /// Default policy allows React/Next.js frontend while maintaining security:
    /// - default-src 'self': Only load resources from same origin
    /// - script-src 'self' 'unsafe-inline': Allow inline scripts for React hydration
    /// - style-src 'self' 'unsafe-inline': Allow inline styles for Tailwind/CSS-in-JS
    /// - img-src 'self' data: https:: Allow images from same origin, data URIs, and HTTPS
    /// - font-src 'self' data:: Allow fonts from same origin and data URIs
    /// - connect-src 'self': Allow API calls to same origin only
    /// - frame-ancestors 'none': Prevent embedding in iframes (clickjacking protection)
    /// </summary>
    public string CspPolicy { get; set; } =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: https:; " +
        "font-src 'self' data:; " +
        "connect-src 'self'; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";

    /// <summary>
    /// Strict-Transport-Security header value.
    /// Default: max-age=31536000 (1 year), includeSubDomains, preload
    /// Forces HTTPS for 1 year and includes subdomains.
    /// </summary>
    public string HstsPolicy { get; set; } = "max-age=31536000; includeSubDomains; preload";

    /// <summary>
    /// X-Frame-Options header value.
    /// Default: DENY (prevents any framing, strongest protection against clickjacking)
    /// </summary>
    public string XFrameOptionsPolicy { get; set; } = "DENY";

    /// <summary>
    /// X-Content-Type-Options header value.
    /// Default: nosniff (prevents MIME type sniffing)
    /// </summary>
    public string XContentTypeOptionsPolicy { get; set; } = "nosniff";

    /// <summary>
    /// X-XSS-Protection header value.
    /// Default: 1; mode=block (enables XSS filter and blocks page if attack detected)
    /// Note: This is a legacy header, modern browsers rely on CSP instead.
    /// </summary>
    public string XssProtectionPolicy { get; set; } = "1; mode=block";

    /// <summary>
    /// Referrer-Policy header value.
    /// Default: strict-origin-when-cross-origin (balanced security and functionality)
    /// </summary>
    public string ReferrerPolicyValue { get; set; } = "strict-origin-when-cross-origin";

    /// <summary>
    /// Permissions-Policy header value.
    /// Default: Disables potentially dangerous browser features
    /// - camera, microphone: Privacy-sensitive media capture
    /// - geolocation: Location tracking
    /// - payment: Payment Request API
    /// - usb, serial: Hardware device access
    /// - sync-xhr: Synchronous XHR (deprecated, performance issue)
    /// - fullscreen: Fullscreen API (prevents unauthorized fullscreen)
    /// - picture-in-picture: PiP video mode (prevents abuse)
    /// - accelerometer, gyroscope, magnetometer: Motion/orientation sensors
    /// - web-share: Web Share API
    /// </summary>
    public string PermissionsPolicyValue { get; set; } =
        "camera=(), " +
        "microphone=(), " +
        "geolocation=(), " +
        "payment=(), " +
        "usb=(), " +
        "serial=(), " +
        "sync-xhr=(), " +
        "fullscreen=(), " +
        "picture-in-picture=(), " +
        "accelerometer=(), " +
        "gyroscope=(), " +
        "magnetometer=(), " +
        "web-share=()";
}

/// <summary>
/// Validator for SecurityHeadersOptions to ensure configuration correctness at startup.
/// Validates policy strings and detects common configuration errors.
/// </summary>
public class SecurityHeadersOptionsValidator : IValidateOptions<SecurityHeadersOptions>
{
    public ValidateOptionsResult Validate(string? name, SecurityHeadersOptions options)
    {
        var errors = new List<string>();

        // Validate HSTS policy
        if (options.EnableHsts)
        {
            if (string.IsNullOrWhiteSpace(options.HstsPolicy))
            {
                errors.Add("HSTS policy cannot be null or empty when HSTS is enabled");
            }
            else if (!options.HstsPolicy.Contains("max-age=", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("HSTS policy must contain 'max-age=' directive");
            }
            else
            {
                // Extract max-age value and validate it's a number
                // FIX MA0009: Add timeout to prevent ReDoS attacks
                var maxAgeMatch = System.Text.RegularExpressions.Regex.Match(
                    options.HstsPolicy,
                    @"max-age=(\d+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase,
                    TimeSpan.FromSeconds(1));

                if (maxAgeMatch.Success)
                {
                    var maxAge = int.Parse(maxAgeMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                    if (maxAge < 0)
                    {
                        errors.Add("HSTS max-age must be a positive number");
                    }
                    if (options.HstsPolicy.Contains("preload", StringComparison.OrdinalIgnoreCase))
                    {
                        // Preload requires max-age >= 31536000 (1 year) and includeSubDomains
                        if (maxAge < 31536000)
                        {
                            errors.Add("HSTS preload requires max-age of at least 31536000 (1 year)");
                        }
                        if (!options.HstsPolicy.Contains("includeSubDomains", StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add("HSTS preload requires includeSubDomains directive");
                        }
                    }
                }
            }
        }

        // Validate CSP policy
        if (options.EnableCsp && string.IsNullOrWhiteSpace(options.CspPolicy))
        {
            errors.Add("CSP policy cannot be null or empty when CSP is enabled");
        }

        // Validate X-Frame-Options
        if (options.EnableXFrameOptions)
        {
            if (string.IsNullOrWhiteSpace(options.XFrameOptionsPolicy))
            {
                errors.Add("X-Frame-Options policy cannot be null or empty when enabled");
            }
            else
            {
                var validValues = new[] { "DENY", "SAMEORIGIN" };
                var upperPolicy = options.XFrameOptionsPolicy.Trim().ToUpperInvariant();
                if (!validValues.Contains(upperPolicy, StringComparer.Ordinal) && !upperPolicy.StartsWith("ALLOW-FROM"))
                {
                    errors.Add($"X-Frame-Options must be DENY, SAMEORIGIN, or ALLOW-FROM uri (got: {options.XFrameOptionsPolicy})");
                }
            }
        }

        // Validate X-Content-Type-Options
        if (options.EnableXContentTypeOptions)
        {
            if (string.IsNullOrWhiteSpace(options.XContentTypeOptionsPolicy))
            {
                errors.Add("X-Content-Type-Options policy cannot be null or empty when enabled");
            }
            else if (!options.XContentTypeOptionsPolicy.Equals("nosniff", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"X-Content-Type-Options must be 'nosniff' (got: {options.XContentTypeOptionsPolicy})");
            }
        }

        // Validate X-XSS-Protection
        if (options.EnableXssProtection)
        {
            if (string.IsNullOrWhiteSpace(options.XssProtectionPolicy))
            {
                errors.Add("X-XSS-Protection policy cannot be null or empty when enabled");
            }
            else
            {
                var validPatterns = new[] { "0", "1", "1; mode=block" };
                if (!validPatterns.Contains(options.XssProtectionPolicy.Trim(), StringComparer.Ordinal))
                {
                    errors.Add($"X-XSS-Protection must be '0', '1', or '1; mode=block' (got: {options.XssProtectionPolicy})");
                }
            }
        }

        // Validate Referrer-Policy
        if (options.EnableReferrerPolicy)
        {
            if (string.IsNullOrWhiteSpace(options.ReferrerPolicyValue))
            {
                errors.Add("Referrer-Policy cannot be null or empty when enabled");
            }
            else
            {
                var validPolicies = new[]
                {
                    "no-referrer",
                    "no-referrer-when-downgrade",
                    "origin",
                    "origin-when-cross-origin",
                    "same-origin",
                    "strict-origin",
                    "strict-origin-when-cross-origin",
                    "unsafe-url"
                };
                if (!validPolicies.Contains(options.ReferrerPolicyValue.Trim(), StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add($"Referrer-Policy value '{options.ReferrerPolicyValue}' is not a valid policy");
                }
            }
        }

        // Validate Permissions-Policy
        if (options.EnablePermissionsPolicy && string.IsNullOrWhiteSpace(options.PermissionsPolicyValue))
        {
            errors.Add("Permissions-Policy cannot be null or empty when enabled");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}

/// <summary>
/// Extension methods for registering security headers middleware.
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    /// <summary>
    /// Adds security headers middleware to the pipeline.
    /// MUST be called BEFORE UseCors() to ensure headers are added to all responses including CORS preflight.
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }

    /// <summary>
    /// Registers security headers configuration services with startup validation.
    /// Binds SecurityHeadersOptions from appsettings.json "SecurityHeaders" section.
    /// Validates configuration at startup to catch errors early.
    /// </summary>
    public static IServiceCollection AddSecurityHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SecurityHeadersOptions>(
            configuration.GetSection(SecurityHeadersOptions.SectionName));

        // Register validator and enable startup validation
        services.AddSingleton<IValidateOptions<SecurityHeadersOptions>, SecurityHeadersOptionsValidator>();
        services.AddOptions<SecurityHeadersOptions>().ValidateOnStart();

        return services;
    }
}