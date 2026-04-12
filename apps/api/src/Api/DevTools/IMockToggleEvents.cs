using System;

namespace Api.DevTools;

/// <summary>
/// Event subscription access to mock toggle changes.
/// </summary>
internal interface IMockToggleEvents
{
    event EventHandler<MockToggleChangedEventArgs>? ToggleChanged;
}
