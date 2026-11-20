using SonarDotNet.Components;
using SonarDotNet.Core;
using SonarDotNet.Core.Audio;
using static Donkeh.UILab._;

namespace SonarDotNet.Tests;

public sealed class ComponentTests
{
    [Fact]
    public void StageRailShowsTheFiveSignalStagesAndSelectsThem()
    {
        StudioSession session = Fx.Session();
        var page = Fx.Render(session, C<StageRail>());

        Assert.Equal(5, page.FindAll(".stage-card").Count);
        Assert.Equal("Tone", page.Find(".stage-card.is-active .stage-name").Text);
        Assert.Contains("Five stages", page.Find(".rail-title").Text);
        Assert.Null(page.TryFind(".rail-spacer"));

        page.FindAll(".stage-card")[0].Click();
        Assert.Equal(StudioStage.Clean, session.ActiveStage);

        Fx.Reload<StageRail>(page, session);
        Assert.Equal("Clean", page.Find(".stage-card.is-active .stage-name").Text);

        page.FindAll(".stage-card")[2].Find(".stage-name").Click();
        Assert.Equal(StudioStage.Dynamics, session.ActiveStage);
    }

    [Fact]
    public void EditorSwitchBypassesTheActiveStage()
    {
        StudioSession session = Fx.Session();
        var page = Fx.Render(session, C<StageEditor>());

        page.Find(".editor-enable .switch").Click();
        Assert.False(session.Settings.Tone.Enabled);
        Assert.Equal(StudioStage.Tone, session.ActiveStage);
    }

    [Fact]
    public void StageEditorRendersTheInteractiveToneControls()
    {
        StudioSession session = Fx.Session();
        var page = Fx.Render(session, C<StageEditor>());

        Assert.Equal("Tone", page.Find(".editor-title").Text);
        Assert.NotNull(page.TryFind(".tone-graph"));
        Assert.Equal(2, page.FindAll(".slider").Count);
    }

    [Fact]
    public void StageEditorSwitchesToTheChosenStage()
    {
        StudioSession session = Fx.Session();
        session.SelectStage(StudioStage.Dynamics);
        var page = Fx.Render(session, C<StageEditor>());

        Assert.Equal("Dynamics", page.Find(".editor-title").Text);
        Assert.NotNull(page.TryFind(".dynamics-graph"));
        Assert.Equal(4, page.FindAll(".slider, .toggle-block").Count);
    }

    [Fact]
    public void VoiceStageExposesPitchFormantResonanceBrightnessAndTexture()
    {
        StudioSession session = Fx.Session();
        session.SelectStage(StudioStage.Voice);
        var page = Fx.Render(session, C<StageEditor>());

        Assert.Equal("Voice", page.Find(".editor-title").Text);
        Assert.Equal(5, page.FindAll(".slider").Count);
        page.Input(".slider-input", "5");
        Assert.Equal(5f, session.Settings.Character.PitchSemitones);
    }

    [Fact]
    public void SpaceStageExposesRoomAndEchoControls()
    {
        StudioSession session = Fx.Session();
        session.SelectStage(StudioStage.Space);
        var page = Fx.Render(session, C<StageEditor>());

        Assert.Equal("Space", page.Find(".editor-title").Text);
        Assert.NotNull(page.TryFind(".space-graph"));
        Assert.Equal(4, page.FindAll(".slider").Count);
    }

    [Fact]
    public void DeviceBarChangesTheMicrophoneAndShowsVirtualReadiness()
    {
        StudioSession session = Fx.Session();
        var page = Fx.Render(session, C<DeviceBar>());

        Assert.Equal("READY", page.Find(".virtual-state").Text);
        Assert.Equal("CABLE Output", page.Find(".virtual-name").Text);
        Assert.False(session.VirtualEnabled);

        page.Find(".choice").Click();
        Assert.NotNull(session.OpenChoice);
        var menu = Fx.Render(session, C<DeviceMenu>());
        menu.FindAll(".choice-option")[1].Click();
        Assert.Equal("mic-2", session.InputDeviceId);

        Fx.Reload<DeviceBar>(page, session);
        page.Find(".virtual-cluster .switch").Click();
        Assert.True(session.VirtualEnabled);
        Assert.True(((FakeAudioService)session.Audio).StartCount > 0);
    }

    [Fact]
    public void LivePanelStartsARecordingAndExplainsWhenNoTakesExist()
    {
        StudioSession session = Fx.Session();
        var page = Fx.Render(session, C<LivePanel>());

        Assert.Contains("No takes yet", page.Find(".empty-title").Text);
        page.Find(".monitor-test").Click();
        Assert.Equal(1, ((FakeAudioService)session.Audio).TestCount);
        page.Find(".record-btn").Click();
        Assert.Equal(1, ((FakeAudioService)session.Audio).RecordingCount);
    }

    [Fact]
    public void TakesCanBeDiscardedAndFilesDeleted()
    {
        StudioSession session = Fx.Session();
        string folder = Path.Combine(Path.GetTempPath(), "SonarDotNetTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string raw = Path.Combine(folder, "raw.wav");
        string processed = Path.Combine(folder, "processed.wav");
        File.WriteAllBytes(raw, [1, 2, 3]);
        File.WriteAllBytes(processed, [4, 5, 6]);
        session.Takes.Add(new RecordingResult(raw, processed, TimeSpan.FromSeconds(1)));
        try
        {
            var page = Fx.Render(session, C<LivePanel>());
            page.Find(".take-discard").Click();

            Assert.Empty(session.Takes);
            Assert.False(File.Exists(raw));
            Assert.False(File.Exists(processed));
        }
        finally
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch
            {
                // Cleanup is not part of the assertion.
            }
        }
    }

    [Fact]
    public void SettingsDialogExplainsTheVirtualCableWhenOneIsPresent()
    {
        StudioSession session = Fx.Session();
        session.ToggleSettings();
        var page = Fx.Render(session, C<SettingsDialog>());

        Assert.NotNull(page.TryFind(".cable-note.is-ready"));
        Assert.Contains("Virtual microphone ready", page.Find(".cable-note-title").Text);
        Assert.NotNull(page.TryFind(".settings-modal"));
    }

    [Fact]
    public void PresetPopoverAppliesABuiltInSound()
    {
        StudioSession session = Fx.Session();
        session.TogglePresets();
        var page = Fx.Render(session, C<PresetPopover>());

        Assert.Equal(9, page.FindAll(".preset-row").Count);
        Assert.True(page.Find(".preset-list").Bounds.Height > 100f);
        page.FindByText("Broadcast").Click();

        Assert.Equal("Broadcast", session.PresetName);
        Assert.Equal(3.5f, session.Settings.Tone.Bands[2].GainDb, 1);
    }

    [Fact]
    public void TopBarShowsTheLocalOnlyPromiseAndPreset()
    {
        StudioSession session = Fx.Session();
        var page = Fx.Render(session, C<TopBar>());

        Assert.Equal("Natural", page.Find(".preset-name").Text);
        Assert.Contains("LOCAL ONLY", page.Find(".local-badge").InnerText);
        Assert.Equal(3, page.FindAll(".top-btn").Count);
    }
}
