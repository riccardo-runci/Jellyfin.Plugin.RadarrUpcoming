using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.RadarrUpcoming.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RadarrUpcoming.Controllers;

/// <summary>
/// REST API controller for the Radarr Upcoming plugin.
/// Exposes endpoints consumed by the Jellyfin Web UI to display the Upcoming section.
/// </summary>
[ApiController]
[Route("RadarrUpcoming")]
[Produces(MediaTypeNames.Application.Json)]
public class RadarrUpcomingController : ControllerBase
{
    private readonly RadarrService _radarrService;
    private readonly ILogger<RadarrUpcomingController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RadarrUpcomingController"/> class.
    /// </summary>
    /// <param name="radarrService">Radarr service instance.</param>
    /// <param name="logger">Logger instance.</param>
    public RadarrUpcomingController(RadarrService radarrService, ILogger<RadarrUpcomingController> logger)
    {
        _radarrService = radarrService;
        _logger = logger;
    }

    /// <summary>
    /// Returns the cached list of upcoming (wanted) movies from Radarr.
    /// </summary>
    /// <response code="200">Returns the list of upcoming movies.</response>
    /// <response code="503">Plugin is not configured.</response>
    [HttpGet("upcoming")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public ActionResult<IReadOnlyList<UpcomingMovieDto>> GetUpcoming()
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || string.IsNullOrWhiteSpace(config.ApiKey))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Radarr Upcoming plugin is not configured.");
        }

        var movies = UpcomingMoviesCache.Get();
        return Ok(movies);
    }

    /// <summary>
    /// Immediately refreshes the upcoming movies list by calling Radarr.
    /// Requires authentication.
    /// </summary>
    /// <response code="200">Returns the freshly-fetched list.</response>
    /// <response code="503">Plugin is not configured.</response>
    [HttpPost("refresh")]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<UpcomingMovieDto>>> RefreshUpcoming(
        CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is null || string.IsNullOrWhiteSpace(config.ApiKey))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Radarr Upcoming plugin is not configured.");
        }

        _logger.LogInformation("RadarrUpcoming: Manual refresh triggered via API.");

        var movies = await _radarrService.GetWantedMoviesAsync(
            config.RadarrUrl,
            config.ApiKey,
            config.MaxMovies,
            config.OnlyWithReleaseDate,
            cancellationToken).ConfigureAwait(false);

        UpcomingMoviesCache.Set(movies);
        return Ok(movies);
    }

    /// <summary>
    /// Returns metadata about the cache (last updated timestamp, movie count).
    /// </summary>
    /// <response code="200">Returns cache metadata.</response>
    [HttpGet("status")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<object> GetStatus()
    {
        return Ok(new
        {
            lastUpdated = UpcomingMoviesCache.LastUpdated,
            movieCount = UpcomingMoviesCache.Get().Count,
            isConfigured = !string.IsNullOrWhiteSpace(Plugin.Instance?.Configuration?.ApiKey)
        });
    }
}
