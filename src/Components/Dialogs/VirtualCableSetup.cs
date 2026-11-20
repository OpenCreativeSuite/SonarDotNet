using System.Diagnostics;
using System.IO.Compression;
using Donkeh.Hosting;
using SonarDotNet.Core;

namespace SonarDotNet.Components;

/// <summary>
/// Helps a first-time user install the VB-CABLE virtual audio device, then
/// reconnects Sonus's virtual output to it.
/// </summary>
/// <remarks>
/// The driver itself cannot be created by a user-mode application. VB-CABLE is
/// the freeware virtual cable recommended here; Sonus downloads the official
/// package from VB-Audio and hands control to their standalone installer. No
/// driver files are bundled with Sonus, and the installer remains the official,
/// signed VB-Audio package.
/// </remarks>
internal static class VirtualCableSetup
{
    /// <summary>The official VB-CABLE package.</summary>
    public const string DownloadUrl = "https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack46.zip";

    /// <summary>The project page shown for attribution and donations.</summary>
    public const string ProjectUrl = "https://vb-audio.com/Cable/";

    /// <summary>Downloads the official package and launches its setup program.</summary>
    public static void Install(StudioSession session)
    {
        session.EnableVirtualOutput();
        session.SetStatus("Downloading the VB-CABLE package…");
        Task.Run(async () =>
        {
            string folder = Path.Combine(Path.GetTempPath(), "Sonus", "VBCABLE_Driver_Pack46");
            try
            {
                Directory.CreateDirectory(folder);
                using HttpClient client = new();
                byte[] package = await client.GetByteArrayAsync(DownloadUrl).ConfigureAwait(false);
                string zip = Path.Combine(folder, "VBCABLE_Driver_Pack46.zip");
                await File.WriteAllBytesAsync(zip, package).ConfigureAwait(false);
                ZipFile.ExtractToDirectory(zip, folder, overwriteFiles: true);

                string setup = Path.Combine(folder, Environment.Is64BitOperatingSystem
                    ? "VBCABLE_Setup_x64.exe"
                    : "VBCABLE_Setup.exe");
                if (!File.Exists(setup))
                {
                    throw new FileNotFoundException("The VB-CABLE setup program was not in the package.");
                }

                Host.Post(() =>
                {
                    try
                    {
                        Process? process = Process.Start(new ProcessStartInfo(setup)
                        {
                            UseShellExecute = true,
                            Verb = "runas",
                            WorkingDirectory = folder,
                        });
                        session.SetStatus("Complete the VB-CABLE installer, then restart Windows.");
                        if (process is not null)
                        {
                            process.EnableRaisingEvents = true;
                            process.Exited += (_, _) => Host.Post(() =>
                            {
                                session.RefreshDevices();
                                session.SetStatus("VB-CABLE installed. Restart Windows, then reopen Sonus.");
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        session.SetStatus("VB-CABLE setup was not started: " + ex.Message);
                    }
                });
            }
            catch (Exception ex)
            {
                Host.Post(() => session.SetStatus("Could not download VB-CABLE: " + ex.Message));
            }
        });
    }

    /// <summary>Opens the official VB-CABLE page.</summary>
    public static void OpenPage(StudioSession session)
    {
        try
        {
            Process.Start(new ProcessStartInfo(ProjectUrl) { UseShellExecute = true });
            session.SetStatus("VB-CABLE opened in your browser. Restart Windows after installing.");
        }
        catch (Exception ex)
        {
            session.SetStatus("Could not open the browser: " + ex.Message);
        }
    }
}
