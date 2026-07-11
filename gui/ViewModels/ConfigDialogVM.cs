using TreeChat.Commands;
using TreeChat.Infrastructure;
using TreeChat.Models;
using TreeChat.Services;

namespace TreeChat.ViewModels
{
    public class ConfigDialogViewModel : BaseViewModel
    {
        private readonly bool _isNew; // true = 创建新配置, false = 编辑已有配置
        private readonly string? _originalName; // 编辑时记录原名
        private bool _isLoading; // 初始化时阻止自动填充副作用

        // ---- Profile 字段 ----

        private string _profileName;
        public string ProfileName
        {
            get => _profileName;
            set
            {
                if (SetProperty(ref _profileName, value))
                    ValidateAll();
            }
        }

        private int _providerIndex;
        public int ProviderIndex
        {
            get => _providerIndex;
            set
            {
                if (SetProperty(ref _providerIndex, value) && !_isLoading)
                    OnProviderChanged();
            }
        }

        public string[] ProviderOptions { get; } = { "deepseek", "openai", "other" };

        public string SelectedProvider => ProviderOptions[ProviderIndex];

        private bool _isEndpointReadOnly = true;
        public bool IsEndpointReadOnly
        {
            get => _isEndpointReadOnly;
            set => SetProperty(ref _isEndpointReadOnly, value);
        }

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

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public RelayCommand ConfirmCommand { get; }
        public RelayCommand CancelCommand { get; }

        public event Action<bool?> CloseRequest;

        /// <summary>
        /// 构造函数。
        /// </summary>
        /// <param name="existing">null 表示新建，非 null 表示编辑已有 profile</param>
        public ConfigDialogViewModel(ProfileData? existing = null)
        {
            _isNew = existing == null;
            _originalName = existing?.Name;
            _isLoading = true;

            if (existing != null)
            {
                // 编辑模式：从已有 profile 填充
                _profileName = existing.Name;

                // 供应商选择：已知供应商映射到对应索引，否则选 "other"
                int idx = System.Array.IndexOf(ProviderOptions, existing.Provider);
                _providerIndex = idx >= 0 ? idx : 2; // 2 = other

                _apiKey = existing.ApiKey;
                _modelName = existing.Model;
                Temperature = existing.Temperature;
                TopP = existing.TopP;
                MaxTokens = existing.MaxTokens;
                _temperatureText = existing.Temperature.ToString();
                _topPText = existing.TopP.ToString();
                _maxTokensText = existing.MaxTokens.ToString();

                // API 站点：已知供应商自动填充，其他保留原值
                if (_providerIndex < 2)
                {
                    _apiEndpoint = GetDefaultEndpoint(_providerIndex);
                    _isEndpointReadOnly = true;
                }
                else
                {
                    _apiEndpoint = existing.ApiEndpoint;
                    _isEndpointReadOnly = false;
                }
            }
            else
            {
                // 新建模式：默认 deepseek，自动填充端点
                _profileName = "";
                _providerIndex = 0;
                _apiKey = ApiConfig.ApiKey;
                _apiEndpoint = GetDefaultEndpoint(0);
                _isEndpointReadOnly = true;
                _modelName = ApiConfig.ModelName;
                Temperature = ApiConfig.Temperature;
                TopP = ApiConfig.TopP;
                MaxTokens = ApiConfig.MaxTokens;
                _temperatureText = ApiConfig.Temperature.ToString();
                _topPText = ApiConfig.TopP.ToString();
                _maxTokensText = ApiConfig.MaxTokens.ToString();
            }

            _isLoading = false;
            ValidateAll();
            ConfirmCommand = new RelayCommand(async _ => await ConfirmAsync(), _ => IsValid);
            CancelCommand = new RelayCommand(_ => Cancel());
        }

        /// <summary>
        /// 当供应商选择改变时，自动填充/清空 API 站点。
        /// </summary>
        private void OnProviderChanged()
        {
            if (_providerIndex < 2)
            {
                // 已知供应商：自动填充，只读
                ApiEndpoint = GetDefaultEndpoint(_providerIndex);
                IsEndpointReadOnly = true;
            }
            else
            {
                // "other"：清空，可编辑
                ApiEndpoint = "";
                IsEndpointReadOnly = false;
            }
        }

        private static string GetDefaultEndpoint(int index) => index switch
        {
            0 => "https://api.deepseek.com",
            1 => "https://api.openai.com",
            _ => "",
        };

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
            bool nameOk = !string.IsNullOrWhiteSpace(ProfileName);
            bool tempOk = double.TryParse(TemperatureText, out double t) && t >= 0 && t <= 2;
            bool topPOk = double.TryParse(TopPText, out double p) && p >= 0 && p <= 1;
            bool maxTokensOk = int.TryParse(MaxTokensText, out int m) && m >= 1 && m <= 8192;
            IsValid = nameOk && tempOk && topPOk && maxTokensOk;
        }

        private async Task ConfirmAsync()
        {
            if (!IsValid) return;

            var profile = new ProfileData
            {
                Name = ProfileName.Trim(),
                Provider = SelectedProvider,
                ApiKey = ApiKey,
                ApiEndpoint = ApiEndpoint,
                Model = ModelName,
                Temperature = Temperature,
                TopP = TopP,
                MaxTokens = MaxTokens,
            };

            try
            {
                if (_isNew)
                {
                    await App.Backend.CreateProfileAsync(profile);
                    AppLogger.Info("Profile created: {Name}", profile.Name);
                }
                else
                {
                    // 编辑时使用原名（可能已修改名称）
                    string targetName = _originalName ?? profile.Name;
                    await App.Backend.UpdateProfileAsync(targetName, profile);
                    AppLogger.Info("Profile updated: {Name}", profile.Name);
                }

                // 同步本地配置
                ApiConfig.LoadFromFile();

                CloseRequest?.Invoke(true);
            }
            catch (Exception ex)
            {
                StatusMessage = $"保存失败: {ex.Message}";
                AppLogger.Warn(ex, "Failed to save profile");
            }
        }

        private void Cancel() => CloseRequest?.Invoke(false);
    }
}
