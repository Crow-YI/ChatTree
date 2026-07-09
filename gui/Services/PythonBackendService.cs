using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using TreeChat.Models;

namespace TreeChat.Services
{
    /// <summary>
    /// Python 后端进程管理 + HTTP 通信 + SSE 流解析。
    /// </summary>
    public class PythonBackendService : IDisposable
    {
        private Process? _pythonProcess;
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _pyProjectDir;
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };

        public PythonBackendService(string baseUrl = "http://127.0.0.1:8800",
                                     string? pyProjectDir = null)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            // 自动检测 Python 后端目录
            _pyProjectDir = pyProjectDir ?? AutoDetectBackendDir();
        }

        /// <summary>
        /// 自动查找 backend/ 目录（从程序集位置向上遍历，查找 backend/pyproject.toml）。
        /// </summary>
        private static string AutoDetectBackendDir()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var searched = new List<string>();
            var dir = baseDir;

            // 向上最多查找 8 层
            for (int i = 0; i < 8; i++)
            {
                // 检查: <dir>/backend/pyproject.toml
                var direct = Path.Combine(dir, "backend");
                searched.Add(direct);
                if (File.Exists(Path.Combine(direct, "pyproject.toml")))
                    return direct;

                // 检查: <dir>/py_version/backend/pyproject.toml
                var viaPyVersion = Path.Combine(dir, "py_version", "backend");
                searched.Add(viaPyVersion);
                if (File.Exists(Path.Combine(viaPyVersion, "pyproject.toml")))
                    return viaPyVersion;

                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }

            // 回退：相对路径
            var fallback = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "backend"));
            throw new DirectoryNotFoundException(
                $"找不到 Python 后端目录。已搜索以下路径:\n{string.Join("\n", searched)}\n" +
                $"程序集目录: {baseDir}");
        }

        // ==================== 生命周期 ====================

        /// <summary>
        /// 启动 Python 后端进程，等待就绪。
        /// 如果检测到后端已在运行，则直接返回。
        /// </summary>
        public async Task<bool> StartAsync(CancellationToken ct = default)
        {
            // 先检查是否已经在运行
            if (await HealthCheckAsync())
            {
                System.Diagnostics.Debug.WriteLine("Python 后端已在运行，跳过启动。");
                return true;
            }

            // 查找 uv 可执行文件
            string? uvPath = FindUvPath();
            if (uvPath == null)
            {
                throw new InvalidOperationException(
                    "未找到 uv。请先安装 uv: https://docs.astral.sh/uv/getting-started/installation/");
            }

            // 检查项目目录
            if (!Directory.Exists(_pyProjectDir))
            {
                throw new DirectoryNotFoundException(
                    $"Python 项目目录不存在: {_pyProjectDir}");
            }

            var psi = new ProcessStartInfo
            {
                FileName = uvPath,
                Arguments = "run uvicorn src.main:app --host 127.0.0.1 --port 8800",
                WorkingDirectory = _pyProjectDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            _pythonProcess = new Process { StartInfo = psi };
            _pythonProcess.Start();

            // 等待 health check（最多 15 秒）
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < 15)
            {
                if (_pythonProcess.HasExited)
                {
                    var stderr = await _pythonProcess.StandardError.ReadToEndAsync();
                    var stdout = await _pythonProcess.StandardOutput.ReadToEndAsync();
                    throw new InvalidOperationException(
                        $"Python 进程意外退出 (exit code: {_pythonProcess.ExitCode})。\n" +
                        $"uv: {uvPath}\n" +
                        $"工作目录: {_pyProjectDir}\n" +
                        $"错误输出: {stderr}\n" +
                        $"标准输出: {stdout}");
                }

                if (await HealthCheckAsync())
                {
                    return true;
                }

                await Task.Delay(500, ct);
            }

            throw new TimeoutException("Python 后端启动超时（15秒）。");
        }

        /// <summary>
        /// 优雅停止 Python 后端（无论由谁启动）。
        /// </summary>
        public async Task StopAsync()
        {
            // 1. 始终尝试发送 shutdown 请求
            try
            {
                await PostAsync<ApiSuccessResponse>("/api/v1/shutdown");
            }
            catch
            {
                // 忽略 shutdown 请求中的错误（可能已经关闭）
            }

            // 2. 如果 WPF 管理了进程，等待并清理
            if (_pythonProcess == null || _pythonProcess.HasExited)
                return;

            var sw = Stopwatch.StartNew();
            while (!_pythonProcess.HasExited && sw.Elapsed.TotalSeconds < 3)
            {
                await Task.Delay(200);
            }

            // 若仍未退出则强制结束
            if (!_pythonProcess.HasExited)
            {
                try { _pythonProcess.Kill(); } catch { }
            }
        }

        public async Task<bool> HealthCheckAsync()
        {
            try
            {
                var result = await GetAsync<Dictionary<string, object>>("/api/v1/health");
                return result != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 在常见安装位置搜索 uv，返回完整路径（含 .exe）。
        /// 搜索顺序：PATH → 已知安装位置。
        /// </summary>
        private static string? FindUvPath()
        {
            // 已知安装位置
            var knownLocations = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "uv.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cargo", "bin", "uv.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "uv", "uv.exe"),
            };

            foreach (var loc in knownLocations)
            {
                if (File.Exists(loc))
                    return loc;
            }

            // 回退：尝试 PATH 中的 "uv"
            if (IsCommandAvailable("uv"))
                return "uv";

            return null;
        }

        private static bool IsCommandAvailable(string command)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                var proc = Process.Start(psi);
                if (proc == null) return false;
                proc.WaitForExit(3000);
                return proc.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        // ==================== Tree CRUD ====================

        public Task<TreeDetailResponse> CreateTreeAsync(string title, string systemPrompt)
        {
            return PostAsync<TreeDetailResponse>("/api/v1/trees",
                new CreateTreeRequest { Title = title, SystemPrompt = systemPrompt });
        }

        public Task<TreeListResponse> ListTreesAsync()
        {
            return GetAsync<TreeListResponse>("/api/v1/trees")!;
        }

        public Task<TreeDetailResponse> GetTreeAsync(string treeId)
        {
            return GetAsync<TreeDetailResponse>($"/api/v1/trees/{treeId}")!;
        }

        public Task<ApiSuccessResponse> DeleteTreeAsync(string treeId)
        {
            return DeleteAsync<ApiSuccessResponse>($"/api/v1/trees/{treeId}");
        }

        public Task<ApiSuccessResponse> RenameTreeAsync(string treeId, string newTitle)
        {
            return PutAsync<ApiSuccessResponse>($"/api/v1/trees/{treeId}",
                new RenameTreeRequest { Title = newTitle });
        }

        // ==================== Chat (SSE 流式) ====================

        /// <summary>
        /// 流式聊天。返回 SSE 事件流。
        /// </summary>
        public async IAsyncEnumerable<SseEvent> ChatStreamAsync(
            string treeId, int parentNodeId, string message,
            ChatConfigData? config = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var requestBody = new ChatRequest
            {
                ParentNodeId = parentNodeId,
                Message = message,
            };

            if (config != null)
            {
                requestBody.Model = config.Model;
                requestBody.Temperature = config.Temperature;
                requestBody.TopP = config.TopP;
                requestBody.MaxTokens = config.MaxTokens;
            }

            var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
            var request = new HttpRequestMessage(HttpMethod.Post,
                $"{_baseUrl}/api/v1/trees/{treeId}/chat")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue("text/event-stream"));

            var response = await _httpClient.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);

            string? eventType = null;
            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line == null) break;

                if (line.StartsWith("event: "))
                {
                    eventType = line[7..].Trim();
                }
                else if (line.StartsWith("data: ") && eventType != null)
                {
                    yield return new SseEvent
                    {
                        EventType = eventType,
                        Data = line[6..],
                    };
                    eventType = null;
                }
            }
        }

        // ==================== Node Operations ====================

        public Task<ApiSuccessResponse> RenameNodeAsync(string treeId, int nodeId, string name)
        {
            return PutAsync<ApiSuccessResponse>($"/api/v1/trees/{treeId}/nodes/{nodeId}",
                new RenameNodeRequest { Name = name });
        }

        public Task<ApiSuccessResponse> DeleteNodeAsync(string treeId, int nodeId)
        {
            return DeleteAsync<ApiSuccessResponse>($"/api/v1/trees/{treeId}/nodes/{nodeId}");
        }

        // ==================== Config ====================

        public Task<ChatConfigData> GetConfigAsync()
        {
            return GetAsync<ChatConfigData>("/api/v1/config")!;
        }

        public Task<ApiSuccessResponse> PushConfigAsync(ChatConfigData config)
        {
            return PutAsync<ApiSuccessResponse>("/api/v1/config", config);
        }

        // ==================== File Serialization ====================

        public Task<SerializeResponse> SerializeTreeAsync(string treeId)
        {
            return GetAsync<SerializeResponse>($"/api/v1/trees/{treeId}/serialize")!;
        }

        public Task<TreeDetailResponse> DeserializeTreeAsync(string jsonContent, string? title = null)
        {
            return PostAsync<TreeDetailResponse>("/api/v1/trees/deserialize",
                new DeserializeRequest { JsonContent = jsonContent, Title = title });
        }

        // ==================== HTTP Helpers ====================

        private async Task<T> GetAsync<T>(string path) where T : class
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}{path}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json, _jsonOptions)!;
        }

        private async Task<T> PostAsync<T>(string path, object? body = null) where T : class
        {
            var content = body != null
                ? new StringContent(JsonSerializer.Serialize(body, _jsonOptions),
                    Encoding.UTF8, "application/json")
                : null;
            var response = await _httpClient.PostAsync($"{_baseUrl}{path}", content);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json, _jsonOptions)!;
        }

        private async Task<T> PutAsync<T>(string path, object body) where T : class
        {
            var content = new StringContent(JsonSerializer.Serialize(body, _jsonOptions),
                Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync($"{_baseUrl}{path}", content);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json, _jsonOptions)!;
        }

        private async Task<T> DeleteAsync<T>(string path) where T : class
        {
            var response = await _httpClient.DeleteAsync($"{_baseUrl}{path}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json, _jsonOptions)!;
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            _pythonProcess?.Dispose();
        }
    }

    /// <summary>
    /// SSE 事件结构。
    /// </summary>
    public class SseEvent
    {
        public string EventType { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
    }
}
