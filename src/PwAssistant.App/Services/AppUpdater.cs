using System.Windows;
using PwAssistant.App.Resources;
using PwAssistant.Core.Ux;
using Velopack;
using Velopack.Sources;

namespace PwAssistant.App.Services;

/// <summary>
/// Velopack self-update over GitHub Releases (public repo, no token).
/// Background startup check: prompt (pt-BR) → download → apply + restart.
/// Every failure ends in log + continue — updates never break startup,
/// and dev (non-installed) builds simply skip the check.
/// </summary>
public sealed class AppUpdater
{
    /// <summary>Stable feed: releases of this repo.</summary>
    public const string FeedUrl = "https://github.com/feelipechs/pwassistant";

    private readonly FileLogger _log;
    private readonly IDialogService _dialogs;

    public AppUpdater(FileLogger log, IDialogService dialogs)
    {
        _log = log;
        _dialogs = dialogs;
    }

    public async Task CheckAndPromptAsync()
    {
        UpdateInfo? update;
        try
        {
            var mgr = new UpdateManager(new GithubSource(FeedUrl, null, false));
            if (!mgr.IsInstalled)
                return;
            update = await mgr.CheckForUpdatesAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Info($"Update check skipped: {ex.Message}");
            return;
        }
        if (update is null)
            return;
        string version = update.TargetFullRelease.Version.ToString();
        _log.Info($"Update available: {version}.");
        bool apply;
        try
        {
            apply = await Application.Current.Dispatcher.InvokeAsync(() =>
                _dialogs.AskConfirmAsync(
                    Strings.UpdateAvailableTitle,
                    Strings.UpdateAvailableConfirm(version),
                    Strings.UpdateApply)).Task.Unwrap().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Warn($"Update prompt failed: {ex.Message}");
            return;
        }
        if (!apply)
            return;
        try
        {
            var mgr = new UpdateManager(new GithubSource(FeedUrl, null, false));
            await mgr.DownloadUpdatesAsync(update, _ => { }, CancellationToken.None).ConfigureAwait(false);
            mgr.ApplyUpdatesAndRestart(update);
        }
        catch (Exception ex)
        {
            _log.Error($"Update to {version} failed: {ex.Message}");
        }
    }
}
