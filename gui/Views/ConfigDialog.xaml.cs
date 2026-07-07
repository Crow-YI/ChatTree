using System.Windows;
using TreeChat.Services;
using TreeChat.ViewModels;

namespace TreeChat.Views
{
    public partial class ConfigDialog : Window
    {
        public ConfigDialogViewModel ViewModel { get; }

        public ConfigDialog()
        {
            InitializeComponent();
            ViewModel = new ConfigDialogViewModel(
                ApiConfig.ApiKey, ApiConfig.ApiEndpoint, ApiConfig.ModelName,
                ApiConfig.Temperature, ApiConfig.TopP, ApiConfig.TopK);
            DataContext = ViewModel;
            ViewModel.CloseRequest += result =>
            {
                DialogResult = result;
                Close();
            };
        }

        // 对外暴露配置值（供调用方读取）
        public string ApiKey => ViewModel.ApiKey;
        public string ApiEndpoint => ViewModel.ApiEndpoint;
        public string ModelName => ViewModel.ModelName;
        public double Temperature => ViewModel.Temperature;
        public double TopP => ViewModel.TopP;
        public int TopK => ViewModel.TopK;
    }
}