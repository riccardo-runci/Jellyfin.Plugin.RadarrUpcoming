using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.RadarrUpcoming.Api;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RadarrUpcoming.Library;

/// <summary>
/// Maintains a Jellyfin collection called "Radarr Upcoming" that mirrors the
/// Radarr Wanted list by matching movies already present in the Jellyfin library.
/// </summary>
public class CollectionSyncService
{
    /// <summary>Name of the managed collection as it appears in Jellyfin.</summary>
    public const string CollectionName = "Radarr Upcoming";

    private readonly ILibraryManager _libraryManager;
    private readonly ICollectionManager _collectionManager;
    private readonly ILogger<CollectionSyncService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CollectionSyncService"/> class.
    /// </summary>
    public CollectionSyncService(
        ILibraryManager libraryManager,
        ICollectionManager collectionManager,
        ILogger<CollectionSyncService> logger)
    {
        _libraryManager = libraryManager;
        _collectionManager = collectionManager;
        _logger = logger;
    }

    /// <summary>
    /// Synchronises the "Radarr Upcoming" collection so it contains exactly the
    /// Jellyfin library movies whose TmdbId or ImdbId appears in
    /// <paramref name="wantedMovies"/>.
    /// </summary>
    public async Task SyncAsync(
        IReadOnlyList<UpcomingMovieDto> wantedMovies,
        CancellationToken cancellationToken)
    {
        // ── 1. Find Jellyfin library items that match the wanted list ────────
        var matchedIds = FindMatchingLibraryItems(wantedMovies);

        if (matchedIds.Count == 0)
        {
            _logger.LogInformation(
                "RadarrUpcoming: None of the {Count} wanted movies exist in the Jellyfin library yet. " +
                "The collection will be empty until matching movies are added.",
                wantedMovies.Count);
        }
        else
        {
            _logger.LogInformation(
                "RadarrUpcoming: {Matched} of {Total} wanted movies found in the Jellyfin library.",
                matchedIds.Count,
                wantedMovies.Count);
        }

        // ── 2. Find or create the "Radarr Upcoming" collection ──────────────
        var collection = await GetOrCreateCollectionAsync(cancellationToken).ConfigureAwait(false);

        // ── 3. Compute diff: what to add and what to remove ─────────────────
        var currentMemberIds = GetCurrentMemberIds(collection);

        var toAdd = matchedIds.Except(currentMemberIds).ToList();
        var toRemove = currentMemberIds.Except(matchedIds).ToList();

        if (toAdd.Count > 0)
        {
            _logger.LogInformation("RadarrUpcoming: Adding {Count} movies to collection.", toAdd.Count);
            await _collectionManager.AddToCollectionAsync(collection.Id, toAdd).ConfigureAwait(false);
        }

        if (toRemove.Count > 0)
        {
            _logger.LogInformation("RadarrUpcoming: Removing {Count} stale movies from collection.", toRemove.Count);
            await _collectionManager.RemoveFromCollectionAsync(collection.Id, toRemove).ConfigureAwait(false);
        }

        if (toAdd.Count == 0 && toRemove.Count == 0)
        {
            _logger.LogInformation("RadarrUpcoming: Collection is already up to date.");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private List<Guid> FindMatchingLibraryItems(IReadOnlyList<UpcomingMovieDto> wantedMovies)
    {
        // Build lookup sets for fast matching
        var tmdbIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var imdbIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var m in wantedMovies)
        {
            if (m.TmdbId > 0)
            {
                tmdbIds.Add(m.TmdbId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            if (!string.IsNullOrWhiteSpace(m.ImdbId))
            {
                imdbIds.Add(m.ImdbId);
            }
        }

        // Query all Movie items in the library
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { Jellyfin.Data.Enums.BaseItemKind.Movie },
            IsVirtualItem = false,
            Recursive = true
        };

        var allMovies = _libraryManager.GetItemList(query);

        var matched = new List<Guid>();
        foreach (var item in allMovies)
        {
            var tmdb = item.GetProviderId(MetadataProvider.Tmdb);
            var imdb = item.GetProviderId(MetadataProvider.Imdb);

            if ((!string.IsNullOrEmpty(tmdb) && tmdbIds.Contains(tmdb))
                || (!string.IsNullOrEmpty(imdb) && imdbIds.Contains(imdb)))
            {
                matched.Add(item.Id);
            }
        }

        return matched;
    }

    private async Task<BoxSet> GetOrCreateCollectionAsync(CancellationToken cancellationToken)
    {
        // Look for an existing collection with our name
        var existing = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { Jellyfin.Data.Enums.BaseItemKind.BoxSet },
            Name = CollectionName,
            Recursive = true
        });

        if (existing.Count > 0 && existing[0] is BoxSet boxSet)
        {
            _logger.LogDebug("RadarrUpcoming: Found existing collection '{Name}' (id={Id}).", CollectionName, boxSet.Id);
            return boxSet;
        }

        _logger.LogInformation("RadarrUpcoming: Creating new collection '{Name}'.", CollectionName);

        var created = await _collectionManager.CreateCollectionAsync(new CollectionCreationOptions
        {
            Name = CollectionName,
            IsLocked = false
        }).ConfigureAwait(false);

        return created;
    }

    private static List<Guid> GetCurrentMemberIds(BoxSet collection)
    {
        // BoxSet.GetLinkedChildren() returns the actual member items
        return collection.GetLinkedChildren()
                         .Select(i => i.Id)
                         .ToList();
    }
}
