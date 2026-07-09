using TreeChat.Commands;
using TreeChat.Services;

namespace TreeChat.ViewModels
{
    public class ConfigDialogViewModel : BaseViewModel
    {
        // 原始配置值（全部可编辑）
        private string _apiKey;
        public string ApiKey
        {
            get => _apiKey;
            set => SetProperty(ref _apiKey, value);
        }

        private string _apiEndpoint;
        public string ApiEndpoint
        {
            get => _apiEndpoint;
            set => SetProperty(ref _apiEndpoint, value);
        }

        private string _modelName;
        public string ModelName
        {
            get => _modelName;
            set => SetProperty(ref _modelName, value);
        }

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

        private int _maxTokens;
        public int MaxTokens
        {
            get => _maxTokens;
            set => SetProperty(ref _maxTokens, value);
        }

        // ---- 文本框绑定用字符串 ----

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

        private string _maxTokensText;
        public string MaxTokensText
        {
            get => _maxTokensText;
            set
            {
                if (SetProperty(ref _maxTokensText, value))
                    ValidateMaxTokens();
            }
        }

        // ---- 显示默认值提示 ----

        public string TemperatureDisplay => $"(默认 {ApiConfig.Temperature:F1})";
        public string TopPDisplay => $"(默认 {ApiConfig.TopP:F1})";
        public string MaxTokensDisplay => $"(默认 {ApiConfig.MaxTokens})";

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
                                      double temperature, double topP, int maxTokens)
        {
            _apiKey = apiKey;
            _apiEndpoint = apiEndpoint;
            _modelName = modelName;
            Temperature = temperature;
            TopP = topP;
            MaxTokens = maxTokens;

            _temperatureText = temperature.ToString();
            _topPText = topP.ToString();
            _maxTokensText = maxTokens.ToString();

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

        private void ValidateMaxTokens()
        {
            if (int.TryParse(MaxTokensText, out int value) && value >= 1 && value <= 8192)
            {
                MaxTokens = value;
                OnPropertyChanged(nameof(MaxTokens));
            }
            ValidateAll();
        }

        private void ValidateAll()
        {
            bool tempOk = double.TryParse(TemperatureText, out double t) && t >= 0 && t <= 2;
            bool topPOk = double.TryParse(TopPText, out double p) && p >= 0 && p <= 1;
            bool maxTokensOk = int.TryParse(MaxTokensText, out int m) && m >= 1 && m <= 8192;
            IsValid = tempOk && topPOk && maxTokensOk;
        }

        private void Confirm()
        {
            if (IsValid)
                CloseRequest?.Invoke(true);
        }

        private void Cancel() => CloseRequest?.Invoke(false);
    }
}