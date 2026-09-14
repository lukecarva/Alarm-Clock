using System.IO;
using AlarmClock.Core.Model;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AlarmClock.App.Audio;

/// <summary>Plays an alert's sound; one instance per on-screen alert. | Toca o som de um alerta; uma instância por alerta na tela.</summary>
public sealed class AlarmAudioPlayer(ILogger log) : IDisposable
{
    private readonly ILogger _log = log;

    private WaveOutEvent? _output;
    private AudioFileReader? _reader;
    private bool _disposed;

    /// <summary>Plays the given sound (volume, fade and loop applied). | Toca o som informado (volume, fade e loop aplicados).</summary>
    public void Play(SoundSpec spec)
    {
        if (_disposed || spec.IsSilent)
        {
            return;
        }

        try
        {
            Stop();

            ISampleProvider fonte = BuildSource(spec);

            if (spec.Volume < 1d)
            {
                fonte = new VolumeSampleProvider(fonte) { Volume = (float)Math.Clamp(spec.Volume, 0d, 1d) };
            }

            if (spec.FadeIn > TimeSpan.Zero)
            {
                var fade = new FadeInOutSampleProvider(fonte, initiallySilent: true);
                fade.BeginFadeIn(spec.FadeIn.TotalMilliseconds);
                fonte = fade;
            }

            _output = new WaveOutEvent();
            _output.Init(fonte);
            _output.Play();
        }
        catch (Exception ex)
        {
            // Keep the visual alert even if audio fails. | Mantém o alerta visual mesmo se o áudio falhar.
            _log.LogError(ex, "Falha ao tocar o som do alerta. Seguindo em silêncio.");
            Stop();
        }
    }

    /// <summary>Builds the sample source: the user's file, or the built-in tone. | Monta a fonte de áudio: o arquivo do usuário, ou o tom embutido.</summary>
    private ISampleProvider BuildSource(SoundSpec spec)
    {
        if (spec.FilePath is { Length: > 0 } caminho && File.Exists(caminho))
        {
            _reader = new AudioFileReader(caminho);
            return spec.Loop ? new LoopingSampleProvider(_reader) : _reader;
        }

        if (spec.FilePath is { Length: > 0 } ausente)
        {
            _log.LogWarning("Som {Caminho} não encontrado. Usando o toque padrão.", ausente);
        }

        var repete = spec.Loop || spec.RepeatEvery > TimeSpan.Zero;
        var intervalo = spec.RepeatEvery > TimeSpan.Zero ? spec.RepeatEvery : TimeSpan.FromMilliseconds(700);

        return new AlarmToneProvider(intervalo, repete);
    }

    /// <summary>Stops playback and releases audio resources. | Para a reprodução e libera os recursos de áudio.</summary>
    public void Stop()
    {
        _output?.Dispose();
        _output = null;

        _reader?.Dispose();
        _reader = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }
}
