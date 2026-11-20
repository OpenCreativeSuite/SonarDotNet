using Donkeh.Hosting;
using Donkeh.UILab;
using SonarDotNet.Components;
using SonarDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace SonarDotNet;

/// <summary>
/// The application root. Owns the studio session and rebuilds the tree when the
/// session or the playback/recording clock changes.
/// </summary>
[AppRoot(
    Width = 1440,
    Height = 920,
    Title = "Sonus.NET",
    ClearColor = 0xFF090B0E,
    MinWidth = 1120,
    MinHeight = 720)]
public sealed class App : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        Ref<StudioSession> reference = UseRef<StudioSession>();
        StudioSession session = reference.Value ??= new StudioSession();
        Ref<bool> screenshotLayerApplied = UseRef<bool>();
        if (!screenshotLayerApplied.Value)
        {
            screenshotLayerApplied.Value = true;
            switch (Environment.GetEnvironmentVariable("SONUS_SCREENSHOT_LAYER"))
            {
                case "settings":
                    session.ToggleSettings();
                    break;
                case "presets":
                    session.TogglePresets();
                    break;
                case "help":
                    session.ToggleHelp();
                    break;
            }
        }

        var (_, _, tick) = StateWithUpdate(0);
        session.Refresh = () => tick(value => value + 1);

        Effect(() =>
        {
            System.Threading.Timer timer = new(
                _ => Host.Post(() => tick(value => value + 1)),
                null,
                TimeSpan.FromMilliseconds(200),
                TimeSpan.FromMilliseconds(200));
            return () => timer.Dispose();
        }, []);

        Node root = Div(new()
        {
            ClassName = "app",
            OnKeyDown = chord => HandleKey(session, chord),
        },
        C<TopBar>(),
        C<DeviceBar>(),
        C<StageRail>(),
        C<StageEditor>(),
        C<LivePanel>(),
        C<StatusBar>(),
        Show(session.OpenChoice is not null, C<DeviceMenu>()),
        Show(session.ShowPresets, C<PresetPopover>()),
        Show(session.ShowSavePreset, C<SavePresetDialog>()),
        Show(session.ShowSettings, C<SettingsDialog>()),
        Show(session.ShowHelp, C<HelpDialog>()));

        return Provider(SessionContext.Value, session, root);
    }

    private static void HandleKey(StudioSession session, string chord)
    {
        if (chord == "Escape")
        {
            session.CloseLayers();
            return;
        }

        if (session.IsTyping)
        {
            return;
        }

        switch (chord)
        {
            case "Space":
            case " ":
                session.ToggleLive();
                break;
            case "R":
            case "r":
                if (session.Recording is not null)
                {
                    session.StopRecording();
                }
                else
                {
                    session.StartRecording();
                }

                break;
            case "1":
                session.SelectStage(StudioStage.Clean);
                break;
            case "2":
                session.SelectStage(StudioStage.Tone);
                break;
            case "3":
                session.SelectStage(StudioStage.Dynamics);
                break;
            case "4":
                session.SelectStage(StudioStage.Voice);
                break;
            case "5":
                session.SelectStage(StudioStage.Space);
                break;
            case "Ctrl+S":
                session.OpenSavePreset();
                break;
        }
    }
}
