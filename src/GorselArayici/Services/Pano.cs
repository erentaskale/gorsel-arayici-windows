using System.Collections.Specialized;
using System.Windows;

namespace GorselArayici.Services;

/// <summary>
/// Resmi panoya (Ctrl+C) koyar. Kalite birebir aynı kalsın diye orijinal dosyanın kendisi konur,
/// Dosya Gezgini'nde kopyalamak gibi. Dosyayı değil resmi bekleyen uygulamalar (Paint gibi) için
/// tam boy görüntü de yanına eklenir.
/// </summary>
public static class Pano
{
    public static void Kopyala(string yol)
    {
        var veri = new DataObject();
        veri.SetFileDropList(new StringCollection { yol });
        if (Onizleyici.TamYukle(yol) is { } resim)
            veri.SetImage(resim);

        // copy: true → uygulama kapansa bile panodaki kopya kalır.
        Clipboard.SetDataObject(veri, copy: true);
    }
}
