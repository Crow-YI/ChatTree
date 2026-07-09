using System.Windows;
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

            if (e.Args.Length > 0)
            {
                StartupFilePath = e.Args[0];
            }

            // 应用启动时读取保存的配置
            ApiConfig.LoadFromFile();

            // 启动 Python 后端
            Backend = new PythonBackendService(
                baseUrl: ApiConfig.PythonBackendUrl,
                pyProjectDir: ApiConfig.PythonProjectDir);

            try
            {
                await Backend.StartAsync();
            }
            catch (Exception ex)
            {
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
            }
            catch
            {
                // 配置推送失败不阻止启动
            }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            // 优雅关闭 Python 后端
            try
            {
                await Backend.StopAsync();
            }
            catch
            {
                // 忽略关闭时的错误
            }

            // 应用关闭时保存当前配置
            ApiConfig.SaveToFile();
            base.OnExit(e);
        }
    }
}