using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.RadarrUpcoming.Configuration;

/// <summary>
/// Plugin configuration for RadarrUpcoming.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the Radarr base URL (e.g. http://localhost:7878).
    /// </summary>
    public string RadarrUrl { get; set; } = "http://localhost:7878";

    /// <summary>
    /// Gets or sets the Radarr API key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum number of movies to show in the Upcoming section.
    /// </summary>
    public int MaxMovies { get; set; } = 20;

    /// <summary>
    /// Gets or sets a value indicating whether to show only movies with a known release date.
    /// </summary>
    public bool OnlyWithReleaseDate { get; set; } = false;
}
