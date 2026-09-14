using NAudio.Wave;

namespace AlarmClock.App.Audio;

/// <summary>
/// Reinicia o arquivo quando ele acaba. Reagir ao evento PlaybackStopped daria
/// um buraco audível entre as voltas — aqui a emenda acontece dentro do mesmo
/// buffer.
/// </summary>
public sealed class LoopingSampleProvider(AudioFileReader reader) : ISampleProvider
{
    private readonly AudioFileReader _reader = reader;

    public WaveFormat WaveFormat => _reader.WaveFormat;

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
