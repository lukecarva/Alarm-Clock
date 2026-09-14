using System.IO;
using AlarmClock.Core.Model;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AlarmClock.App.Audio;

/// <summary>
/// Toca o som de um alerta. Uma instância por alerta na tela — dois alarmes
/// simultâneos tocam em paralelo, cada um com seu volume e seu fade.
/// </summary>
public sealed class AlarmAudioPlayer(ILogger log) : IDisposable
{
    private readonly ILogger _log = log;

    private WaveOutEvent? _output;
    private AudioFileReader? _reader;
    private bool _disposed;

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
            // Um alarme sem som ainda é um alarme. Placa muda, dispositivo
            // ocupado ou arquivo corrompido não podem impedir o alerta visual.
            _log.LogError(ex, "Falha ao tocar o som do alerta. Seguindo em silêncio.");
            Stop();
        }
    }

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
