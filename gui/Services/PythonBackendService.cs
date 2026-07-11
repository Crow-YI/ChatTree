using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using TreeChat.Infrastructure;
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
        private bool _isStartingUp;
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };

        /// <summary>
        /// 后端进程意外退出时触发。
        /// </summary>
        public event Action<int?>? ProcessExited;

        /// <summary>
        /// 后端是否正在运行。
        /// </summary>
        public bool IsRunning =>
            _pythonProcess != null && !_pythonProcess.HasExited;

        public PythonBackendService(string baseUrl = "http://127.0.0.1:8800",
                                     string? pyProjectDir = null)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            // 自动检测 Python 后端目录（空字符串视为未指定，触发自动检测）
            _pyProjectDir = string.IsNullOrEmpty(pyProjectDir)
                ? AutoDetectBackendDir()
                : pyProjectDir;
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
            _isStartingUp = false;

            // 如果有旧进程在运行，先清理（避免僵进程干扰）
            await TryKillExistingProcessAsync();

            // 清除之前监控的进程引用
            DetachProcessMonitor();

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

            // 清除系统环境变量干扰，移除 VIRTUAL_ENV，避免 uv 混淆
            psi.EnvironmentVariables.Remove("VIRTUAL_ENV");

            _pythonProcess = new Process { StartInfo = psi };

            // 注册进程退出监控
            _pythonProcess.EnableRaisingEvents = true;
            _pythonProcess.Exited += OnProcessExited;

            // 标记启动中，防止 ProcessExited 在启动阶段误报
            _isStartingUp = true;

            AppLogger.Info("Starting backend process: uv={UvPath} dir={WorkingDir}", uvPath, _pyProjectDir);
            _pythonProcess.Start();

            // 等待 health check（首次 uv 安装依赖可能较慢，最多 60 秒）
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < 60)
            {
                if (_pythonProcess.HasExited)
                {
                    _isStartingUp = false;
                    var stderr = await _pythonProcess.StandardError.ReadToEndAsync();
                    var stdout = await _pythonProcess.StandardOutput.ReadToEndAsync();
                    AppLogger.Error(
                        "Backend process exited during startup (code={ExitCode}): {StdErr}",
                        _pythonProcess.ExitCode, stderr);
                    throw new InvalidOperationException(
                        $"Python 进程意外退出 (exit code: {_pythonProcess.ExitCode})。\n" +
                        $"uv: {uvPath}\n" +
                        $"工作目录: {_pyProjectDir}\n" +
                        $"错误输出: {stderr}\n" +
                        $"标准输出: {stdout}");
                }

                if (await HealthCheckAsync())
                {
                    _isStartingUp = false;
                    AppLogger.Info("Backend started in {Elapsed}ms", sw.ElapsedMilliseconds);
                    return true;
                }

                await Task.Delay(500, ct);
            }

            _isStartingUp = false;
            AppLogger.Error("Backend start timed out after 60s");
            throw new TimeoutException("Python 后端启动超时（60秒）。首次运行可能较慢，请稍后重试。");
        }

        /// <summary>
        /// 尝试释放 8800 端口 — 先发 shutdown 请求，若无效则尝试通过 netstat+taskkill 强制清理。
        /// </summary>
        private async Task TryKillExistingProcessAsync()
        {
            // 方式一：发送优雅 shutdown 请求
            try
            {
                await PostAsync<ApiSuccessResponse>("/api/v1/shutdown");
                AppLogger.Info("Sent shutdown request to existing backend");
                await Task.Delay(1000);
                return;
            }
            catch
            {
                // 没有响应 shutdown 的进程
            }

            // 方式二：通过 netstat 查找 8800 端口的进程并强制终止
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "netstat",
                    Arguments = "-ano | findstr :8800",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var proc = Process.Start(psi);
                if (proc == null) return;
                var output = await proc.StandardOutput.ReadToEndAsync();
                proc.WaitForExit(3000);

                foreach (var line in output.Split('\n'))
                {
                    if (line.Contains("LISTENING"))
                    {
                        var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 4 && int.TryParse(parts[4], out var pid))
                        {
                            try
                            {
                                Process.GetProcessById(pid).Kill();
                                AppLogger.Info("Force-killed process on port 8800 (PID={Pid})", pid);
                                await Task.Delay(500);
                            }
                            catch { /* 进程可能已结束或权限不足 */ }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn(ex, "Failed to clean up port 8800");
            }
        }

        /// <summary>
        /// 进程意外退出处理。
        /// </summary>
        private void OnProcessExited(object? sender, EventArgs e)
        {
            var exitCode = _pythonProcess?.ExitCode;
            var stderr = "";
            try
            {
                if (_pythonProcess != null && !_pythonProcess.StandardError.EndOfStream)
                    stderr = _pythonProcess.StandardError.ReadToEnd();
            }
            catch { /* stderr 可能已被消费 */ }

            AppLogger.Warn(
                "Backend process exited (code={ExitCode}): {StdErr}",
                exitCode, stderr);

            // 启动阶段的退出由 StartAsync 的轮询逻辑处理，不重复通知
            if (!_isStartingUp)
                ProcessExited?.Invoke(exitCode);
        }

        /// <summary>
        /// 断开进程退出事件监听。
        /// </summary>
        private void DetachProcessMonitor()
        {
            if (_pythonProcess != null)
            {
                _pythonProcess.Exited -= OnProcessExited;
            }
        }

        /// <summary>
        /// 尝试重启后端进程。返回 true 表示重启成功。
        /// </summary>
        public async Task<bool> TryRestartAsync(CancellationToken ct = default)
        {
            AppLogger.Info("Attempting backend restart...");
            try { await StopAsync(); } catch { }
            try { return await StartAsync(ct); }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Backend restart failed");
                return false;
            }
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
                AppLogger.Info("Shutdown request sent to backend");
            }
            catch
            {
                // 忽略 shutdown 请求中的错误（可能已经关闭）
            }

            // 2. 如果 WPF 管理了进程，等待并清理
            if (_pythonProcess == null || _pythonProcess.HasExited)
            {
                DetachProcessMonitor();
                return;
            }

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

            DetachProcessMonitor();
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
            AppLogger.Debug("CreateTree: title={Title}", title);
            return PostAsync<TreeDetailResponse>("/api/v1/trees",
                new CreateTreeRequest { Title = title, SystemPrompt = systemPrompt });
        }

        public Task<TreeListResponse> ListTreesAsync()
        {
            return GetAsync<TreeListResponse>("/api/v1/trees")!;
        }

        public Task<TreeDetailResponse> GetTreeAsync(string treeId)
        {
            AppLogger.Debug("GetTree: id={TreeId}", treeId);
            return GetAsync<TreeDetailResponse>($"/api/v1/trees/{treeId}")!;
        }

        public Task<ApiSuccessResponse> DeleteTreeAsync(string treeId)
        {
            AppLogger.Info("DeleteTree: id={TreeId}", treeId);
            return DeleteAsync<ApiSuccessResponse>($"/api/v1/trees/{treeId}");
        }

        public Task<ApiSuccessResponse> RenameTreeAsync(string treeId, string newTitle)
        {
            AppLogger.Info("RenameTree: id={TreeId} title={Title}", treeId, newTitle);
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
            string? profileName = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var requestBody = new ChatRequest
            {
                ParentNodeId = parentNodeId,
                Message = message,
                ProfileName = profileName,
            };

            if (config != null)
            {
                requestBody.Model = config.Model;
                requestBody.Temperature = config.Temperature;
                requestBody.TopP = config.TopP;
                requestBody.MaxTokens = config.MaxTokens;
            }

            AppLogger.Info("ChatStream start: tree={TreeId} parent={ParentId}", treeId, parentNodeId);

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
            AppLogger.Info("RenameNode: tree={TreeId} node={NodeId} name={Name}", treeId, nodeId, name);
            return PutAsync<ApiSuccessResponse>($"/api/v1/trees/{treeId}/nodes/{nodeId}",
                new RenameNodeRequest { Name = name });
        }

        public Task<ApiSuccessResponse> DeleteNodeAsync(string treeId, int nodeId)
        {
            AppLogger.Info("DeleteNode: tree={TreeId} node={NodeId}", treeId, nodeId);
            return DeleteAsync<ApiSuccessResponse>($"/api/v1/trees/{treeId}/nodes/{nodeId}");
        }

        // ==================== Config ====================

        public Task<ChatConfigData> GetConfigAsync()
        {
            return GetAsync<ChatConfigData>("/api/v1/config")!;
        }

        public Task<ApiSuccessResponse> PushConfigAsync(ChatConfigData config)
        {
            AppLogger.Debug("PushConfig: model={Model}", config.Model);
            return PutAsync<ApiSuccessResponse>("/api/v1/config", config);
        }

        // ==================== Profile Management ====================

        public Task<ProfileListResponse> GetProfilesAsync()
        {
            return GetAsync<ProfileListResponse>("/api/v1/profiles")!;
        }

        public Task<ProfileData> GetProfileAsync(string name)
        {
            AppLogger.Debug("GetProfile: name={Name}", name);
            return GetAsync<ProfileData>($"/api/v1/profiles/{name}")!;
        }

        public Task<ProfileData> CreateProfileAsync(ProfileData profile)
        {
            AppLogger.Info("CreateProfile: name={Name} provider={Provider}", profile.Name, profile.Provider);
            return PostAsync<ProfileData>("/api/v1/profiles", profile);
        }

        public Task<ProfileData> UpdateProfileAsync(string name, ProfileData profile)
        {
            AppLogger.Info("UpdateProfile: name={Name}", name);
            return PutAsync<ProfileData>($"/api/v1/profiles/{name}", profile);
        }

        public Task<ApiSuccessResponse> DeleteProfileAsync(string name)
        {
            AppLogger.Info("DeleteProfile: name={Name}", name);
            return DeleteAsync<ApiSuccessResponse>($"/api/v1/profiles/{name}");
        }

        public Task<ActivateProfileResponse> ActivateProfileAsync(string name)
        {
            AppLogger.Info("ActivateProfile: name={Name}", name);
            return PutAsync<ActivateProfileResponse>($"/api/v1/profiles/{name}/activate", new { });
        }

        // ==================== File Serialization ====================

        public Task<SerializeResponse> SerializeTreeAsync(string treeId)
        {
            AppLogger.Debug("SerializeTree: id={TreeId}", treeId);
            return GetAsync<SerializeResponse>($"/api/v1/trees/{treeId}/serialize")!;
        }

        public Task<TreeDetailResponse> DeserializeTreeAsync(string jsonContent, string? title = null)
        {
            AppLogger.Info("DeserializeTree: title={Title}", title);
            return PostAsync<TreeDetailResponse>("/api/v1/trees/deserialize",
                new DeserializeRequest { JsonContent = jsonContent, Title = title });
        }

        // ==================== HTTP Helpers ====================

        private async Task<T> GetAsync<T>(string path) where T : class
        {
            AppLogger.Debug("GET {Path}", path);
            var sw = Stopwatch.StartNew();
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}{path}");
                sw.Stop();
                AppLogger.Debug("GET {Path} → {Status} in {Elapsed}ms",
                    path, (int)response.StatusCode, sw.ElapsedMilliseconds);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(json, _jsonOptions)!;
            }
            catch (Exception ex)
            {
                sw.Stop();
                AppLogger.Error(ex, "GET {Path} failed after {Elapsed}ms", path, sw.ElapsedMilliseconds);
                throw;
            }
        }

        private async Task<T> PostAsync<T>(string path, object? body = null) where T : class
        {
            AppLogger.Debug("POST {Path}", path);
            var sw = Stopwatch.StartNew();
            try
            {
                var content = body != null
                    ? new StringContent(JsonSerializer.Serialize(body, _jsonOptions),
                        Encoding.UTF8, "application/json")
                    : null;
                var response = await _httpClient.PostAsync($"{_baseUrl}{path}", content);
                sw.Stop();
                AppLogger.Debug("POST {Path} → {Status} in {Elapsed}ms",
                    path, (int)response.StatusCode, sw.ElapsedMilliseconds);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(json, _jsonOptions)!;
            }
            catch (Exception ex)
            {
                sw.Stop();
                AppLogger.Error(ex, "POST {Path} failed after {Elapsed}ms", path, sw.ElapsedMilliseconds);
                throw;
            }
        }

        private async Task<T> PutAsync<T>(string path, object body) where T : class
        {
            AppLogger.Debug("PUT {Path}", path);
            var sw = Stopwatch.StartNew();
            try
            {
                var content = new StringContent(JsonSerializer.Serialize(body, _jsonOptions),
                    Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync($"{_baseUrl}{path}", content);
                sw.Stop();
                AppLogger.Debug("PUT {Path} → {Status} in {Elapsed}ms",
                    path, (int)response.StatusCode, sw.ElapsedMilliseconds);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(json, _jsonOptions)!;
            }
            catch (Exception ex)
            {
                sw.Stop();
                AppLogger.Error(ex, "PUT {Path} failed after {Elapsed}ms", path, sw.ElapsedMilliseconds);
                throw;
            }
        }

        private async Task<T> DeleteAsync<T>(string path) where T : class
        {
            AppLogger.Debug("DELETE {Path}", path);
            var sw = Stopwatch.StartNew();
            try
            {
                var response = await _httpClient.DeleteAsync($"{_baseUrl}{path}");
                sw.Stop();
                AppLogger.Debug("DELETE {Path} → {Status} in {Elapsed}ms",
                    path, (int)response.StatusCode, sw.ElapsedMilliseconds);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(json, _jsonOptions)!;
            }
            catch (Exception ex)
            {
                sw.Stop();
                AppLogger.Error(ex, "DELETE {Path} failed after {Elapsed}ms", path, sw.ElapsedMilliseconds);
                throw;
            }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            DetachProcessMonitor();
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
