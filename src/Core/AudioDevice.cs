namespace SonarDotNet.Core;

/// <summary>Whether an audio endpoint is captured from or rendered to.</summary>
public enum AudioDeviceKind
{
    Input,
    Output,
}

/// <summary>A selectable audio endpoint.</summary>
/// <param name="Id">The stable Windows endpoint id.</param>
/// <param name="Name">The friendly endpoint name.</param>
/// <param name="Kind">Capture or render.</param>
/// <param name="IsDefault">Whether Windows marks this as the default endpoint.</param>
/// <param name="IsVirtualCable">Whether the name looks like a virtual audio cable.</param>
public sealed record AudioDevice(
    string Id,
    string Name,
    AudioDeviceKind Kind,
    bool IsDefault = false,
    bool IsVirtualCable = false)
{
    /// <summary>A short, readable version of the endpoint name.</summary>
    public string ShortName
    {
        get
        {
            string name = Name;
            int parenthesis = name.IndexOf('(');
            if (parenthesis > 4)
            {
                name = name[..parenthesis].Trim().TrimEnd('-').Trim();
            }

            return name.Length > 38 ? name[..36] + "…" : name;
        }
    }
}

/// <summary>The state of the real-time audio host.</summary>
public enum AudioEngineState
{
    Stopped,
    Running,
    Error,
}
