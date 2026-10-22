using System.Threading.Tasks;
using Earlo.Core.Media;
using Earlo.Core.Tracking;

namespace Earlo.App.Services;

/// <summary>Pauses playback when an AirPod comes out and resumes it when it goes back in.</summary>
public sealed class EarDetectionService(AppSettings settings, MediaController media)
{
    bool _pausedByUs;

    public async Task OnEarStatusChangedAsync(EarStatusChangedEventArgs e, bool deviceConnected)
    {
        if (!settings.EarDetection || (settings.RequireConnected && !deviceConnected))
            return;

        var now = e.Snapshot;
        bool oneMode = settings.PauseMode == 0;
        bool wasWearing = oneMode ? e.WasLeftInEar && e.WasRightInEar : e.WasLeftInEar || e.WasRightInEar;
        bool isWearing = oneMode ? now.LeftInEar && now.RightInEar : now.LeftInEar || now.RightInEar;

        if (wasWearing && !isWearing)
        {
            if (await media.IsPlayingAsync() && await media.PauseAsync())
                _pausedByUs = true;
        }
        else if (!wasWearing && isWearing && _pausedByUs)
        {
            _pausedByUs = false;
            if (settings.ResumeOnInsert)
                await media.PlayAsync();
        }
    }
}
