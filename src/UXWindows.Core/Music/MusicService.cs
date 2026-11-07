using Windows.Media.Control;
using Windows.Storage.Streams;

namespace UXWindows.Core.Music;

public sealed record NowPlaying(string Title, string Artist, bool IsPlaying, byte[]? Artwork, string AppId);

/// <summary>
/// Musique en cours via les contrôles média de Windows (ceux du volume / clavier multimédia).
/// Apple Music est choisi en priorité, sinon l'app qui joue en ce moment.
/// </summary>
public sealed class MusicService : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private string? _artworkKey;
    private byte[]? _artwork;

    /// <summary>Appelé (sur un thread quelconque) quand le morceau ou l'état de lecture change.</summary>
    public event Action<NowPlaying?>? Changed;

    public async Task StartAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.SessionsChanged += (_, _) => _ = RefreshSessionAsync();
        _manager.CurrentSessionChanged += (_, _) => _ = RefreshSessionAsync();
        await RefreshSessionAsync();
    }

    public async Task PlayPauseAsync()
    {
        if (_session is not null) await _session.TryTogglePlayPauseAsync();
    }

    public async Task NextAsync()
    {
        if (_session is not null) await _session.TrySkipNextAsync();
    }

    public async Task PreviousAsync()
    {
        if (_session is not null) await _session.TrySkipPreviousAsync();
    }

    private async Task RefreshSessionAsync()
    {
        if (_manager is null) return;

        var sessions = _manager.GetSessions();
        var chosen =
            sessions.FirstOrDefault(s => s.SourceAppUserModelId.Contains("AppleMusic", StringComparison.OrdinalIgnoreCase)) ??
            _manager.GetCurrentSession();

        if (!ReferenceEquals(chosen, _session))
        {
            if (_session is not null)
            {
                _session.MediaPropertiesChanged -= OnSessionChanged;
                _session.PlaybackInfoChanged -= OnSessionChanged;
            }
            _session = chosen;
            if (_session is not null)
            {
                _session.MediaPropertiesChanged += OnSessionChanged;
                _session.PlaybackInfoChanged += OnSessionChanged;
            }
        }
        await PublishAsync();
    }

    private void OnSessionChanged(GlobalSystemMediaTransportControlsSession sender, object args) => _ = PublishAsync();

    private async Task PublishAsync()
    {
        var session = _session;
        if (session is null)
        {
            Changed?.Invoke(null);
            return;
        }

        try
        {
            var props = await session.TryGetMediaPropertiesAsync();
            var playback = session.GetPlaybackInfo();
            bool playing = playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            // La pochette n'est relue que quand le morceau change.
            string key = $"{props.Artist}|{props.AlbumTitle}|{props.Title}";
            if (key != _artworkKey)
            {
                _artworkKey = key;
                _artwork = props.Thumbnail is null ? null : await ReadAllAsync(props.Thumbnail);
            }

            Changed?.Invoke(new NowPlaying(props.Title ?? "", props.Artist ?? "", playing, _artwork, session.SourceAppUserModelId));
        }
        catch (Exception)
        {
            // L'app de musique vient de se fermer, par exemple.
            Changed?.Invoke(null);
        }
    }

    private static async Task<byte[]?> ReadAllAsync(IRandomAccessStreamReference reference)
    {
        try
        {
            using var stream = await reference.OpenReadAsync();
            using var reader = new DataReader(stream);
            uint size = (uint)stream.Size;
            await reader.LoadAsync(size);
            var bytes = new byte[size];
            reader.ReadBytes(bytes);
            return bytes;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnSessionChanged;
            _session.PlaybackInfoChanged -= OnSessionChanged;
        }
    }
}
