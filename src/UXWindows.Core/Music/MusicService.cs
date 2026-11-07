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
    private int _version;

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
                _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                _session.PlaybackInfoChanged -= OnSessionChanged;
            }
            _session = chosen;
            if (_session is not null)
            {
                _session.MediaPropertiesChanged += OnMediaPropertiesChanged;
                _session.PlaybackInfoChanged += OnSessionChanged;
            }
        }
        await PublishAsync();
    }

    private void OnSessionChanged(GlobalSystemMediaTransportControlsSession sender, object args) =>
        _ = PublishAsync(reloadArtwork: false);

    /// <summary>
    /// Changement de morceau. Apple Music envoie souvent le titre d'abord et la pochette un peu après :
    /// on relit donc la pochette tout de suite, puis encore une fois 1,5 s plus tard.
    /// </summary>
    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, object args)
    {
        _ = PublishAsync(reloadArtwork: true);
        _ = Task.Delay(1500).ContinueWith(_ => PublishAsync(reloadArtwork: true));
    }

    private async Task PublishAsync(bool reloadArtwork = true)
    {
        // Les lectures se chevauchent : seule la plus récente a le droit d'afficher son résultat.
        int version = Interlocked.Increment(ref _version);

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

            string key = $"{props.Artist}|{props.AlbumTitle}|{props.Title}";
            byte[]? artwork = _artwork;
            if (reloadArtwork || key != _artworkKey)
            {
                artwork = props.Thumbnail is null ? null : await ReadAllAsync(props.Thumbnail);
            }

            if (version != Volatile.Read(ref _version)) return; // une lecture plus récente a eu lieu
            _artworkKey = key;
            _artwork = artwork;

            Changed?.Invoke(new NowPlaying(props.Title ?? "", props.Artist ?? "", playing, _artwork, session.SourceAppUserModelId));
        }
        catch (Exception)
        {
            // L'app de musique vient de se fermer, par exemple.
            if (version == Volatile.Read(ref _version)) Changed?.Invoke(null);
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
            _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged -= OnSessionChanged;
        }
    }
}
