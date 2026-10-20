using System.Windows;
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
