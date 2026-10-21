using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using GorselArayici.Services;

namespace GorselArayici;

public partial class App : Application
{
    // Tek kopya kuralı: aynı anda iki pencere aynı veritabanını taramasın.
    // İkinci kez açılırsa yeni pencere açılmaz, açık olan öne gelir.
    private Mutex? _tekKopya;

    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr pencere);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr pencere, int komut);
    private const int Geri = 9; // SW_RESTORE: simge durumundaysa eski boyutuna getir

    protected override void OnStartup(StartupEventArgs e)
    {
        _tekKopya = new Mutex(initiallyOwned: true, "GorselArayici-TekKopya", out var ilkKopya);
        if (!ilkKopya)
        {
            AcikOlaniOneGetir();
            Shutdown();
            return;
        }

        // Beklenmeyen hatalar sessizce kaybolmasın: hata.log'a yazılır, uygulama açık kalır.
        DispatcherUnhandledException += (_, a) =>
        {
            HatayiYaz(a.Exception);
            MessageBox.Show("Beklenmeyen bir hata oldu. Ayrıntı: " + HataDosyasi, "Görsel Arayıcı");
            a.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, a) => { HatayiYaz(a.Exception); a.SetObserved(); };

        base.OnStartup(e);
        Tema.Baslat(); // pencere açılmadan önce: ilk kare doğru renklerle çizilsin
        new MainWindow().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tekKopya?.Dispose();
        base.OnExit(e);
    }

    private static void AcikOlaniOneGetir()
    {
        var ben = Process.GetCurrentProcess();
        var acik = Process.GetProcessesByName(ben.ProcessName).FirstOrDefault(p => p.Id != ben.Id);
        if (acik is null || acik.MainWindowHandle == IntPtr.Zero)
            return;
        ShowWindow(acik.MainWindowHandle, Geri);
        SetForegroundWindow(acik.MainWindowHandle);
    }

    private static string HataDosyasi =>
        Path.Combine(Path.GetDirectoryName(Veritabani.DosyaYolu)!, "hata.log");

    private static void HatayiYaz(Exception hata)
    {
        try { File.AppendAllText(HataDosyasi, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {hata}\n\n"); }
        catch { /* kayıt yazılamazsa yapacak bir şey yok */ }
    }
}
