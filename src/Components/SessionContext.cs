using Donkeh.UILab;
using SonarDotNet.Core;

namespace SonarDotNet.Components;

/// <summary>Shares the studio session with every panel.</summary>
internal static class SessionContext
{
    public static readonly Context<StudioSession> Value = new(null!);
}
