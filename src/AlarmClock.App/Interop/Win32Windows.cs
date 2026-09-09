using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AlarmClock.App.Interop;

/// <summary>
/// Posicionamento de janela em pixels físicos.
/// </summary>
/// <remarks>
/// WPF trabalha em DIPs e o WinForms <c>Screen</c> devolve pixels físicos.
/// Converter entre os dois exige saber o DPI de cada monitor, e num setup com
/// escalas diferentes por tela isso vira uma fonte permanente de janela meio
/// fora do lugar. Falar direto com <c>SetWindowPos</c> resolve exato: o overlay
/// cobre o monitor inteiro, ponto.
/// </remarks>
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

    /// <summary>Garante o HWND mesmo antes de a janela aparecer.</summary>
    public static IntPtr HandleOf(Window window) => new WindowInteropHelper(window).EnsureHandle();

    /// <summary>Fator de escala do monitor onde a janela está (1.0 = 96 DPI).</summary>
    public static double ScaleOf(Window window)
    {
        var dpi = GetDpiForWindow(HandleOf(window));
        return dpi == 0 ? 1d : dpi / 96d;
    }

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
    /// Encosta a janela no canto inferior direito da área de trabalho da tela
    /// principal, respeitando a barra de tarefas.
    /// </summary>
    /// <remarks>
    /// Usa o tamanho já medido pelo WPF (<c>ActualWidth</c>/<c>ActualHeight</c>),
    /// e não um tamanho fixo: com <c>SizeToContent</c>, a altura do card depende
    /// de o alarme ter mensagem e de quantas opções de adiamento o nível oferece.
    /// </remarks>
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

        // Sem ativar: um card discreto no canto não deve roubar o cursor de
        // quem está digitando.
        PlacePhysical(window, destino, activate: false);
    }
}
