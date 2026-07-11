using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using TreeChat.Infrastructure;
using TreeChat.Services;

namespace TreeChat
{
    public partial class App : Application
    {
        /// <summary>
        /// 启动时传入的文件路径
        /// </summary>
        public static string? StartupFilePath { get; private set; }

        /// <summary>
        /// Python 后端服务（全局单例）
        /// </summary>
        public static PythonBackendService Backend { get; private set; } = null!;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // === 初始化日志系统 ===
            InitializeLogger(e.Args);

            var appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
            AppLogger.Info("Application starting: version={Version}", appVersion);

            // === 注册全局未处理异常捕获 ===
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                AppLogger.Fatal(ex ?? new Exception("Unknown"), "AppDomain unhandled exception");
            };

            Application.Current.DispatcherUnhandledException += (s, args) =>
            {
                AppLogger.Fatal(args.Exception, "Dispatcher unhandled exception");
                args.Handled = true; // 记录后继续运行，避免直接崩溃
            };

            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                AppLogger.Fatal(args.Exception, "Unobserved task exception");
                args.SetObserved();
            };

            // === 启动参数 ===
            if (e.Args.Length > 0 && !e.Args[0].StartsWith("--"))
            {
                StartupFilePath = e.Args[0];
            }

            // 应用启动时读取保存的配置
            ApiConfig.LoadFromFile();

            // 启动 Python 后端
            Backend = new PythonBackendService(
                baseUrl: ApiConfig.PythonBackendUrl,
                pyProjectDir: ApiConfig.PythonProjectDir);

            // 监听后端进程意外退出
            Backend.ProcessExited += OnBackendProcessExited;

            try
            {
                await Backend.StartAsync();
                AppLogger.Info("Python backend started successfully");
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, "Failed to start Python backend");
                MessageBox.Show(
                    $"无法启动 Python 后端服务。\n\n{ex.Message}\n\n" +
                    "请确保已安装 uv (https://docs.astral.sh/uv/)，" +
                    "且 py_version/ 目录中的 Python 项目完整。",
                    "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            // 将当前配置推送到 Python 后端
            try
            {
                await Backend.PushConfigAsync(new Models.ChatConfigData
                {
                    Model = ApiConfig.ModelName,
                    Temperature = ApiConfig.Temperature,
                    TopP = ApiConfig.TopP,
                    MaxTokens = ApiConfig.MaxTokens,
                });
                AppLogger.Info("Configuration pushed to backend: model={Model}", ApiConfig.ModelName);
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Failed to push config to backend: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// 确定日志目录并初始化 AppLogger。
        /// </summary>
        private static void InitializeLogger(string[] args)
        {
            // 日志目录：项目根目录下的 logs/
            var projectRoot = FindProjectRoot();
            var logDir = projectRoot != null
                ? Path.Combine(projectRoot, "logs")
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

            // 是否 DEBUG 模式：通过 --debug 命令行参数或配置文件控制
            bool debugMode = args.Contains("--debug") || ApiConfig.EnableDebugLogging;

            AppLogger.Initialize(logDir, debugMode);
        }

        /// <summary>
        /// 查找项目根目录（查找 backend/pyproject.toml）。
        /// 复用 ApiConfig 中的查找逻辑。
        /// </summary>
        private static string? FindProjectRoot()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var dir = new DirectoryInfo(baseDir);

            for (int i = 0; i < 8; i++)
            {
                if (File.Exists(Path.Combine(dir.FullName, "backend", "pyproject.toml")))
                    return dir.FullName;

                if (File.Exists(Path.Combine(dir.FullName, "py_version", "backend", "pyproject.toml")))
                    return dir.FullName;

                if (dir.Parent == null) break;
                dir = dir.Parent;
            }

            return null;
        }

        /// <summary>
        /// Python 后端进程意外退出时的处理。
        /// </summary>
        private void OnBackendProcessExited(int? exitCode)
        {
            AppLogger.Warn("Backend process exited unexpectedly: code={ExitCode}", exitCode);

            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    $"Python 后端进程意外退出 (exit code: {exitCode})。\n\n" +
                    "请重启应用程序以重新连接后端服务。",
                    "后端连接断开", MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            AppLogger.Info("Application exiting");

            // 优雅关闭 Python 后端
            try
            {
                await Backend.StopAsync();
                AppLogger.Info("Python backend stopped");
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Error stopping backend: {Message}", ex.Message);
            }

            // 应用关闭时保存当前配置和最近文件列表
            ApiConfig.SaveToFile();
            RecentFilesManager.Save();

            AppLogger.Close();
            base.OnExit(e);
        }
    }
}
