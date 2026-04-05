using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.RadarrUpcoming.Api;

/// <summary>
/// Represents a movie from Radarr's API.
/// </summary>
public class RadarrMovie
{
    /// <summary>Gets or sets the Radarr internal ID.</summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>Gets or sets the movie title.</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the sort title.</summary>
    [JsonPropertyName("sortTitle")]
    public string SortTitle { get; set; } = string.Empty;

    /// <summary>Gets or sets the release year.</summary>
    [JsonPropertyName("year")]
    public int Year { get; set; }

    /// <summary>Gets or sets the overview / synopsis.</summary>
    [JsonPropertyName("overview")]
    public string Overview { get; set; } = string.Empty;

    /// <summary>Gets or sets the in-cinema date.</summary>
    [JsonPropertyName("inCinemas")]
    public DateTime? InCinemas { get; set; }

    /// <summary>Gets or sets the physical / digital release date.</summary>
    [JsonPropertyName("physicalRelease")]
    public DateTime? PhysicalRelease { get; set; }

    /// <summary>Gets or sets the digital release date.</summary>
    [JsonPropertyName("digitalRelease")]
    public DateTime? DigitalRelease { get; set; }

    /// <summary>Gets or sets the runtime in minutes.</summary>
    [JsonPropertyName("runtime")]
    public int Runtime { get; set; }

    /// <summary>Gets or sets the TMDB ID.</summary>
    [JsonPropertyName("tmdbId")]
    public int TmdbId { get; set; }

    /// <summary>Gets or sets the IMDB ID.</summary>
    [JsonPropertyName("imdbId")]
    public string? ImdbId { get; set; }

    /// <summary>Gets or sets the studio.</summary>
    [JsonPropertyName("studio")]
    public string? Studio { get; set; }

    /// <summary>Gets or sets the genres.</summary>
    [JsonPropertyName("genres")]
    public List<string> Genres { get; set; } = new();

    /// <summary>Gets or sets the ratings.</summary>
    [JsonPropertyName("ratings")]
    public RadarrRatings? Ratings { get; set; }

    /// <summary>Gets or sets the images (poster, fanart, etc.).</summary>
    [JsonPropertyName("images")]
    public List<RadarrImage> Images { get; set; } = new();

    /// <summary>Gets or sets a value indicating whether the movie has been downloaded.</summary>
    [JsonPropertyName("hasFile")]
    public bool HasFile { get; set; }

    /// <summary>Gets or sets a value indicating whether the movie is monitored.</summary>
    [JsonPropertyName("monitored")]
    public bool Monitored { get; set; }

    /// <summary>Gets or sets the status (e.g. announced, inCinemas, released).</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Represents rating information from Radarr.
/// </summary>
public class RadarrRatings
{
    /// <summary>Gets or sets the IMDB votes.</summary>
    [JsonPropertyName("imdb")]
    public RadarrRatingValue? Imdb { get; set; }

    /// <summary>Gets or sets the TMDB votes.</summary>
    [JsonPropertyName("tmdb")]
    public RadarrRatingValue? Tmdb { get; set; }
}

/// <summary>
/// Represents a single rating value.
/// </summary>
public class RadarrRatingValue
{
    /// <summary>Gets or sets votes count.</summary>
    [JsonPropertyName("votes")]
    public int Votes { get; set; }

    /// <summary>Gets or sets the rating value.</summary>
    [JsonPropertyName("value")]
    public double Value { get; set; }
}

/// <summary>
/// Represents an image (poster, fanart, banner) from Radarr.
/// </summary>
public class RadarrImage
{
    /// <summary>Gets or sets the cover type (poster, fanart, banner).</summary>
    [JsonPropertyName("coverType")]
    public string CoverType { get; set; } = string.Empty;

    /// <summary>Gets or sets the remote URL of the image.</summary>
    [JsonPropertyName("remoteUrl")]
    public string? RemoteUrl { get; set; }

    /// <summary>Gets or sets the local URL served by Radarr.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

/// <summary>
/// A simplified DTO returned by the plugin's own API endpoint.
/// </summary>
public class UpcomingMovieDto
{
    /// <summary>Gets or sets the Radarr ID.</summary>
    public int RadarrId { get; set; }

    /// <summary>Gets or sets the title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the year.</summary>
    public int Year { get; set; }

    /// <summary>Gets or sets the overview.</summary>
    public string Overview { get; set; } = string.Empty;

    /// <summary>Gets or sets the in-cinema date.</summary>
    public DateTime? InCinemas { get; set; }

    /// <summary>Gets or sets the physical/digital release date.</summary>
    public DateTime? ReleaseDate { get; set; }

    /// <summary>Gets or sets the runtime in minutes.</summary>
    public int Runtime { get; set; }

    /// <summary>Gets or sets the genres.</summary>
    public List<string> Genres { get; set; } = new();

    /// <summary>Gets or sets the TMDB ID.</summary>
    public int TmdbId { get; set; }

    /// <summary>Gets or sets the IMDB ID.</summary>
    public string? ImdbId { get; set; }

    /// <summary>Gets or sets the poster image URL.</summary>
    public string? PosterUrl { get; set; }

    /// <summary>Gets or sets the fanart image URL.</summary>
    public string? FanartUrl { get; set; }

    /// <summary>Gets or sets the community rating.</summary>
    public double? CommunityRating { get; set; }

    /// <summary>Gets or sets the Radarr status string.</summary>
    public string Status { get; set; } = string.Empty;
}
