using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using GorselArayici.Services;

namespace GorselArayici.ViewModels;

public record ResimOgesi(string Ad, string Klasor, string Yol);

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string durum = "Galeri aranıyor…";

    [ObservableProperty]
    private List<ResimOgesi> resimler = [];

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
    }
}
