using System.Globalization;
using System.Text;
using MacroDeck.Sdk.MusicPlayer;

namespace MacroDeck.SampleMusicPlayerPlugin.Player;

/// <summary>A fixed catalogue of made-up tracks and playlists, so the sample needs no music service.</summary>
internal static class MusicLibrary
{
	internal static IReadOnlyList<LibraryTrack> Tracks { get; } =
	[
		new("track-solar-drift", "Solar Drift", "Nova Fields", "Orbital", TimeSpan.FromSeconds(214), "#F5A623"),
		new("track-night-transit", "Night Transit", "Nova Fields", "Orbital", TimeSpan.FromSeconds(187), "#4A90D9"),
		new("track-paper-lanterns", "Paper Lanterns", "Halcyon Row", "Slow Light", TimeSpan.FromSeconds(243), "#7ED321"),
		new("track-quiet-machines", "Quiet Machines", "Halcyon Row", "Slow Light", TimeSpan.FromSeconds(198), "#BD10E0"),
		new("track-harbour-lights", "Harbour Lights", "Ash & Ivory", "Tide", TimeSpan.FromSeconds(226), "#50E3C2")
	];

	internal static IReadOnlyList<LibraryPlaylist> Playlists { get; } =
	[
		new("playlist-focus", "Focus", ["track-solar-drift", "track-quiet-machines", "track-harbour-lights"]),
		new("playlist-evening", "Evening", ["track-night-transit", "track-paper-lanterns"])
	];

	internal static LibraryTrack? FindTrack(string id)
		=> Tracks.FirstOrDefault(track => string.Equals(track.Id, id, StringComparison.Ordinal));

	internal static LibraryPlaylist? FindPlaylist(string id)
		=> Playlists.FirstOrDefault(playlist => string.Equals(playlist.Id, id, StringComparison.Ordinal));

	/// <summary>Resolves a playlist to its tracks, skipping ids the library no longer knows.</summary>
	internal static IReadOnlyList<LibraryTrack> TracksOf(LibraryPlaylist playlist)
		=> [.. playlist.TrackIds.Select(FindTrack).OfType<LibraryTrack>()];

	internal static IReadOnlyList<MusicPlayerCatalogItem> CatalogItems(MusicPlayerCatalogItemKind kind, string? filter)
	{
		var items = kind switch
		{
			MusicPlayerCatalogItemKind.Playlist => Playlists.Select(playlist => new MusicPlayerCatalogItem(
				playlist.Id,
				playlist.Title,
				MusicPlayerCatalogItemKind.Playlist,
				Subtitle: $"{playlist.TrackIds.Count} tracks")),
			_ => Tracks.Select(track => new MusicPlayerCatalogItem(
				track.Id,
				track.Title,
				MusicPlayerCatalogItemKind.Track,
				Subtitle: track.Artist,
				ArtworkId: track.Id,
				Duration: track.Duration))
		};

		if (!string.IsNullOrWhiteSpace(filter))
		{
			items = items.Where(item => item.Title.Contains(filter, StringComparison.OrdinalIgnoreCase));
		}

		return [.. items];
	}

	/// <summary>
	/// Cover art, drawn here rather than shipped as files. A real integration downloads the artwork its
	/// service reports; what matters for the contract is that the bytes and the MIME type match the
	/// <c>ArtworkId</c> the state reported.
	/// </summary>
	internal static MusicPlayerArtwork? Artwork(string artworkId)
	{
		if (FindTrack(artworkId) is not { } track)
		{
			return null;
		}

		var initials = string.Concat(track.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries)
			.Take(2)
			.Select(word => char.ToUpper(word[0], CultureInfo.InvariantCulture)));

		var svg = $"""
			<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">
				<rect width="64" height="64" rx="8" fill="{track.Color}" />
				<text x="32" y="40" text-anchor="middle" font-family="sans-serif" font-size="22" fill="#FFFFFF">{initials}</text>
			</svg>
			""";

		return new MusicPlayerArtwork(Encoding.UTF8.GetBytes(svg), "image/svg+xml");
	}
}

internal sealed record LibraryTrack(
	string Id,
	string Title,
	string Artist,
	string Album,
	TimeSpan Duration,
	string Color);

internal sealed record LibraryPlaylist(string Id, string Title, IReadOnlyList<string> TrackIds);
