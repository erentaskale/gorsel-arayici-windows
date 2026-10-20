using System.Diagnostics;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GorselArayici.Services;

namespace GorselArayici.ViewModels;

/// <summary>
/// Uygulamanın içindeki büyük görünüm: bir aramanın sonuçları arasında ← → ile gezilir.
/// </summary>
public partial class GoruntuleyiciViewModel : ObservableObject
{
    private List<ResimOgesi> _sonuclar = [];
    private string[] _arananKelimeler = [];
    private int _indeks;
    private int _gosterimNo; // hızlı gezerken eski resmin geç gelen sonucu yenisinin üstüne yazmasın
    private OcrOkuyucu? _ocr;

    [ObservableProperty]
    private bool acik;

    [ObservableProperty]
    private ResimOgesi? secili;

    [ObservableProperty]
    private string sayac = "";

    [ObservableProperty]
    private ImageSource? resim;

    // Resmin piksel boyutu. İşaret kutuları da piksel cinsinden; ikisi birlikte ölçeklenir.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CerceveKalinligi))]
    private double resimGenislik;

    [ObservableProperty]
    private double resimYukseklik;

    /// <summary>İşaret çerçevesinin kalınlığı, resmin piksel genişliğine oranlı (küçülünce de görünsün).</summary>
    public double CerceveKalinligi => Math.Max(2, ResimGenislik / 400);

    [ObservableProperty]
    private List<KelimeKonumu> isaretler = [];

    /// <summary>Resimden resme geçerken korunur; kullanıcı kapattıysa kapalı kalır.</summary>
    [ObservableProperty]
    private bool isaretleriGoster = true;

    /// <summary>Kopyalama gibi işlerin kısa süreli bildirimini dışarıya (ana pencereye) iletir.</summary>
    public event Action<string>? BildirimVar;

    public void Ac(List<ResimOgesi> sonuclar, ResimOgesi baslangic, string aramaMetni)
    {
        _sonuclar = sonuclar;
        _arananKelimeler = Normallestirici.Kelimeler(aramaMetni);
        _indeks = Math.Max(0, sonuclar.IndexOf(baslangic));
        Acik = true;
        _ = GosterAsync();
    }

    [RelayCommand]
    private void Kapat() => Acik = false;

    [RelayCommand]
    private void Sonraki() => Git(+1);

    [RelayCommand]
    private void Onceki() => Git(-1);

    [RelayCommand]
    private void IsaretleriDegistir() => IsaretleriGoster = !IsaretleriGoster;

    [RelayCommand]
    private void FotograflardaAc()
    {
        if (Secili is null)
            return;
        try
        {
            // UseShellExecute: dosyayı çift tıklamışız gibi, Windows hangi uygulamayla açılacağına karar verir.
            Process.Start(new ProcessStartInfo(Secili.Yol) { UseShellExecute = true });
        }
        catch (Exception hata)
        {
            BildirimVar?.Invoke("Resim açılamadı: " + hata.Message);
        }
    }

    private void Git(int yon)
    {
        if (_sonuclar.Count == 0)
            return;
        var yeni = _indeks + yon;
        if (yeni < 0 || yeni >= _sonuclar.Count)
            return; // başta ya da sonda dur, başa sarma
        _indeks = yeni;
        _ = GosterAsync();
    }

    private async Task GosterAsync()
    {
        var no = ++_gosterimNo;
        var oge = _sonuclar[_indeks];
        Secili = oge;
        Sayac = $"{_indeks + 1} / {_sonuclar.Count}";
        Isaretler = [];

        // Tam boy resim gelene kadar kutudaki küçük önizleme, resmin gerçek boyutunda gerilerek görünsün:
        // ekran boş kalmaz, tam resim gelince yerine oturur.
        Resim = oge.Onizleme;
        ResimGenislik = oge.Genislik;
        ResimYukseklik = oge.Yukseklik;

        var tam = await Task.Run(() => Onizleyici.TamYukle(oge.Yol));
        if (no != _gosterimNo)
            return;
        if (tam is not null)
        {
            Resim = tam;
            ResimGenislik = tam.PixelWidth;
            ResimYukseklik = tam.PixelHeight;
        }

        // Kelimelerin yeri veritabanında yok; resim başına ~40 ms olduğu için açarken yeniden okuyoruz.
        try
        {
            _ocr ??= new OcrOkuyucu();
            var kelimeler = await Task.Run(() => _ocr.KelimeleriBulAsync(oge.Yol));
            if (no != _gosterimNo)
                return;
            // Çerçeve kelimeden biraz taşsın (kelimenin harflerini kapatmasın).
            var pay = CerceveKalinligi * 2;
            Isaretler = kelimeler
                .Where(EslesiyorMu)
                .Select(k => k with { X = k.X - pay, Y = k.Y - pay, Genislik = k.Genislik + 2 * pay, Yukseklik = k.Yukseklik + 2 * pay })
                .ToList();
        }
        catch
        {
            // İşaretleme bir ek özellik; okunamazsa resim yine de görünür.
        }
    }

    /// <summary>Aramadaki gibi: kelime, aranan kelimelerden biriyle başlıyorsa işaretlenir ("fat" → "fatura").</summary>
    private bool EslesiyorMu(KelimeKonumu kelime) =>
        Normallestirici.Kelimeler(kelime.Metin)
            .Any(parca => _arananKelimeler.Any(aranan => parca.StartsWith(aranan, StringComparison.Ordinal)));
}
