using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using GorselArayici.Services;

namespace GorselArayici.ViewModels;

public record ResimOgesi(string Ad, string Klasor, string Yol);

public partial class MainViewModel : ObservableObject
{
    private readonly CancellationTokenSource _iptal = new();
    private Task<int>? _tarama;

    [ObservableProperty]
    private string durum = "Galeri aranıyor…";

    [ObservableProperty]
    private List<ResimOgesi> resimler = [];

    [ObservableProperty]
    private bool taraniyor;

    [ObservableProperty]
    private double ilerlemeYuzde;

    [ObservableProperty]
    private string sonOkunan = "";

    public async Task YukleAsync()
    {
        // 12 bin dosyayı gezmek birkaç saniye sürebilir; pencere donmasın diye arka planda.
        var bulunanlar = await Task.Run(() =>
            GaleriBulucu.ResimleriBul()
                .Select(yol => new ResimOgesi(
                    Path.GetFileName(yol),
                    Path.GetRelativePath(GaleriBulucu.GaleriKlasoru, Path.GetDirectoryName(yol)!),
                    yol))
                .ToList());

        Resimler = bulunanlar;
        Durum = $"Galeride {bulunanlar.Count:N0} resim bulundu";

        await TaraAsync(bulunanlar.Select(r => r.Yol).ToList());
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

    /// <summary>Taramayı durdurur ve son grubun kaydedilmesini bekler.</summary>
    public async Task DurdurAsync()
    {
        _iptal.Cancel();
        if (_tarama is null)
            return;
        try { await _tarama; }
        catch { /* hata zaten Durum'a yazıldı */ }
    }

    private static string Kisalt(string metin) =>
        string.IsNullOrWhiteSpace(metin) ? "(yazı yok)"
        : metin.Length > 70 ? metin[..70] + "…"
        : metin;
}
