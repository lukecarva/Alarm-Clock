using System.Runtime.InteropServices;
using AlarmClock.Core.Abstractions;

namespace AlarmClock.App.Services;

/// <summary>
/// Ociosidade pela última entrada de teclado ou mouse, via
/// <c>GetLastInputInfo</c>.
/// </summary>
public sealed class Win32IdleDetector : IIdleDetector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint cbSize;
        public uint dwTime;
    }

    // DllImport, não LibraryImport: este último exige AllowUnsafeBlocks no
    // projeto inteiro, e não vale habilitar unsafe por três P/Invoke triviais.
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    public TimeSpan IdleFor
    {
        get
        {
            var info = new LastInputInfo { cbSize = (uint)Marshal.SizeOf<LastInputInfo>() };

            if (!GetLastInputInfo(ref info))
            {
                // Falhou: assume presença. O erro aqui não pode virar alarme
                // silenciado.
                return TimeSpan.Zero;
            }

            // dwTime vive no mesmo espaço de GetTickCount: 32 bits, que dá a
            // volta a cada ~49,7 dias. A subtração em uint dá o resultado certo
            // mesmo na virada; fazer a conta em int (ou em long) não daria.
            var agora = (uint)Environment.TickCount;
            var decorrido = agora - info.dwTime;

            return TimeSpan.FromMilliseconds(decorrido);
        }
    }
}
