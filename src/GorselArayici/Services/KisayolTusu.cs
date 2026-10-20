using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace GorselArayici.Services;

/// <summary>
/// Windows genelinde çalışan kısayol tuşu (Ctrl+Alt+F): hangi uygulamadaysan bas, arama penceresi öne gelsin.
/// WPF'te hazırı yok; Windows'un kendi RegisterHotKey işlevini kullanıyoruz.
/// Not: uygulama açıkken (simge durumunda bile) çalışır; tamamen kapalıysa çalışmaz.
/// </summary>
public sealed class KisayolTusu : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int Kimlik = 0x4741; // bu uygulamaya ait herhangi bir sayı
    private const uint Alt = 0x1, Ctrl = 0x2, Tekrarsiz = 0x4000;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr pencere, int kimlik, uint degistirici, uint tus);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr pencere, int kimlik);

    private readonly HwndSource _kaynak;
    private readonly Action _basildi;

    /// <summary>Başka bir uygulama aynı tuşu kapmışsa false.</summary>
    public bool Kayitli { get; }

    public KisayolTusu(Window pencere, Action basildi)
    {
        _basildi = basildi;
        _kaynak = HwndSource.FromHwnd(new WindowInteropHelper(pencere).Handle)!;
        _kaynak.AddHook(MesajGeldi);
        Kayitli = RegisterHotKey(_kaynak.Handle, Kimlik, Ctrl | Alt | Tekrarsiz,
            (uint)KeyInterop.VirtualKeyFromKey(Key.F));
    }

    private IntPtr MesajGeldi(IntPtr pencere, int mesaj, IntPtr w, IntPtr l, ref bool islendi)
    {
        if (mesaj == WmHotkey && w.ToInt32() == Kimlik)
        {
            _basildi();
            islendi = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterHotKey(_kaynak.Handle, Kimlik);
        _kaynak.RemoveHook(MesajGeldi);
    }
}
