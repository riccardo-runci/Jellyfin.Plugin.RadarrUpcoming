using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RadarrUpcoming.Api;

/// <summary>
/// Service responsible for communicating with the Radarr API.
/// </summary>
public class RadarrService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RadarrService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RadarrService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="logger">Logger instance.</param>
    public RadarrService(IHttpClientFactory httpClientFactory, ILogger<RadarrService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Fetches all monitored, not-yet-downloaded movies from Radarr (the "Wanted" list).
    /// </summary>
    /// <param name="radarrUrl">Base URL of the Radarr instance.</param>
    /// <param name="apiKey">The Radarr API key.</param>
    /// <param name="maxMovies">Maximum number of results to return.</param>
    /// <param name="onlyWithReleaseDate">When true, only returns movies that have a known release date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of upcoming movie DTOs.</returns>
    public async Task<IReadOnlyList<UpcomingMovieDto>> GetWantedMoviesAsync(
        string radarrUrl,
        string apiKey,
        int maxMovies,
        bool onlyWithReleaseDate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(radarrUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("RadarrUpcoming: Radarr URL or API key is not configured.");
            return Array.Empty<UpcomingMovieDto>();
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var baseUrl = radarrUrl.TrimEnd('/');
            var requestUrl = $"{baseUrl}/api/v3/movie?apikey={apiKey}";

            _logger.LogDebug("RadarrUpcoming: Fetching movies from {Url}", $"{baseUrl}/api/v3/movie");

            var movies = await client.GetFromJsonAsync<List<RadarrMovie>>(requestUrl, cancellationToken)
                         ?? new List<RadarrMovie>();

            // Filter: monitored + not yet downloaded (these are the "wanted" movies)
            var wanted = movies
                .Where(m => m.Monitored && !m.HasFile)
                .ToList();

            if (onlyWithReleaseDate)
            {
                wanted = wanted
                    .Where(m => m.InCinemas.HasValue || m.PhysicalRelease.HasValue || m.DigitalRelease.HasValue)
                    .ToList();
            }

            // Sort: closest upcoming release first, then by title
            wanted = wanted
                .OrderBy(m => GetEarliestReleaseDate(m) ?? DateTime.MaxValue)
                .ThenBy(m => m.SortTitle)
                .Take(maxMovies)
                .ToList();

            return wanted.Select(MapToDto).ToList();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "RadarrUpcoming: HTTP error while contacting Radarr at {Url}", radarrUrl);
            return Array.Empty<UpcomingMovieDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RadarrUpcoming: Unexpected error fetching Radarr wanted list.");
            return Array.Empty<UpcomingMovieDto>();
        }
    }

    private static DateTime? GetEarliestReleaseDate(RadarrMovie movie)
    {
        var dates = new[] { movie.InCinemas, movie.PhysicalRelease, movie.DigitalRelease }
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .ToList();

        return dates.Count > 0 ? dates.Min() : (DateTime?)null;
    }

    private static UpcomingMovieDto MapToDto(RadarrMovie m)
    {
        var poster = m.Images.FirstOrDefault(i => i.CoverType == "poster");
        var fanart = m.Images.FirstOrDefault(i => i.CoverType == "fanart");

        return new UpcomingMovieDto
        {
            RadarrId = m.Id,
            Title = m.Title,
            Year = m.Year,
            Overview = m.Overview,
            InCinemas = m.InCinemas,
            ReleaseDate = m.PhysicalRelease ?? m.DigitalRelease,
            Runtime = m.Runtime,
            Genres = m.Genres,
            TmdbId = m.TmdbId,
            ImdbId = m.ImdbId,
            PosterUrl = poster?.RemoteUrl ?? poster?.Url,
            FanartUrl = fanart?.RemoteUrl ?? fanart?.Url,
            CommunityRating = m.Ratings?.Tmdb?.Value ?? m.Ratings?.Imdb?.Value,
            Status = m.Status
        };
    }
}
