using Windows.Media.Control;

namespace PodPilot.Core.Media;

/// <summary>
/// Pauses and resumes whatever is playing (Spotify, browsers, media players) through the
/// Windows media session API, the same one the volume flyout uses.
/// </summary>
public sealed class MediaController
{
    GlobalSystemMediaTransportControlsSessionManager? _manager;

    async Task<GlobalSystemMediaTransportControlsSession?> GetSessionAsync()
    {
        try
        {
            _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            return _manager.GetCurrentSession();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<bool> IsPlayingAsync()
    {
        var session = await GetSessionAsync();
        return session?.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
    }

    public async Task<bool> PauseAsync()
    {
        var session = await GetSessionAsync();
        return session is not null && await session.TryPauseAsync();
    }

    public async Task<bool> PlayAsync()
    {
        var session = await GetSessionAsync();
        return session is not null && await session.TryPlayAsync();
    }
}
