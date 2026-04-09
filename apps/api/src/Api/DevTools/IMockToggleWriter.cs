namespace Api.DevTools;

/// <summary>
/// Write-only access to mock toggle state. Injected into /dev/toggles endpoints (phase 2).
/// </summary>
internal interface IMockToggleWriter
{
    void Set(string serviceName, bool mocked);
}
