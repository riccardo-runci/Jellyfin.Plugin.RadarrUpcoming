using System;
using System.Collections.Generic;
using Jellyfin.Plugin.RadarrUpcoming.Api;

namespace Jellyfin.Plugin.RadarrUpcoming;

/// <summary>
/// Simple in-memory cache for the last-fetched Radarr upcoming movies list.
/// </summary>
public static class UpcomingMoviesCache
{
    private static IReadOnlyList<UpcomingMovieDto> _movies = Array.Empty<UpcomingMovieDto>();
    private static DateTime _lastUpdated = DateTime.MinValue;

    /// <summary>Gets the timestamp of the last successful sync.</summary>
    public static DateTime LastUpdated => _lastUpdated;

    /// <summary>
    /// Returns the currently cached list of upcoming movies.
    /// </summary>
    public static IReadOnlyList<UpcomingMovieDto> Get() => _movies;

    /// <summary>
    /// Replaces the cached list with a new one.
    /// </summary>
    /// <param name="movies">The new list to cache.</param>
    public static void Set(IReadOnlyList<UpcomingMovieDto> movies)
    {
        _movies = movies;
        _lastUpdated = DateTime.UtcNow;
    }
}
