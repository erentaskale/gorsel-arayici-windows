using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GorselArayici.Services;

namespace GorselArayici.ViewModels;

/// <summary>Bir arama sonucu kutusu.</summary>
/// <param name="ayBasligi">Izgaradaki grup başlığı ("Ekim 2026").</param>
public partial class ResimOgesi(string ad, string ozet, string yol, DateTime tarih, string ayBasligi) : ObservableObject
{
    public string Ad { get; } = ad;

    public DateTime Tarih { get; } = tarih;

    public string AyBasligi { get; } = ayBasligi;

    /// <summary>Kutuda gösterilen tarih: "12 Kasım 2023".</summary>
    public string TarihYazisi => Tarih.ToString("d MMMM yyyy", new System.Globalization.CultureInfo("tr-TR"));

    /// <summary>Resimdeki yazıdan, aranan kelimenin geçtiği kısa bir parça.</summary>
    public string Ozet { get; } = ozet;

    public string Yol { get; } = yol;

    /// <summary>Önizleme arka planda yüklenir; hazır olunca kutuda belirir.</summary>
    [ObservableProperty]
    private ImageSource? onizleme;

    // Resmin gerçek piksel boyutu (önizleme yüklenirken öğrenilir; büyük görünüm kullanır).
    public int Genislik { get; set; }
    public int Yukseklik { get; set; }
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

    // Kutuların hepsi önizleme resmi demek; bellek şişmesin diye en fazla bu kadar gösterilir.
    private const int EnFazlaKutu = 200;


    public GoruntuleyiciViewModel Goruntuleyici { get; } = new();

    /// <summary>"Kopyalandı" gibi birkaç saniye görünüp kaybolan kısa mesaj.</summary>
    [ObservableProperty]
    private string bildirim = "";
    private int _bildirimNo;

    // Izgaradaki sütun sayısı: Ctrl + tekerlekle değişir (iPhone gibi), son seçim hatırlanır.
    // Kareler pencereyi her zaman tam doldurur; değişen şey satırdaki kare sayısıdır.
    private const int EnAzSutun = 3;
    private const int EnCokSutun = 12;
    private readonly Ayarlar _ayarlar = Ayarlar.Oku();

    [ObservableProperty]
    private int sutunSayisi;

    public MainViewModel()
    {
        SutunSayisi = Math.Clamp(_ayarlar.SutunSayisi, EnAzSutun, EnCokSutun);
        Goruntuleyici.BildirimVar += Bildir;
    }

    /// <param name="yon">+1 daha çok (küçük) kare, -1 daha az (büyük) kare.</param>
    public void SutunSayisiniDegistir(int yon)
    {
        SutunSayisi = Math.Clamp(SutunSayisi + yon, EnAzSutun, EnCokSutun);
        _ayarlar.SutunSayisi = SutunSayisi;
        _ayarlar.Kaydet();
    }
    /// <summary>Kutuyu uygulamanın içinde büyük açar; ← → ile bu aramanın sonuçlarında gezilir.</summary>
    [RelayCommand]
    private void Goruntule(ResimOgesi? oge)
    {
        if (oge is not null)
            Goruntuleyici.Ac(Liste, oge, AramaMetni);
    }

    /// <summary>Orijinal resmi panoya kopyalar (Ctrl+C).</summary>
    public void Kopyala(ResimOgesi? oge)
    {
        if (oge is null)
            return;
        try
        {
            Pano.Kopyala(oge.Yol);
            Bildir("Resim kopyalandı, istediğin yere yapıştırabilirsin (Ctrl+V)");
        }
        catch (Exception hata)
        {
            Bildir("Kopyalanamadı: " + hata.Message);
        }
    }

    private async void Bildir(string mesaj)
    {
        var no = ++_bildirimNo;
        Bildirim = mesaj;
        await Task.Delay(2500);
        if (no == _bildirimNo)
            Bildirim = "";
    }

    public async Task YukleAsync()
    {
        // 12 bin dosyayı gezmek birkaç saniye sürebilir; pencere donmasın diye arka planda.
        var yollar = await Task.Run(GaleriBulucu.ResimleriBul);
        Durum = $"{yollar.Count:N0} resim bulundu";

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

        List<(string Yol, string Metin, long Degisme)> bulunanlar;
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
        // Sonuçlar her zaman en yeniden eskiye: önce hepsi tarihe göre dizilir, sonra ilk 200 alınır
        // (en yeniler kaçmasın). Kareler ay ay gruplanır (iPhone gibi).
        var turkce = new System.Globalization.CultureInfo("tr-TR");
        var ogeler = bulunanlar
            .Select(b => (b.Yol, b.Metin, Tarih: TarihBulucu.Bul(b.Yol, b.Degisme)))
            .OrderByDescending(b => b.Tarih)
            .Take(EnFazlaKutu)
            .Select(b => new ResimOgesi(Path.GetFileName(b.Yol), Ozet(b.Metin, ilkKelime), b.Yol, b.Tarih,
                b.Tarih.ToString("MMMM yyyy", turkce)))
            .ToList();
        Liste = ogeler;

        SonucBilgisi = bulunanlar.Count switch
        {
            0 => "Bu yazıyı içeren resim bulunamadı",
            > EnFazlaKutu => $"{bulunanlar.Count:N0} sonuç · en yeni {EnFazlaKutu} tanesi",
            var n => $"{n:N0} sonuç",
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
            var (resim, genislik, yukseklik) = Onizleyici.KutuIcinYukle(oge.Yol);
            oge.Genislik = genislik;
            oge.Yukseklik = yukseklik;
            await pencere.InvokeAsync(() => oge.Onizleme = resim);
        });
    }

    private async Task TaraAsync(List<string> yollar)
    {
        Taraniyor = true;

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
            await _tarama;
            Durum = $"{yollar.Count:N0} resim aranmaya hazır";
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
