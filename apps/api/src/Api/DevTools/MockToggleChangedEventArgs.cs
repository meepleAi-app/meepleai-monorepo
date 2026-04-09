using System;

namespace Api.DevTools;

internal sealed class MockToggleChangedEventArgs(string serviceName, bool mocked) : EventArgs
{
    public string ServiceName { get; } = serviceName;
    public bool Mocked { get; } = mocked;
}
