using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace GorselArayici.Services;

/// <summary>
/// Açık / koyu tema. İlk açılışta Windows'un ayarına uyar; kullanıcı düğmeyle değiştirirse seçimi hatırlanır.
/// Renkler Temalar\Acik.xaml ve Temalar\Koyu.xaml'de; değişince uygulamanın renk sözlüğü ötekiyle yer değiştirir.
/// </summary>
public static class Tema
{
    public static bool Koyu { get; private set; }

    /// <summary>Tema değişince (düğmenin simgesini güncellemek gibi işler için).</summary>
    public static event Action? Degisti;

    public static void Baslat()
    {
        var secim = Ayarlar.Simdiki.Tema;
        Uygula(secim is null ? WindowsKoyuMu() : secim == "Koyu");
    }

    public static void Degistir()
    {
        Uygula(!Koyu);
        Ayarlar.Simdiki.Tema = Koyu ? "Koyu" : "Acik";
        Ayarlar.Simdiki.Kaydet();
    }

    private static void Uygula(bool koyu)
    {
        Koyu = koyu;
        // App.xaml'deki ilk birleşik sözlük renk sözlüğüdür; onu yenisiyle değiştir.
        Application.Current.Resources.MergedDictionaries[0] = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Temalar/{(koyu ? "Koyu" : "Acik")}.xaml"),
        };
        foreach (Window pencere in Application.Current.Windows)
            BaslikCubugunuBoya(pencere);
        Degisti?.Invoke();
    }

    /// <summary>
    /// Pencerenin başlık çubuğu WPF'in değil Windows'un; onu koyulaştırmak için Windows'a ayrıca söylemek gerekir.
    /// Yoksa koyu temada üstte beyaz bir şerit kalır.
    /// </summary>
    public static void BaslikCubugunuBoya(Window pencere)
    {
        var tutamac = new WindowInteropHelper(pencere).Handle;
        if (tutamac == IntPtr.Zero)
            return; // pencere henüz oluşmadı; SourceInitialized'da tekrar çağrılır
        var deger = Koyu ? 1 : 0;
        DwmSetWindowAttribute(tutamac, KoyuBaslikCubugu, ref deger, sizeof(int));
    }

    /// <summary>Windows Ayarlar > Kişiselleştirme > Renkler'de uygulama modu "Koyu" mu?</summary>
    private static bool WindowsKoyuMu()
    {
        using var anahtar = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return anahtar?.GetValue("AppsUseLightTheme") is int acik && acik == 0;
    }

    private const int KoyuBaslikCubugu = 20; // DWMWA_USE_IMMERSIVE_DARK_MODE

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr pencere, int ozellik, ref int deger, int boyut);
}
