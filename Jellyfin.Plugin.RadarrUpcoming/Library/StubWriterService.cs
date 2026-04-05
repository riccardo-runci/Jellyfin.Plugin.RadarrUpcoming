using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.RadarrUpcoming.Api;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RadarrUpcoming.Library;

/// <summary>
/// Writes one stub folder per Radarr wanted movie into the configured
/// <see cref="Configuration.PluginConfiguration.StubLibraryPath"/>.
/// Each folder contains a <c>.strm</c> file (so Jellyfin treats it as a video)
/// and an <c>.nfo</c> file with metadata so Jellyfin displays the correct
/// title, year, overview and artwork without an internet metadata fetch.
/// </summary>
public class StubWriterService
{
    private readonly ILogger<StubWriterService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StubWriterService"/> class.
    /// </summary>
    public StubWriterService(ILogger<StubWriterService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Synchronises the stub folder: creates stubs for new movies, removes
    /// stubs for movies that are no longer in the wanted list.
    /// </summary>
    public Task SyncStubsAsync(
        IReadOnlyList<UpcomingMovieDto> movies,
        string stubLibraryPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stubLibraryPath))
        {
            _logger.LogDebug("RadarrUpcoming: StubLibraryPath is empty, skipping stub sync.");
            return Task.CompletedTask;
        }

        try
        {
            Directory.CreateDirectory(stubLibraryPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RadarrUpcoming: Cannot create stub library path '{Path}'.", stubLibraryPath);
            return Task.CompletedTask;
        }

        // Build the expected set of folder names for the current wanted list
        var expectedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var movie in movies)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var folderName = SanitiseFolderName($"{movie.Title} ({movie.Year})");
            expectedFolders.Add(folderName);

            var folderPath = Path.Combine(stubLibraryPath, folderName);
            Directory.CreateDirectory(folderPath);

            var baseName = Path.Combine(folderPath, folderName);
            WriteStrmFile(baseName + ".strm", movie);
            WriteNfoFile(baseName + ".nfo", movie);
        }

        // Remove stubs for movies no longer in the wanted list
        RemoveStaleStubs(stubLibraryPath, expectedFolders);

        _logger.LogInformation(
            "RadarrUpcoming: Stub sync complete. {Count} stubs in '{Path}'.",
            movies.Count,
            stubLibraryPath);

        return Task.CompletedTask;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void WriteStrmFile(string path, UpcomingMovieDto movie)
    {
        // Point to the TMDB page so that if a user clicks Play they land on
        // the movie's TMDB page rather than getting a cryptic playback error.
        var content = movie.TmdbId > 0
            ? $"https://www.themoviedb.org/movie/{movie.TmdbId}"
            : $"https://www.themoviedb.org/search?query={Uri.EscapeDataString(movie.Title)}";
        WriteIfChanged(path, content);
    }

    private void WriteNfoFile(string path, UpcomingMovieDto movie)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>");
        sb.AppendLine("<movie>");
        sb.AppendLine($"  <title>{XmlEscape(movie.Title)}</title>");
        sb.AppendLine($"  <originaltitle>{XmlEscape(movie.Title)}</originaltitle>");
        sb.AppendLine($"  <year>{movie.Year}</year>");

        if (!string.IsNullOrWhiteSpace(movie.Overview))
        {
            sb.AppendLine($"  <plot>{XmlEscape(movie.Overview)}</plot>");
            sb.AppendLine($"  <outline>{XmlEscape(movie.Overview)}</outline>");
        }

        if (movie.TmdbId > 0)
        {
            sb.AppendLine($"  <tmdbid>{movie.TmdbId}</tmdbid>");
            sb.AppendLine($"  <uniqueid type=\"tmdb\" default=\"true\">{movie.TmdbId}</uniqueid>");
        }

        if (!string.IsNullOrWhiteSpace(movie.ImdbId))
        {
            sb.AppendLine($"  <imdbid>{XmlEscape(movie.ImdbId)}</imdbid>");
            sb.AppendLine($"  <uniqueid type=\"imdb\">{XmlEscape(movie.ImdbId)}</uniqueid>");
        }

        if (movie.CommunityRating.HasValue && movie.CommunityRating.Value > 0)
        {
            sb.AppendLine($"  <rating>{movie.CommunityRating.Value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}</rating>");
        }

        if (movie.Runtime > 0)
        {
            sb.AppendLine($"  <runtime>{movie.Runtime}</runtime>");
        }

        foreach (var genre in movie.Genres ?? Enumerable.Empty<string>())
        {
            sb.AppendLine($"  <genre>{XmlEscape(genre)}</genre>");
        }

        // Embed the release date so Jellyfin shows it in the UI
        var releaseDate = movie.ReleaseDate ?? movie.InCinemas;
        if (releaseDate.HasValue)
        {
            sb.AppendLine($"  <releasedate>{releaseDate.Value:yyyy-MM-dd}</releasedate>");
            sb.AppendLine($"  <premiered>{releaseDate.Value:yyyy-MM-dd}</premiered>");
        }

        // Poster — Jellyfin will download and cache this image
        if (!string.IsNullOrWhiteSpace(movie.PosterUrl))
        {
            sb.AppendLine($"  <thumb aspect=\"poster\">{XmlEscape(movie.PosterUrl)}</thumb>");
        }

        // Fanart
        if (!string.IsNullOrWhiteSpace(movie.FanartUrl))
        {
            sb.AppendLine("  <fanart>");
            sb.AppendLine($"    <thumb>{XmlEscape(movie.FanartUrl)}</thumb>");
            sb.AppendLine("  </fanart>");
        }

        // Tag so users can filter these stubs in Jellyfin
        sb.AppendLine("  <tag>Radarr Upcoming</tag>");

        sb.AppendLine("</movie>");

        WriteIfChanged(path, sb.ToString());
    }

    private void RemoveStaleStubs(string stubLibraryPath, HashSet<string> expectedFolders)
    {
        try
        {
            foreach (var dir in Directory.GetDirectories(stubLibraryPath))
            {
                var name = Path.GetFileName(dir);
                if (!expectedFolders.Contains(name))
                {
                    _logger.LogInformation("RadarrUpcoming: Removing stale stub folder '{Name}'.", name);
                    try { Directory.Delete(dir, recursive: true); }
                    catch (Exception ex) { _logger.LogWarning(ex, "RadarrUpcoming: Could not delete '{Dir}'.", dir); }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RadarrUpcoming: Error while cleaning stale stubs.");
        }
    }

    private static void WriteIfChanged(string path, string content)
    {
        if (File.Exists(path))
        {
            var existing = File.ReadAllText(path, Encoding.UTF8);
            if (existing == content)
            {
                return; // No change — don't touch the file (avoids unnecessary Jellyfin re-scans)
            }
        }

        File.WriteAllText(path, content, Encoding.UTF8);
    }

    private static string SanitiseFolderName(string name)
    {
        // Replace characters invalid on Windows/Linux/macOS filesystems
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        }

        // Collapse multiple spaces/underscores and trim
        var result = Regex.Replace(sb.ToString(), @"[ _]{2,}", " ").Trim();
        return result.Length > 0 ? result : "_";
    }

    private static string XmlEscape(string value)
        => value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
}
