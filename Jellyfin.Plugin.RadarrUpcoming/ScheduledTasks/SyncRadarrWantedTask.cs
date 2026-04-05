using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.RadarrUpcoming.Api;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RadarrUpcoming.ScheduledTasks;

/// <summary>
/// Scheduled task that syncs the Radarr Wanted list and stores it in the plugin's cache.
/// </summary>
public class SyncRadarrWantedTask : IScheduledTask
{
    private readonly RadarrService _radarrService;
    private readonly ILogger<SyncRadarrWantedTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncRadarrWantedTask"/> class.
    /// </summary>
    /// <param name="radarrService">Radarr service instance.</param>
    /// <param name="logger">Logger instance.</param>
    public SyncRadarrWantedTask(RadarrService radarrService, ILogger<SyncRadarrWantedTask> logger)
    {
        _radarrService = radarrService;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Sync Radarr Wanted List";

    /// <inheritdoc />
    public string Key => "RadarrUpcomingSyncWanted";

    /// <inheritdoc />
    public string Description => "Fetches the Wanted (missing) movie list from Radarr and caches it for display in the Upcoming section.";

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

        progress.Report(80);

        // Store result in the in-memory cache on the plugin instance
        UpcomingMoviesCache.Set(movies);

        _logger.LogInformation("RadarrUpcoming: Sync complete. {Count} upcoming movies cached.", movies.Count);
        progress.Report(100);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // Run once per hour by default
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
