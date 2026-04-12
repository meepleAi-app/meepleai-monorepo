using System.Collections.Generic;

namespace Api.DevTools;

/// <summary>
/// Read-only access to mock toggle state. Injected into MockAwareProxy&lt;T&gt;.
/// </summary>
internal interface IMockToggleReader
{
    bool IsMocked(string serviceName);
    IReadOnlyDictionary<string, bool> GetAll();
}
