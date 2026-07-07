using System.IO;
using System.Text.Json;

namespace TreeChat.Services
{
    public static class ApiConfig
    {
        public static string ApiKey = "";
        public static string PythonBackendUrl = "http://127.0.0.1:8800";
        public static string PythonProjectDir = "";
        public static string ApiEndpoint = "https://api.deepseek.com/chat/completions";
        public static string ModelName = "deepseek-v4";
        public static double Temperature = 0.7;
        public static double TopP = 0.8;
        public static int TopK = 20;
        public static int MaxTokens = 800;

        private static readonly string ConfigFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TreeChat", "api_config.json");

        /// <summary>
        /// 从文件加载配置，若文件不存在则使用默认值
        /// </summary>
        public static void LoadFromFile()
        {
            if (!File.Exists(ConfigFilePath))
            {
                SaveToFile(); // 创建默认配置文件
                return;
            }

            try
            {
                string json = File.ReadAllText(ConfigFilePath);
                var data = JsonSerializer.Deserialize<ApiConfigData>(json);
                if (data != null)
                {
                    ApiKey = data.ApiKey;
                    PythonBackendUrl = data.PythonBackendUrl;
                    PythonProjectDir = data.PythonProjectDir;
                    ApiEndpoint = data.ApiEndpoint;
                    ModelName = data.ModelName;
                    Temperature = data.Temperature;
                    TopP = data.TopP;
                    TopK = data.TopK;
                    MaxTokens = data.MaxTokens;
                }
            }
            catch (Exception ex)
            {
                // 日志或忽略异常，可保留现有内存中的值
                System.Diagnostics.Debug.WriteLine($"加载配置文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存当前配置到文件
        /// </summary>
        public static void SaveToFile()
        {
            try
            {
                var data = new ApiConfigData
                {
                    ApiKey = ApiKey,
                    PythonBackendUrl = PythonBackendUrl,
                    PythonProjectDir = PythonProjectDir,
                    ApiEndpoint = ApiEndpoint,
                    ModelName = ModelName,
                    Temperature = Temperature,
                    TopP = TopP,
                    TopK = TopK,
                    MaxTokens = MaxTokens,
                };
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                string directory = Path.GetDirectoryName(ConfigFilePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存配置文件失败: {ex.Message}");
            }
        }
    }
}