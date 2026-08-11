using System.Text.Json;
using System.Windows.Threading;
using GrassiBoard.Models;
using GrassiBoard.Services;
using GrassiBoard.ViewModels;

namespace GrassiBoard.Services.Remote;

internal sealed class RemoteCommandDispatcher
{
    private readonly MainViewModel _viewModel;
    private readonly Dispatcher _dispatcher;

    public RemoteCommandDispatcher(MainViewModel viewModel, Dispatcher dispatcher)
    {
        _viewModel = viewModel;
        _dispatcher = dispatcher;
    }

    public Task<RemoteCommandResult> ExecuteAsync(RemoteIncomingEnvelope envelope)
    {
        if (_dispatcher.CheckAccess()) return ExecuteOnUiAsync(envelope);
        return _dispatcher.InvokeAsync(() => ExecuteOnUiAsync(envelope), DispatcherPriority.Normal).Task.Unwrap();
    }

    public Task<RemoteStateSnapshot> CreateSnapshotAsync(long revision)
    {
        if (_dispatcher.CheckAccess()) return Task.FromResult(CreateSnapshotOnUi(revision));
        return _dispatcher.InvokeAsync(() => CreateSnapshotOnUi(revision), DispatcherPriority.Background).Task;
    }

    private async Task<RemoteCommandResult> ExecuteOnUiAsync(RemoteIncomingEnvelope envelope)
    {
        try
        {
            switch (envelope.Type)
            {
                case "engine.start":
                    if (!_viewModel.NativeReady) return RemoteCommandResult.Fail("engine_unavailable", "The audio engine is unavailable.");
                    await _viewModel.RemoteStartEngineAsync();
                    return RemoteCommandResult.Ok();

                case "engine.stop":
                    if (!_viewModel.NativeReady) return RemoteCommandResult.Fail("engine_unavailable", "The audio engine is unavailable.");
                    await _viewModel.RemoteStopEngineAsync();
                    return RemoteCommandResult.Ok();

                case "engine.stopAll":
                    if (!_viewModel.NativeReady) return RemoteCommandResult.Fail("engine_unavailable", "The audio engine is unavailable.");
                    await _viewModel.RemoteStopAllAsync();
                    return RemoteCommandResult.Ok();

                case "mic.mute.set":
                    if (!TryBoolean(envelope.Payload, "muted", out bool muted)) return Invalid("muted");
                    _viewModel.MicrophoneMuted = muted;
                    return RemoteCommandResult.Ok();

                case "voice.fx.set":
                    if (!TryBoolean(envelope.Payload, "enabled", out bool enabled)) return Invalid("enabled");
                    _viewModel.VoiceFxEnabled = enabled;
                    return RemoteCommandResult.Ok();

                case "voice.pitch.set":
                    if (!TryNumber(envelope.Payload, "value", -12.0, 12.0, out double pitch)) return Range("value", -12, 12);
                    _viewModel.Pitch = pitch;
                    return RemoteCommandResult.Ok();

                case "voice.finePitch.set":
                    if (!TryNumber(envelope.Payload, "value", -100.0, 100.0, out double finePitch)) return Range("value", -100, 100);
                    _viewModel.FinePitch = finePitch;
                    return RemoteCommandResult.Ok();

                case "voice.formant.set":
                    if (!TryNumber(envelope.Payload, "value", -12.0, 12.0, out double formant)) return Range("value", -12, 12);
                    _viewModel.Formant = formant;
                    return RemoteCommandResult.Ok();

                case "voice.preserveCharacter.set":
                    if (!TryBoolean(envelope.Payload, "enabled", out bool preserve)) return Invalid("enabled");
                    _viewModel.PreserveVocalCharacter = preserve;
                    return RemoteCommandResult.Ok();

                case "voice.reset":
                    _viewModel.RemoteResetVoice();
                    return RemoteCommandResult.Ok();

                case "preset.apply":
                    if (!TryGuid(envelope.Payload, "presetId", out Guid presetId)) return Invalid("presetId");
                    return await _viewModel.RemoteApplyUserPresetAsync(presetId)
                        ? RemoteCommandResult.Ok()
                        : RemoteCommandResult.Fail("preset_not_found", "The requested preset is not in the active profile.");

                case "pad.play":
                    if (!TryGuid(envelope.Payload, "padId", out Guid playPadId)) return Invalid("padId");
                    return await _viewModel.RemotePlayPadAsync(playPadId)
                        ? RemoteCommandResult.Ok()
                        : RemoteCommandResult.Fail("pad_not_found", "The requested Sound Pad is not in the active profile.");

                case "pad.stop":
                    if (!TryGuid(envelope.Payload, "padId", out Guid stopPadId)) return Invalid("padId");
                    return _viewModel.RemoteStopPad(stopPadId)
                        ? RemoteCommandResult.Ok()
                        : RemoteCommandResult.Fail("pad_not_found", "The requested Sound Pad is not in the active profile.");

                case "mixer.gain.set":
                    if (!TryString(envelope.Payload, "channel", out string channel)) return Invalid("channel");
                    return SetMixerGain(channel, envelope.Payload);

                case "media.playPause":
                    if (!_viewModel.HasMedia) return RemoteCommandResult.Fail("media_missing", "No Media file is loaded on Windows.");
                    _viewModel.RemoteMediaPlayPause();
                    return RemoteCommandResult.Ok();

                case "media.stop":
                    _viewModel.RemoteMediaStop();
                    return RemoteCommandResult.Ok();

                case "media.skip":
                    if (!TryNumber(envelope.Payload, "seconds", -30.0, 30.0, out double skip) || Math.Abs(skip) < 0.01)
                        return RemoteCommandResult.Fail("invalid_range", "seconds must be between -30 and 30.");
                    if (!_viewModel.HasMedia) return RemoteCommandResult.Fail("media_missing", "No Media file is loaded on Windows.");
                    _viewModel.RemoteMediaSkip(skip);
                    return RemoteCommandResult.Ok();

                case "media.seek":
                    if (!TryNumber(envelope.Payload, "seconds", 0.0, Math.Max(0.01, _viewModel.MediaDuration), out double seek))
                        return RemoteCommandResult.Fail("invalid_range", "seconds is outside the loaded Media duration.");
                    if (!_viewModel.HasMedia) return RemoteCommandResult.Fail("media_missing", "No Media file is loaded on Windows.");
                    _viewModel.RemoteMediaSeek(seek);
                    return RemoteCommandResult.Ok();

                case "media.volume.set":
                    if (!TryNumber(envelope.Payload, "value", 0.0, 1.5, out double volume)) return Range("value", 0, 1.5);
                    _viewModel.MediaVolume = volume;
                    return RemoteCommandResult.Ok();

                case "media.monitor.set":
                    if (!TryBoolean(envelope.Payload, "enabled", out bool monitor)) return Invalid("enabled");
                    _viewModel.MediaMonitorEnabled = monitor;
                    return RemoteCommandResult.Ok();

                case "media.send.set":
                    if (!TryBoolean(envelope.Payload, "enabled", out bool send)) return Invalid("enabled");
                    _viewModel.MediaSendEnabled = send;
                    return RemoteCommandResult.Ok();

                default:
                    return RemoteCommandResult.Fail("unknown_command", $"Unsupported command: {envelope.Type}");
            }
        }
        catch (Exception exception)
        {
            CrashReporter.Report(exception, $"Remote command {envelope.Type}", false);
            return RemoteCommandResult.Fail("command_failed", "GrassiBoard could not apply that command.");
        }
    }

    private RemoteCommandResult SetMixerGain(string channel, JsonElement payload)
    {
        switch (channel)
        {
            case "mic":
                if (!TryNumber(payload, "value", -24.0, 24.0, out double mic)) return Range("value", -24, 24);
                _viewModel.MicGain = mic;
                break;
            case "soundboard":
                if (!TryNumber(payload, "value", -24.0, 24.0, out double soundboard)) return Range("value", -24, 24);
                _viewModel.SoundboardGain = soundboard;
                break;
            case "master":
                if (!TryNumber(payload, "value", -24.0, 12.0, out double master)) return Range("value", -24, 12);
                _viewModel.MasterGain = master;
                break;
            default:
                return RemoteCommandResult.Fail("invalid_channel", "channel must be mic, soundboard, or master.");
        }
        return RemoteCommandResult.Ok();
    }

    private RemoteStateSnapshot CreateSnapshotOnUi(long revision)
    {
        IReadOnlyList<RemotePadSnapshot> pads = _viewModel.Pads.Select(pad => new RemotePadSnapshot(
            pad.Id,
            pad.Title,
            pad.StateLabel,
            pad.IsLoaded && !pad.HasError,
            pad.IsPlaying,
            pad.Loop,
            pad.HasError)).ToArray();

        IReadOnlyList<RemotePresetSnapshot> presets = _viewModel.UserPresets
            .Select(preset => new RemotePresetSnapshot(preset.Id, preset.Name))
            .ToArray();

        return new RemoteStateSnapshot(
            revision,
            _viewModel.ActiveProfileName,
            new RemoteEngineSnapshot(
                _viewModel.NativeReady,
                _viewModel.IsRunning,
                _viewModel.IsBusy,
                _viewModel.EngineStateLabel,
                _viewModel.EngineStatus),
            _viewModel.MicrophoneMuted,
            new RemoteVoiceSnapshot(
                _viewModel.VoiceFxEnabled,
                _viewModel.Pitch,
                _viewModel.FinePitch,
                _viewModel.Formant,
                _viewModel.PreserveVocalCharacter),
            new RemoteMixerSnapshot(_viewModel.MicGain, _viewModel.SoundboardGain, _viewModel.MasterGain),
            new RemoteMediaSnapshot(
                _viewModel.HasMedia,
                _viewModel.MediaFileName,
                _viewModel.MediaPlaying,
                _viewModel.MediaPosition,
                _viewModel.MediaDuration,
                _viewModel.MediaVolume,
                _viewModel.MediaMonitorEnabled,
                _viewModel.MediaSendEnabled,
                _viewModel.MediaHasError),
            new RemoteMeterSnapshot(
                _viewModel.MicrophoneMeter,
                _viewModel.SoundboardMeter,
                _viewModel.MasterMeter,
                _viewModel.MicrophoneDb,
                _viewModel.SoundboardDb,
                _viewModel.MasterDb),
            pads,
            presets);
    }

    private static bool TryGuid(JsonElement payload, string name, out Guid value)
    {
        value = Guid.Empty;
        return payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty(name, out JsonElement element) &&
               element.ValueKind == JsonValueKind.String && Guid.TryParse(element.GetString(), out value) && value != Guid.Empty;
    }

    private static bool TryBoolean(JsonElement payload, string name, out bool value)
    {
        value = false;
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty(name, out JsonElement element)) return false;
        if (element.ValueKind == JsonValueKind.True) { value = true; return true; }
        if (element.ValueKind == JsonValueKind.False) { value = false; return true; }
        return false;
    }

    private static bool TryString(JsonElement payload, string name, out string value)
    {
        value = string.Empty;
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty(name, out JsonElement element) ||
            element.ValueKind != JsonValueKind.String) return false;
        value = element.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryNumber(JsonElement payload, string name, double minimum, double maximum, out double value)
    {
        value = 0.0;
        return payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty(name, out JsonElement element) &&
               element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out value) && double.IsFinite(value) &&
               value >= minimum && value <= maximum;
    }

    private static RemoteCommandResult Invalid(string name) =>
        RemoteCommandResult.Fail("invalid_parameter", $"Missing or invalid parameter: {name}.");

    private static RemoteCommandResult Range(string name, double minimum, double maximum) =>
        RemoteCommandResult.Fail("invalid_range", $"{name} must be between {minimum} and {maximum}.");
}
