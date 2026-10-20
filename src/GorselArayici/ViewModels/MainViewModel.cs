using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GorselArayici.Services;

namespace GorselArayici.ViewModels;

/// <summary>Bir arama sonucu kutusu.</summary>
public partial class ResimOgesi(string ad, string ozet, string yol) : ObservableObject
{
    public string Ad { get; } = ad;

    /// <summary>Resimdeki yazıdan, aranan kelimenin geçtiği kısa bir parça.</summary>
    public string Ozet { get; } = ozet;

    public string Yol { get; } = yol;

    /// <summary>Önizleme arka planda yüklenir; hazır olunca kutuda belirir.</summary>
    [ObservableProperty]
    private ImageSource? onizleme;
}

public partial class MainViewModel : ObservableObject
{
    private readonly CancellationTokenSource _iptal = new();
    private Task<int>? _tarama;
    private int _aramaNo;

    [ObservableProperty]
    private string durum = "Galeri aranıyor…";

    [ObservableProperty]
    private List<ResimOgesi> liste = [];

    [ObservableProperty]
    private bool taraniyor;

    [ObservableProperty]
    private double ilerlemeYuzde;

    [ObservableProperty]
    private string sonOkunan = "";

    [ObservableProperty]
    private string aramaMetni = "";

    [ObservableProperty]
    private string sonucBilgisi = "";

    public async Task YukleAsync()
    {
        // 12 bin dosyayı gezmek birkaç saniye sürebilir; pencere donmasın diye arka planda.
        var yollar = await Task.Run(GaleriBulucu.ResimleriBul);
        Durum = $"Galeride {yollar.Count:N0} resim bulundu";

        await TaraAsync(yollar);
    }

    // Arama kutusu her değiştiğinde çağrılır (CommunityToolkit bu metodu kendisi bağlar).
    partial void OnAramaMetniChanged(string value) => _ = AraAsync(value);

    private async Task AraAsync(string sorgu)
    {
        // Hızlı yazarken her harfte aramayalım: kısa bir bekleme, arada yeni harf geldiyse bu arama iptal.
        var no = ++_aramaNo;
        await Task.Delay(150);
        if (no != _aramaNo)
            return;

        if (string.IsNullOrWhiteSpace(sorgu))
        {
            Liste = [];
            SonucBilgisi = "";
            return;
        }

        List<(string Yol, string Metin)> bulunanlar;
        try
        {
            bulunanlar = await Task.Run(() =>
            {
                using var veritabani = new Veritabani();
                return veritabani.Ara(sorgu);
            });
        }
        catch (Exception hata)
        {
            SonucBilgisi = "Arama yapılamadı: " + hata.Message;
            return;
        }

        // Bu arada başka bir arama başladıysa eski sonucu gösterme.
        if (no != _aramaNo)
            return;

        var ilkKelime = Normallestirici.Kelimeler(sorgu).FirstOrDefault() ?? "";
        var ogeler = bulunanlar
            .Select(b => new ResimOgesi(Path.GetFileName(b.Yol), Ozet(b.Metin, ilkKelime), b.Yol))
            .ToList();
        Liste = ogeler;

        SonucBilgisi = bulunanlar.Count switch
        {
            0 => "Bu yazıyı içeren resim bulunamadı",
            >= Veritabani.EnFazlaSonuc => $"En alakalı {Veritabani.EnFazlaSonuc} resim · büyütmek için çift tıkla",
            var n => $"{n:N0} resim bulundu · büyütmek için çift tıkla",
        };

        await OnizlemeleriYukleAsync(ogeler, no);
    }

    /// <summary>
    /// Önizlemeleri arka planda, aynı anda 4 tane hazırlar; her biri hazır olunca kutusunda belirir.
    /// Kullanıcı yeni bir şey yazarsa eski aramanın önizlemeleri yarıda bırakılır.
    /// </summary>
    private async Task OnizlemeleriYukleAsync(List<ResimOgesi> ogeler, int no)
    {
        var pencere = Application.Current.Dispatcher;
        await Parallel.ForEachAsync(ogeler, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (oge, _) =>
        {
            if (no != _aramaNo)
                return;
            var resim = Onizleyici.Yukle(oge.Yol);
            await pencere.InvokeAsync(() => oge.Onizleme = resim);
        });
    }

    private async Task TaraAsync(List<string> yollar)
    {
        Taraniyor = true;
        var sure = Stopwatch.StartNew();

        // Progress, raporu pencerenin kendi iş parçacığına taşır; arayüz güvenle güncellenir.
        var ilerleme = new Progress<TaramaDurumu>(d =>
        {
            IlerlemeYuzde = 100.0 * d.Biten / d.Toplam;
            Durum = $"Yazılar okunuyor: {d.Biten:N0} / {d.Toplam:N0}";
            SonOkunan = $"{d.SonDosya}: {Kisalt(d.SonMetin)}";
        });

        try
        {
            _tarama = Task.Run(() => Tarayici.TaraAsync(yollar, ilerleme, _iptal.Token));
            var okunan = await _tarama;
            Durum = okunan == 0
                ? $"Galeride {yollar.Count:N0} resim var, hepsi okunmuş"
                : $"{okunan:N0} resim okundu ({sure.Elapsed.TotalMinutes:N1} dk)";
        }
        catch (Exception hata)
        {
            Durum = "Tarama durdu: " + hata.Message;
        }
        finally
        {
            Taraniyor = false;
            SonOkunan = "";
        }
    }

    /// <summary>Resmi Windows'un varsayılan resim uygulamasında (Fotoğraflar) açar.</summary>
    [RelayCommand]
    private void Ac(ResimOgesi? oge)
    {
        if (oge is null)
            return;
        try
        {
            // UseShellExecute: dosyayı çift tıklamışız gibi, Windows hangi uygulamayla açılacağına karar verir.
            Process.Start(new ProcessStartInfo(oge.Yol) { UseShellExecute = true });
        }
        catch (Exception hata)
        {
            SonucBilgisi = "Resim açılamadı: " + hata.Message;
        }
    }

    /// <summary>Taramayı durdurur ve son grubun kaydedilmesini bekler.</summary>
    public async Task DurdurAsync()
    {
        _iptal.Cancel();
        if (_tarama is null)
            return;
        try { await _tarama; }
        catch { /* hata zaten Durum'a yazıldı */ }
    }

    /// <summary>Metnin, aranan kelimenin geçtiği yerin çevresinden kısa bir parçası.</summary>
    private static string Ozet(string metin, string kelime)
    {
        // Normalleştirme harf sayısını değiştirmez, bu yüzden bulunan konum orijinal metinde de geçerli.
        var konum = Math.Max(0, Normallestirici.Yap(metin).IndexOf(kelime, StringComparison.Ordinal));
        var bas = Math.Max(0, konum - 30);
        var parca = metin.Substring(bas, Math.Min(100, metin.Length - bas));
        return (bas > 0 ? "…" : "") + parca + (bas + parca.Length < metin.Length ? "…" : "");
    }

    private static string Kisalt(string metin) =>
        string.IsNullOrWhiteSpace(metin) ? "(yazı yok)"
        : metin.Length > 70 ? metin[..70] + "…"
        : metin;
}
