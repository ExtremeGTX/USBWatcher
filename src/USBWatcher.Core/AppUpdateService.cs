using Velopack;
using Velopack.Sources;

namespace USBWatcher.Core;

public enum USBWatcherEdition
{
    WinUI,
    WinForms
}

public sealed class AvailableAppUpdate
{
    internal AvailableAppUpdate(UpdateInfo update) => Update = update;

    internal UpdateInfo Update { get; }

    public string Version => Update.TargetFullRelease.Version.ToString();
}

public sealed class AppUpdateService
{
    public const string ReleasesUrl = "https://github.com/ExtremeGTX/USBWatcher/releases/latest";
    private const string RepositoryUrl = "https://github.com/ExtremeGTX/USBWatcher";
    private static readonly TimeSpan AutomaticCheckInterval = TimeSpan.FromHours(24);

    private readonly UpdateManager? _manager;

    public AppUpdateService(USBWatcherEdition edition)
    {
        string channel = edition == USBWatcherEdition.WinUI ? "winui" : "winforms";

        try
        {
            var manager = new UpdateManager(
                new GithubSource(RepositoryUrl, accessToken: null, prerelease: false),
                new UpdateOptions { ExplicitChannel = channel });

            if (manager.IsInstalled && !manager.IsPortable)
            {
                _manager = manager;
            }
        }
        catch
        {
            // Development and ordinary ZIP builds are intentionally unmanaged.
        }
    }

    public bool IsManagedInstallation => _manager is not null;

    public static bool ShouldRunAutomaticCheck(DateTimeOffset? lastCheckUtc, DateTimeOffset nowUtc) =>
        !lastCheckUtc.HasValue || nowUtc - lastCheckUtc.Value >= AutomaticCheckInterval;

    public async Task<AvailableAppUpdate?> CheckForUpdatesAsync()
    {
        if (_manager is null)
        {
            return null;
        }

        UpdateInfo? update = await _manager.CheckForUpdatesAsync();
        return update is null ? null : new AvailableAppUpdate(update);
    }

    public Task DownloadAsync(
        AvailableAppUpdate update,
        Action<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (_manager is null)
        {
            throw new InvalidOperationException("Updates are available only for Velopack installations.");
        }

        return _manager.DownloadUpdatesAsync(update.Update, progress, cancellationToken);
    }

    public void PrepareUpdateAndRestart(AvailableAppUpdate update, Action exitApplication)
    {
        if (_manager is null)
        {
            throw new InvalidOperationException("Updates are available only for Velopack installations.");
        }

        _manager.WaitExitThenApplyUpdates(
            update.Update.TargetFullRelease,
            silent: false,
            restart: true);
        exitApplication();
    }
}
