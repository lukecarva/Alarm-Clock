using NAudio.Wave;

namespace AlarmClock.App.Audio;

/// <summary>Wraps an audio reader and restarts it seamlessly at the end. | Envolve um leitor de áudio e o reinicia sem emenda ao terminar.</summary>
public sealed class LoopingSampleProvider(AudioFileReader reader) : ISampleProvider
{
    private readonly AudioFileReader _reader = reader;

    public WaveFormat WaveFormat => _reader.WaveFormat;

    /// <summary>Reads samples, looping back to the start when the file ends. | Lê amostras, voltando ao início quando o arquivo acaba.</summary>
    public int Read(float[] buffer, int offset, int count)
    {
        var total = 0;

        while (total < count)
        {
            var lidos = _reader.Read(buffer, offset + total, count - total);

            if (lidos == 0)
            {
                if (_reader.Length == 0)
                {
                    break;
                }

                _reader.Position = 0;
                continue;
            }

            total += lidos;
        }

        return total;
    }
}
