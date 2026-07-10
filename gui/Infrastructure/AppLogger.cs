using Serilog;
using Serilog.Core;
using System.IO;
using System.Runtime.CompilerServices;

namespace TreeChat.Infrastructure
{
    /// <summary>
    /// 统一日志入口。封装 Serilog，提供便捷静态方法。
    ///
    /// 两种模式：
    ///   NORMAL — 仅记录 Information+ 级别，用于日常问题回溯
    ///   DEBUG  — 记录 Debug+ 级别，包含所有调用细节，供排查使用
    /// </summary>
    public static class AppLogger
    {
        private static Logger? _logger;
        private static bool _isDebugMode;

        /// <summary>
        /// 当前是否处于 DEBUG 模式。
        /// </summary>
        public static bool IsDebugMode => _isDebugMode;

        /// <summary>
        /// 初始化日志系统。必须在其它日志调用前调用。
        /// </summary>
        /// <param name="logDir">日志文件写入目录</param>
        /// <param name="debugMode">是否启用 DEBUG 级别日志</param>
        public static void Initialize(string logDir, bool debugMode = false)
        {
            _isDebugMode = debugMode;
            Directory.CreateDirectory(logDir);

            var levelSwitch = new LoggingLevelSwitch(
                debugMode ? Serilog.Events.LogEventLevel.Debug
                          : Serilog.Events.LogEventLevel.Information);

            _logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch)
                .WriteTo.Async(w => w.File(
                    path: Path.Combine(logDir, "treechat-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} | {Level:u4} | {SourceContext} | {Message:l}{NewLine}{Exception}",
                    encoding: System.Text.Encoding.UTF8))
                .WriteTo.Debug(
                    outputTemplate: "{Level:u4} | {Message:l}{NewLine}{Exception}")
                .CreateLogger();

            Info("AppLogger initialized: {LogDir} (mode={Mode})",
                logDir, debugMode ? "DEBUG" : "NORMAL");
        }

        /// <summary>
        /// 关闭日志系统，刷新缓冲区。
        /// </summary>
        public static void Close()
        {
            Info("AppLogger closing");
            _logger?.Dispose();
            _logger = null;
        }

        // ==================== 便捷方法 ====================

        /// <summary>
        /// 记录 DEBUG 级别日志（仅在 DEBUG 模式下生效）。
        /// </summary>
        public static void Debug(string message, params object[] args)
        {
            _logger?.Debug(message, args);
        }

        /// <summary>
        /// 记录 INFO 级别日志。
        /// </summary>
        public static void Info(string message, params object[] args)
        {
            _logger?.Information(message, args);
        }

        /// <summary>
        /// 记录 WARNING 级别日志。
        /// </summary>
        public static void Warn(string message, params object[] args)
        {
            _logger?.Warning(message, args);
        }

        /// <summary>
        /// 记录 WARNING 级别日志（含异常）。
        /// </summary>
        public static void Warn(Exception ex, string message, params object[] args)
        {
            _logger?.Warning(ex, message, args);
        }

        /// <summary>
        /// 记录 ERROR 级别日志（仅含消息）。
        /// </summary>
        public static void Error(string message, params object[] args)
        {
            _logger?.Error(message, args);
        }

        /// <summary>
        /// 记录 ERROR 级别日志（含异常）。
        /// </summary>
        public static void Error(Exception ex, string message, params object[] args)
        {
            _logger?.Error(ex, message, args);
        }

        /// <summary>
        /// 记录 FATAL 级别日志（含异常），用于不可恢复的错误。
        /// </summary>
        public static void Fatal(Exception ex, string message, params object[] args)
        {
            _logger?.Fatal(ex, message, args);
        }
    }
}
