# Görsel Arayıcı for Windows

Fotoğraflarının **üzerindeki yazıyla** arama yap. Bir ekran görüntüsünde, tabelada ya da belgede gördüğün bir kelimeyi yaz, o fotoğraf karşına gelsin.

> 🚧 Geliştirme aşamasında. Henüz kullanıma hazır bir sürüm yok.

## Neden?

Binlerce fotoğraf ve ekran görüntüsü arasında aradığını bulmak zor. Windows Fotoğraflar uygulaması görselin içindeki yazıyı aramıyor. Görsel Arayıcı bu boşluğu dolduruyor.

## Nasıl çalışır?

1. Uygulama, Windows Fotoğraflar'ın **Galeri**'sinde gördüğün fotoğrafları kendisi bulur (Resimler kitaplığı). Klasör seçmen gerekmez.
2. İlk açılışta fotoğrafları bir kere tarar: üzerindeki yazıyı okur (OCR) ve tarihini alır. Tarama arka planda sürer, beklerken de arama yapabilirsin.
3. Sonraki açılışlarda yalnız **yeni eklenen** fotoğraflar taranır.
4. Arama kutusuna bir kelime yazarsın, eşleşen fotoğraflar listelenir.
5. Tıkladığın fotoğraf Windows Fotoğraflar'da açılır.

## Gizlilik

Her şey **senin bilgisayarında** çalışır. İnternet bağlantısı gerekmez, fotoğrafların hiçbir yere gönderilmez. Yazı tanıma için Windows'un kendi OCR motoru kullanılır.

## Teknolojiler

| Parça | Teknoloji |
|---|---|
| Dil / platform | C# / .NET 10 |
| Arayüz | WPF |
| Yazı tanıma | Windows.Media.Ocr |
| Tarih bilgisi | MetadataExtractor (EXIF) |
| Veritabanı | SQLite + FTS5 |

## Yol haritası

**v1**
- [ ] Galerideki fotoğrafları bulma (Resimler kitaplığı)
- [ ] Arka planda, kaldığı yerden devam eden tarama
- [ ] OCR ile yazı okuma
- [ ] EXIF'ten tarih okuma
- [ ] Yazıya ve tarihe göre arama
- [ ] Sonuca tıklayınca Fotoğraflar'da açma
- [ ] GitHub Releases üzerinden indirilebilir `.exe`

**v2**
- [ ] Her yerden açılan kısayol tuşu (ör. `Ctrl+Alt+F`)
- [ ] Uygulama kapalıyken eklenen fotoğrafları da anında yakalama (klasör izleme)

## Lisans

[MIT](LICENSE)

## Geliştirme

Gereksinim: [.NET 10 SDK](https://dotnet.microsoft.com/download)

```bash
git clone https://github.com/erentaskale/gorsel-arayici-windows.git
cd gorsel-arayici-windows
dotnet run --project src/GorselArayici
```
