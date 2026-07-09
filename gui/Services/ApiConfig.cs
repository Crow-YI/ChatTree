using System.IO;
using System.Text.Json;

namespace TreeChat.Services
{
    /// <summary>
    /// 全局配置单例。持久化到项目根目录的 config.json。
    /// 写入时使用 snake_case 命名，与 Python 后端的 config.json 格式一致。
    /// 若 config.json 不存在，自动从 config.example.json 复制初始化。
    /// </summary>
    public static class ApiConfig
    {
        // ---- 用户配置字段 ----
        public static string ApiKey = "";
        public static string ApiEndpoint = "https://api.deepseek.com";
        public static string ModelName = "deepseek-v4-flash";
        public static double Temperature = 0.7;
        public static double TopP = 0.8;
        public static int MaxTokens = 800;

        // ---- 基础设施（不持久化到 config.json，使用代码默认值或自动检测）----
        public static string PythonBackendUrl = "http://127.0.0.1:8800";
        public static string PythonProjectDir = "";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>
        /// 查找项目根目录（先找 backend/，再取其父目录）。
        /// </summary>
        private static string? FindProjectRoot()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var dir = new DirectoryInfo(baseDir);

            for (int i = 0; i < 8; i++)
            {
                // 检查 <dir>/backend/pyproject.toml → 说明 dir 是项目根
                var backendDir = Path.Combine(dir.FullName, "backend");
                if (File.Exists(Path.Combine(backendDir, "pyproject.toml")))
                    return dir.FullName;

                // 检查 <dir>/py_version/backend/pyproject.toml → dir 是项目根
                var pyVerBackend = Path.Combine(dir.FullName, "py_version", "backend");
                if (File.Exists(Path.Combine(pyVerBackend, "pyproject.toml")))
                    return dir.FullName;

                if (dir.Parent == null) break;
                dir = dir.Parent;
            }

            return null;
        }

        /// <summary>
        /// config.json 的完整路径（项目根目录）。
        /// </summary>
        private static string? ConfigFilePath
        {
            get
            {
                var root = FindProjectRoot();
                return root != null ? Path.Combine(root, "config.json") : null;
            }
        }

        /// <summary>
        /// config.example.json 的完整路径（项目根目录）。
        /// </summary>
        private static string? ExampleFilePath
        {
            get
            {
                var root = FindProjectRoot();
                return root != null ? Path.Combine(root, "config.example.json") : null;
            }
        }

        /// <summary>
        /// 从 config.json 加载配置。
        /// 若文件不存在，优先从 config.example.json 复制，回退到代码默认值。
        /// </summary>
        public static void LoadFromFile()
        {
            var path = ConfigFilePath;
            if (path == null || !File.Exists(path))
            {
                TryCopyFromExample(path);
                if (path != null && !File.Exists(path))
                    SaveToFile(); // 兜底：用代码默认值创建
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("api_key", out var apiKey))
                    ApiKey = apiKey.GetString() ?? "";
                if (root.TryGetProperty("api_endpoint", out var endpoint))
                    ApiEndpoint = endpoint.GetString() ?? "https://api.deepseek.com";
                if (root.TryGetProperty("model", out var model))
                    ModelName = model.GetString() ?? "deepseek-v4-flash";
                if (root.TryGetProperty("temperature", out var temp))
                    Temperature = temp.GetDouble();
                if (root.TryGetProperty("top_p", out var topP))
                    TopP = topP.GetDouble();
                if (root.TryGetProperty("max_tokens", out var maxTokens))
                    MaxTokens = maxTokens.GetInt32();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载配置文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 尝试从 config.example.json 复制创建 config.json。
        /// </summary>
        private static void TryCopyFromExample(string? configPath)
        {
            if (configPath == null) return;

            var examplePath = ExampleFilePath;
            if (examplePath == null || !File.Exists(examplePath)) return;

            try
            {
                var dir = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.Copy(examplePath, configPath, overwrite: false);
                System.Diagnostics.Debug.WriteLine("已从 config.example.json 初始化 config.json");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"从 example 复制配置文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存当前配置到 config.json。
        /// </summary>
        public static void SaveToFile()
        {
            var path = ConfigFilePath;
            if (path == null) return;

            try
            {
                var data = new
                {
                    api_key = ApiKey,
                    api_endpoint = ApiEndpoint,
                    model = ModelName,
                    temperature = Temperature,
                    top_p = TopP,
                    max_tokens = MaxTokens,
                };

                string json = JsonSerializer.Serialize(data, JsonOptions);
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存配置文件失败: {ex.Message}");
            }
        }
    }
}
