using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AlarmClock.App.Interop;

/// <summary>Positions windows in physical pixels via SetWindowPos. | Posiciona janelas em pixels físicos via SetWindowPos.</summary>
internal static class Win32Windows
{
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    private static readonly IntPtr HwndTopmost = new(-1);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    /// <summary>Gets the window handle, creating it if needed. | Obtém o HWND da janela, criando-o se preciso.</summary>
    public static IntPtr HandleOf(Window window) => new WindowInteropHelper(window).EnsureHandle();

    /// <summary>Scale factor of the window's monitor (1.0 = 96 DPI). | Fator de escala do monitor da janela (1.0 = 96 DPI).</summary>
    public static double ScaleOf(Window window)
    {
        var dpi = GetDpiForWindow(HandleOf(window));
        return dpi == 0 ? 1d : dpi / 96d;
    }

    /// <summary>Places the window topmost at physical bounds. | Posiciona a janela como topmost nos limites físicos.</summary>
    public static void PlacePhysical(Window window, Rectangle bounds, bool activate)
    {
        var flags = SwpShowWindow | (activate ? 0 : SwpNoActivate);

        SetWindowPos(
            HandleOf(window),
            HwndTopmost,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            flags);
    }

    /// <summary>
    /// Docks the window at the bottom-right of the primary work area, using its
    /// measured size. | Encosta a janela no canto inferior direito da área de trabalho principal,
    /// usando o tamanho já medido.
    /// </summary>
    public static void PlaceInCorner(Window window, int marginPx = 16)
    {
        var area = System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea
                   ?? new Rectangle(0, 0, 1280, 720);

        var escala = ScaleOf(window);
        var largura = (int)Math.Round(window.ActualWidth * escala);
        var altura = (int)Math.Round(window.ActualHeight * escala);

        var destino = new Rectangle(
            area.Right - largura - marginPx,
            area.Bottom - altura - marginPx,
            largura,
            altura);

        PlacePhysical(window, destino, activate: false);
    }
}
