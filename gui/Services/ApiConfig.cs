using System.IO;
using System.Text.Json;
using TreeChat.Infrastructure;

namespace TreeChat.Services
{
    /// <summary>
    /// 全局配置单例。持久化到项目根目录的 config.json。
    /// 支持 v2 profiles 格式（多个配置画像）和 v1 旧平面格式自动迁移。
    /// </summary>
    public static class ApiConfig
    {
        // ---- Profile 数据（v2） ----
        public static List<ProfileData> Profiles { get; private set; } = new();
        public static string ActiveProfileName { get; set; } = "default";

        // ---- 旧有字段（v1 兼容，从激活 profile 同步填充）----
        public static string ApiKey = "";
        public static string ApiEndpoint = "https://api.deepseek.com";
        public static string ModelName = "deepseek-v4-flash";
        public static double Temperature = 0.7;
        public static double TopP = 0.8;
        public static int MaxTokens = 800;
        public static bool EnableDebugLogging = false;

        // ---- 基础设施（不持久化到 config.json，使用代码默认值或自动检测）----
        public static string PythonBackendUrl = "http://127.0.0.1:8800";
        public static string PythonProjectDir = "";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
        };

        // ---- 内部 Profile 数据类（无 JSON 属性，与 ApiModels.ProfileData 区分）----

        public class ProfileData
        {
            public string Name { get; set; } = "default";
            public string Provider { get; set; } = "deepseek";
            public string ApiKey { get; set; } = "";
            public string ApiEndpoint { get; set; } = "https://api.deepseek.com";
            public string Model { get; set; } = "deepseek-v4-flash";
            public double Temperature { get; set; } = 0.7;
            public double TopP { get; set; } = 0.8;
            public int MaxTokens { get; set; } = 800;
        }

        /// <summary>
        /// 获取当前激活的 profile。
        /// </summary>
        public static ProfileData? GetActiveProfile() =>
            Profiles.FirstOrDefault(p => p.Name == ActiveProfileName);

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
        /// 自动检测 v1（平面格式）并迁移到 v2（profiles 格式）。
        /// </summary>
        public static void LoadFromFile()
        {
            var path = ConfigFilePath;
            if (path == null || !File.Exists(path))
            {
                AppLogger.Info("Config file not found: {Path}", path);
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

                // 检测格式：有 "profiles" 键 → v2；否则 → v1 迁移
                if (root.TryGetProperty("profiles", out _))
                {
                    LoadV2Format(root);
                }
                else
                {
                    AppLogger.Info("Detected v1 config format, migrating to v2…");
                    MigrateV1ToV2(root, path);
                    // 迁移后重新加载
                    json = File.ReadAllText(path);
                    using var doc2 = JsonDocument.Parse(json);
                    LoadV2Format(doc2.RootElement);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn(ex, "Failed to load config from {Path}", path);
            }
        }

        /// <summary>
        /// 解析 v2 格式（profiles 数组 + active_profile）。
        /// </summary>
        private static void LoadV2Format(JsonElement root)
        {
            if (root.TryGetProperty("active_profile", out var activeElem))
                ActiveProfileName = activeElem.GetString() ?? "default";

            if (root.TryGetProperty("profiles", out var profilesElem))
            {
                Profiles.Clear();
                foreach (var p in profilesElem.EnumerateArray())
                {
                    Profiles.Add(new ProfileData
                    {
                        Name = p.TryGetProperty("name", out var n) ? n.GetString() ?? "default" : "default",
                        Provider = p.TryGetProperty("provider", out var prv) ? prv.GetString() ?? "deepseek" : "deepseek",
                        ApiKey = p.TryGetProperty("api_key", out var ak) ? ak.GetString() ?? "" : "",
                        ApiEndpoint = p.TryGetProperty("api_endpoint", out var ep) ? ep.GetString() ?? "https://api.deepseek.com" : "https://api.deepseek.com",
                        Model = p.TryGetProperty("model", out var m) ? m.GetString() ?? "deepseek-v4-flash" : "deepseek-v4-flash",
                        Temperature = p.TryGetProperty("temperature", out var t) ? t.GetDouble() : 0.7,
                        TopP = p.TryGetProperty("top_p", out var tp) ? tp.GetDouble() : 0.8,
                        MaxTokens = p.TryGetProperty("max_tokens", out var mt) ? mt.GetInt32() : 800,
                    });
                }
            }

            // 确保 active_profile 有效
            if (!Profiles.Any(p => p.Name == ActiveProfileName) && Profiles.Count > 0)
                ActiveProfileName = Profiles[0].Name;

            // 同步到旧有字段
            SyncActiveToLegacyFields();

            var masked = string.IsNullOrEmpty(ApiKey)
                ? "(empty)"
                : new string('*', ApiKey.Length - 4) + ApiKey[^4..];
            AppLogger.Info(
                "Config loaded (v2): profiles={Count} active={Active} apiKey={ApiKey}",
                Profiles.Count, ActiveProfileName, masked);
        }

        /// <summary>
        /// 将 v1 平面配置迁移到 v2 profiles 格式并写回。
        /// </summary>
        private static void MigrateV1ToV2(JsonElement root, string configPath)
        {
            var apiKey = root.TryGetProperty("api_key", out var ak) ? ak.GetString() ?? "" : "";
            var apiEndpoint = root.TryGetProperty("api_endpoint", out var ep) ? ep.GetString() ?? "https://api.deepseek.com" : "https://api.deepseek.com";
            var model = root.TryGetProperty("model", out var m) ? m.GetString() ?? "deepseek-v4-flash" : "deepseek-v4-flash";
            var temperature = root.TryGetProperty("temperature", out var t) ? t.GetDouble() : 0.7;
            var topP = root.TryGetProperty("top_p", out var tp) ? tp.GetDouble() : 0.8;
            var maxTokens = root.TryGetProperty("max_tokens", out var mt) ? mt.GetInt32() : 800;

            var profiles = new[]
            {
                new
                {
                    name = "default",
                    provider = "deepseek",
                    api_key = apiKey,
                    api_endpoint = apiEndpoint,
                    model = model,
                    temperature = temperature,
                    top_p = topP,
                    max_tokens = maxTokens,
                }
            };

            var newData = new
            {
                version = 2,
                active_profile = "default",
                profiles = profiles,
            };

            try
            {
                string json = JsonSerializer.Serialize(newData, JsonOptions);
                File.WriteAllText(configPath, json);
                AppLogger.Info("Migrated config.json v1 → v2");
            }
            catch (Exception ex)
            {
                AppLogger.Warn(ex, "Failed to write migrated config.json");
            }
        }

        /// <summary>
        /// 将激活 profile 的值同步到旧有静态字段。
        /// </summary>
        private static void SyncActiveToLegacyFields()
        {
            var active = GetActiveProfile();
            if (active == null) return;

            ApiKey = active.ApiKey;
            ApiEndpoint = active.ApiEndpoint;
            ModelName = active.Model;
            Temperature = active.Temperature;
            TopP = active.TopP;
            MaxTokens = active.MaxTokens;
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
                AppLogger.Info("Initialized config.json from config.example.json");
            }
            catch (Exception ex)
            {
                AppLogger.Warn(ex, "Failed to copy config.example.json");
            }
        }

        /// <summary>
        /// 保存当前配置到 config.json（v2 格式）。
        /// </summary>
        public static void SaveToFile()
        {
            var path = ConfigFilePath;
            if (path == null) return;

            try
            {
                // 确保 Profiles 列表中至少有一个 profile
                if (Profiles.Count == 0)
                {
                    Profiles.Add(new ProfileData
                    {
                        Name = "default",
                        Provider = "deepseek",
                        ApiKey = ApiKey,
                        ApiEndpoint = ApiEndpoint,
                        Model = ModelName,
                        Temperature = Temperature,
                        TopP = TopP,
                        MaxTokens = MaxTokens,
                    });
                    ActiveProfileName = "default";
                }

                var data = new
                {
                    version = 2,
                    active_profile = ActiveProfileName,
                    profiles = Profiles.Select(p => new
                    {
                        name = p.Name,
                        provider = p.Provider,
                        api_key = p.ApiKey,
                        api_endpoint = p.ApiEndpoint,
                        model = p.Model,
                        temperature = p.Temperature,
                        top_p = p.TopP,
                        max_tokens = p.MaxTokens,
                    }).ToList(),
                };

                string json = JsonSerializer.Serialize(data, JsonOptions);
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(path, json);
                AppLogger.Info("Config saved to: {Path} (profiles={Count})", path, Profiles.Count);
            }
            catch (Exception ex)
            {
                AppLogger.Warn(ex, "Failed to save config to {Path}", path);
            }
        }
    }
}
