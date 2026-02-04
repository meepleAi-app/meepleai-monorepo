namespace Api.BoundedContexts.Authentication.Domain.ValueObjects;

/// <summary>
/// Value object representing parsed device information from User-Agent string.
/// Issue #3340: Login device tracking and management.
/// </summary>
public sealed record DeviceInfo
{
    public string DeviceType { get; init; }
    public string Browser { get; init; }
    public string BrowserVersion { get; init; }
    public string OperatingSystem { get; init; }
    public string OsVersion { get; init; }
    public bool IsMobile { get; init; }
    public string RawUserAgent { get; init; }

    private DeviceInfo(
        string deviceType,
        string browser,
        string browserVersion,
        string operatingSystem,
        string osVersion,
        bool isMobile,
        string rawUserAgent)
    {
        DeviceType = deviceType;
        Browser = browser;
        BrowserVersion = browserVersion;
        OperatingSystem = operatingSystem;
        OsVersion = osVersion;
        IsMobile = isMobile;
        RawUserAgent = rawUserAgent;
    }

    /// <summary>
    /// Parses a User-Agent string to extract device information.
    /// </summary>
    public static DeviceInfo Parse(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return new DeviceInfo(
                deviceType: "Unknown",
                browser: "Unknown",
                browserVersion: "",
                operatingSystem: "Unknown",
                osVersion: "",
                isMobile: false,
                rawUserAgent: userAgent ?? "");
        }

        var (os, osVersion) = ParseOperatingSystem(userAgent);
        var (browser, browserVersion) = ParseBrowser(userAgent);
        var isMobile = IsMobileDevice(userAgent);
        var deviceType = DetermineDeviceType(userAgent, isMobile);

        return new DeviceInfo(
            deviceType: deviceType,
            browser: browser,
            browserVersion: browserVersion,
            operatingSystem: os,
            osVersion: osVersion,
            isMobile: isMobile,
            rawUserAgent: userAgent);
    }

    private static (string os, string version) ParseOperatingSystem(string userAgent)
    {
        // Windows
        if (userAgent.Contains("Windows NT 10.0", StringComparison.OrdinalIgnoreCase))
            return ("Windows", "10/11");
        if (userAgent.Contains("Windows NT 6.3", StringComparison.OrdinalIgnoreCase))
            return ("Windows", "8.1");
        if (userAgent.Contains("Windows NT 6.2", StringComparison.OrdinalIgnoreCase))
            return ("Windows", "8");
        if (userAgent.Contains("Windows NT 6.1", StringComparison.OrdinalIgnoreCase))
            return ("Windows", "7");
        if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase))
            return ("Windows", "");

        // iOS - Check BEFORE macOS because iOS user agents contain "like Mac OS X"
        if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ||
            userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase))
        {
            var version = ExtractVersion(userAgent, "OS ", "_");
            return ("iOS", version ?? "");
        }

        // macOS
        if (userAgent.Contains("Mac OS X", StringComparison.OrdinalIgnoreCase))
        {
            var version = ExtractVersion(userAgent, "Mac OS X ", "_");
            return ("macOS", version ?? "");
        }

        // Android
        if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase))
        {
            var version = ExtractVersion(userAgent, "Android ", " ");
            return ("Android", version ?? "");
        }

        // Linux
        if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase))
            return ("Linux", "");

        // Chrome OS
        if (userAgent.Contains("CrOS", StringComparison.OrdinalIgnoreCase))
            return ("Chrome OS", "");

        return ("Unknown", "");
    }

    private static (string browser, string version) ParseBrowser(string userAgent)
    {
        // Edge (check before Chrome as Edge contains "Chrome")
        if (userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase) ||
            userAgent.Contains("Edge/", StringComparison.OrdinalIgnoreCase))
        {
            var version = ExtractVersion(userAgent, "Edg/", " ") ?? ExtractVersion(userAgent, "Edge/", " ");
            return ("Edge", version ?? "");
        }

        // Firefox
        if (userAgent.Contains("Firefox/", StringComparison.OrdinalIgnoreCase))
        {
            var version = ExtractVersion(userAgent, "Firefox/", " ");
            return ("Firefox", version ?? "");
        }

        // Safari (check before Chrome as both can be present)
        if (userAgent.Contains("Safari/", StringComparison.OrdinalIgnoreCase) &&
            !userAgent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase))
        {
            var version = ExtractVersion(userAgent, "Version/", " ");
            return ("Safari", version ?? "");
        }

        // Chrome
        if (userAgent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase))
        {
            var version = ExtractVersion(userAgent, "Chrome/", " ");
            return ("Chrome", version ?? "");
        }

        // Opera
        if (userAgent.Contains("OPR/", StringComparison.OrdinalIgnoreCase) ||
            userAgent.Contains("Opera/", StringComparison.OrdinalIgnoreCase))
        {
            var version = ExtractVersion(userAgent, "OPR/", " ") ?? ExtractVersion(userAgent, "Opera/", " ");
            return ("Opera", version ?? "");
        }

        // Internet Explorer
        if (userAgent.Contains("MSIE", StringComparison.OrdinalIgnoreCase) ||
            userAgent.Contains("Trident/", StringComparison.OrdinalIgnoreCase))
        {
            return ("Internet Explorer", "");
        }

        return ("Unknown", "");
    }

    private static bool IsMobileDevice(string userAgent)
    {
        // Android tablets don't have "Mobile" in the user agent
        // So we check for Android + Mobile specifically
        if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase))
        {
            return userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase);
        }

        var mobileKeywords = new[]
        {
            "Mobile", "iPhone", "iPad", "iPod", "webOS",
            "BlackBerry", "Opera Mini", "IEMobile", "Windows Phone"
        };

        return mobileKeywords.Any(keyword =>
            userAgent.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string DetermineDeviceType(string userAgent, bool isMobile)
    {
        if (userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase) ||
            (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase) &&
             !userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase)))
        {
            return "Tablet";
        }

        if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ||
            (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase) &&
             userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase)))
        {
            return "Phone";
        }

        if (isMobile)
        {
            return "Mobile";
        }

        return "Desktop";
    }

    private static string? ExtractVersion(string userAgent, string prefix, string terminator)
    {
        var startIndex = userAgent.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0) return null;

        startIndex += prefix.Length;
        var endIndex = userAgent.IndexOf(terminator, startIndex, StringComparison.OrdinalIgnoreCase);
        if (endIndex < 0) endIndex = userAgent.Length;

        // Also stop at semicolon, parenthesis, or end of string
        var semicolonIndex = userAgent.IndexOf(';', startIndex);
        var parenIndex = userAgent.IndexOf(')', startIndex);

        if (semicolonIndex > 0 && semicolonIndex < endIndex) endIndex = semicolonIndex;
        if (parenIndex > 0 && parenIndex < endIndex) endIndex = parenIndex;

        var version = userAgent.Substring(startIndex, endIndex - startIndex).Trim();

        // Clean up version string (remove trailing characters)
        return version.Split(' ', ';', ')')[0];
    }

    /// <summary>
    /// Returns a human-readable device description.
    /// </summary>
    public string GetDisplayName()
    {
        var browserDisplay = string.IsNullOrEmpty(BrowserVersion)
            ? Browser
            : $"{Browser} {BrowserVersion.Split('.')[0]}";

        var osDisplay = string.IsNullOrEmpty(OsVersion)
            ? OperatingSystem
            : $"{OperatingSystem} {OsVersion}";

        return $"{browserDisplay} on {osDisplay}";
    }
}
