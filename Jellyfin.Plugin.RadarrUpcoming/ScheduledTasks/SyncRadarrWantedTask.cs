using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.RadarrUpcoming.Api;
using Jellyfin.Plugin.RadarrUpcoming.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RadarrUpcoming.ScheduledTasks;

/// <summary>
/// Scheduled task that syncs the Radarr Wanted list, updates the in-memory cache,
/// writes stub files to disk, and keeps the Jellyfin collection in sync.
/// </summary>
public class SyncRadarrWantedTask : IScheduledTask
{
    private readonly RadarrService _radarrService;
    private readonly CollectionSyncService _collectionSyncService;
    private readonly StubWriterService _stubWriterService;
    private readonly ILogger<SyncRadarrWantedTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncRadarrWantedTask"/> class.
    /// </summary>
    public SyncRadarrWantedTask(
        RadarrService radarrService,
        CollectionSyncService collectionSyncService,
        StubWriterService stubWriterService,
        ILogger<SyncRadarrWantedTask> logger)
    {
        _radarrService = radarrService;
        _collectionSyncService = collectionSyncService;
        _stubWriterService = stubWriterService;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Sync Radarr Wanted List";

    /// <inheritdoc />
    public string Key => "RadarrUpcomingSyncWanted";

    /// <inheritdoc />
    public string Description => "Fetches the Wanted list from Radarr, writes stub files to disk, and keeps the 'Radarr Upcoming' Jellyfin collection in sync.";

    /// <inheritdoc />
    public string Category => "Radarr Upcoming";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        progress.Report(0);

        var config = Plugin.Instance?.Configuration;
        if (config is null)
        {
            _logger.LogWarning("RadarrUpcoming: Plugin configuration unavailable, skipping sync.");
            return;
        }

        _logger.LogInformation("RadarrUpcoming: Starting Radarr Wanted list sync.");
        progress.Report(10);

        var movies = await _radarrService.GetWantedMoviesAsync(
            config.RadarrUrl,
            config.ApiKey,
            config.MaxMovies,
            config.OnlyWithReleaseDate,
            cancellationToken).ConfigureAwait(false);

        progress.Report(40);

        // Update in-memory cache
        UpcomingMoviesCache.Set(movies);
        _logger.LogInformation("RadarrUpcoming: {Count} upcoming movies cached.", movies.Count);

        // Write .strm + .nfo stubs to disk so Jellyfin can scan them
        if (!string.IsNullOrWhiteSpace(config.StubLibraryPath))
        {
            await _stubWriterService.SyncStubsAsync(movies, config.StubLibraryPath, cancellationToken)
                .ConfigureAwait(false);
        }

        progress.Report(70);

        // Sync the Jellyfin collection (matches library items by TmdbId/ImdbId)
        await _collectionSyncService.SyncAsync(movies, cancellationToken).ConfigureAwait(false);

        progress.Report(100);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return new[]
        {
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfo.TriggerInterval,
                IntervalTicks = TimeSpan.FromHours(1).Ticks
            }
        };
    }
}
