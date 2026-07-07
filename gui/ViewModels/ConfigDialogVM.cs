using TreeChat.Commands;
using TreeChat.Services;

namespace TreeChat.ViewModels
{
    public class ConfigDialogViewModel : BaseViewModel
    {
        // 原始配置值
        public string ApiKey { get; set; }
        public string ApiEndpoint { get; set; }
        public string ModelName { get; set; }

        private double _temperature;
        public double Temperature
        {
            get => _temperature;
            set => SetProperty(ref _temperature, value);
        }

        private double _topP;
        public double TopP
        {
            get => _topP;
            set => SetProperty(ref _topP, value);
        }

        private int _topK;
        public int TopK
        {
            get => _topK;
            set => SetProperty(ref _topK, value);
        }

        // 文本框绑定用字符串
        private string _temperatureText;
        public string TemperatureText
        {
            get => _temperatureText;
            set
            {
                if (SetProperty(ref _temperatureText, value))
                    ValidateTemperature();
            }
        }

        private string _topPText;
        public string TopPText
        {
            get => _topPText;
            set
            {
                if (SetProperty(ref _topPText, value))
                    ValidateTopP();
            }
        }

        private string _topKText;
        public string TopKText
        {
            get => _topKText;
            set
            {
                if (SetProperty(ref _topKText, value))
                    ValidateTopK();
            }
        }

        // 显示默认值提示
        public string TemperatureDisplay => $"(默认 {ApiConfig.Temperature:F1})";
        public string TopPDisplay => $"(默认 {ApiConfig.TopP:F1})";
        public string TopKDisplay => $"(默认 {ApiConfig.TopK})";

        private bool _isValid;
        public bool IsValid
        {
            get => _isValid;
            private set => SetProperty(ref _isValid, value);
        }

        public RelayCommand ConfirmCommand { get; }
        public RelayCommand CancelCommand { get; }

        public event Action<bool?> CloseRequest;

        public ConfigDialogViewModel(string apiKey, string apiEndpoint, string modelName,
                                      double temperature, double topP, int topK)
        {
            ApiKey = apiKey;
            ApiEndpoint = apiEndpoint;
            ModelName = modelName;
            Temperature = temperature;
            TopP = topP;
            TopK = topK;

            _temperatureText = temperature.ToString();
            _topPText = topP.ToString();
            _topKText = topK.ToString();

            ValidateAll();
            ConfirmCommand = new RelayCommand(_ => Confirm(), _ => IsValid);
            CancelCommand = new RelayCommand(_ => Cancel());
        }

        private void ValidateTemperature()
        {
            if (double.TryParse(TemperatureText, out double value) && value >= 0 && value <= 2)
            {
                Temperature = value;
                OnPropertyChanged(nameof(Temperature));
            }
            ValidateAll();
        }

        private void ValidateTopP()
        {
            if (double.TryParse(TopPText, out double value) && value >= 0 && value <= 1)
            {
                TopP = value;
                OnPropertyChanged(nameof(TopP));
            }
            ValidateAll();
        }

        private void ValidateTopK()
        {
            if (int.TryParse(TopKText, out int value) && value >= 0 && value <= 40)
            {
                TopK = value;
                OnPropertyChanged(nameof(TopK));
            }
            ValidateAll();
        }

        private void ValidateAll()
        {
            bool tempOk = double.TryParse(TemperatureText, out double t) && t >= 0 && t <= 2;
            bool topPOk = double.TryParse(TopPText, out double p) && p >= 0 && p <= 1;
            bool topKOk = int.TryParse(TopKText, out int k) && k >= 0 && k <= 40;
            IsValid = tempOk && topPOk && topKOk;
        }

        private void Confirm()
        {
            if (IsValid)
                CloseRequest?.Invoke(true);
        }

        private void Cancel() => CloseRequest?.Invoke(false);
    }
}