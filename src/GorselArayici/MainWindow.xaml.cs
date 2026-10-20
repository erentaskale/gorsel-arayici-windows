using System.Windows;
using System.Windows.Controls;
using GorselArayici.ViewModels;

namespace GorselArayici;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private bool _kapatilabilir;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.YukleAsync();

        // Çift tık: kutunun kendisi tıklamayı yuttuğu için XAML'deki MouseBinding'e ulaşmıyordu.
        // Burada tıklanan yerin hangi kutuya ait olduğunu bulup o resmi açıyoruz.
        SonucListesi.MouseDoubleClick += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject tiklanan
                && ItemsControl.ContainerFromElement(SonucListesi, tiklanan) is ListBoxItem { DataContext: ResimOgesi oge })
                _viewModel.AcCommand.Execute(oge);
        };

        // Kapanırken önce taramayı durdurup son grubun kaydını bekle, sonra kapan.
        // Pencere hemen gizlenir; kullanıcı beklemez. Sonraki açılışta kaldığı yerden devam eder.
        Closing += async (_, e) =>
        {
            if (_kapatilabilir)
                return;
            e.Cancel = true;
            Hide();
            await _viewModel.DurdurAsync();
            _kapatilabilir = true;
            Close();
        };
    }
}
