using Serilog;
using Serilog.Core;
using System.Diagnostics;
using System.IO;

namespace TreeChat.Infrastructure
{
    /// <summary>
    /// 统一日志入口。封装 Serilog，提供便捷静态方法。
    ///
    /// 两种模式：
    ///   NORMAL — 仅记录 Information+ 级别，用于日常问题回溯
    ///   DEBUG  — 记录 Debug+ 级别，包含所有调用细节，供排查使用
    ///
    /// 日志文件命名（与后端 Python TimedRotatingFileHandler 风格一致）：
    ///   treechat.log              — 当前日志（今日）
    ///   treechat.log.YYYY-MM-DD   — 历史轮换备份（保留 7 天）
    ///
    /// 日志格式：
    ///   YYYY-MM-DD HH:mm:ss.fff | LEVEL | SourceContext | Message
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
        public static void Initialize(string logDir, bool debugMode = false)
        {
            _isDebugMode = debugMode;
            Directory.CreateDirectory(logDir);

            // 手动轮换：若 treechat.log 来自前一个天，移为备份
            RotateLogFile(logDir);

            var levelSwitch = new LoggingLevelSwitch(
                debugMode ? Serilog.Events.LogEventLevel.Debug
                          : Serilog.Events.LogEventLevel.Information);

            _logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch)
                .WriteTo.Async(w => w.File(
                    path: Path.Combine(logDir, "treechat.log"),
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

        public static void Debug(string message, params object?[] args)
        {
            if (_logger == null) return;
            _logger.ForContext("SourceContext", GetCallerClassName())
                   .Debug(message, args);
        }

        public static void Info(string message, params object?[] args)
        {
            if (_logger == null) return;
            _logger.ForContext("SourceContext", GetCallerClassName())
                   .Information(message, args);
        }

        public static void Warn(string message, params object?[] args)
        {
            if (_logger == null) return;
            _logger.ForContext("SourceContext", GetCallerClassName())
                   .Warning(message, args);
        }

        public static void Warn(Exception ex, string message, params object?[] args)
        {
            if (_logger == null) return;
            _logger.ForContext("SourceContext", GetCallerClassName())
                   .Warning(ex, message, args);
        }

        public static void Error(string message, params object?[] args)
        {
            if (_logger == null) return;
            _logger.ForContext("SourceContext", GetCallerClassName())
                   .Error(message, args);
        }

        public static void Error(Exception ex, string message, params object?[] args)
        {
            if (_logger == null) return;
            _logger.ForContext("SourceContext", GetCallerClassName())
                   .Error(ex, message, args);
        }

        public static void Fatal(Exception ex, string message, params object?[] args)
        {
            if (_logger == null) return;
            _logger.ForContext("SourceContext", GetCallerClassName())
                   .Fatal(ex, message, args);
        }

        // ==================== 私有方法 ====================

        /// <summary>
        /// 自动提取调用方类名，无需调用方传参。
        /// 栈帧: 0=GetCallerClassName, 1=Info/Debug/..., 2=实际调用方
        /// </summary>
        private static string GetCallerClassName()
        {
            try
            {
                var frame = new StackFrame(2, false);
                var callerType = frame?.GetMethod()?.DeclaringType;
                if (callerType != null)
                {
                    var name = callerType.Name;
                    // 跳过匿名闭包类（lambda/async 状态机），取外层真实类名
                    if (name.Contains('<'))
                        name = callerType.DeclaringType?.Name ?? name;
                    return name;
                }
            }
            catch { }
            return "?";
        }

        /// <summary>
        /// 手动轮换 treechat.log：若日志文件来自前一个天则移为备份，
        /// 并清理超过 7 天的旧备份。风格与后端 Python TimedRotatingFileHandler 一致。
        /// </summary>
        private static void RotateLogFile(string logDir)
        {
            var logPath = Path.Combine(logDir, "treechat.log");
            if (File.Exists(logPath))
            {
                var lastWrite = File.GetLastWriteTime(logPath);
                if (lastWrite.Date < DateTime.Today)
                {
                    var backupName = $"treechat.log.{lastWrite:yyyy-MM-dd}";
                    var backupPath = Path.Combine(logDir, backupName);
                    try
                    {
                        if (!File.Exists(backupPath))
                            File.Move(logPath, backupPath);
                        else
                            File.Delete(logPath);
                    }
                    catch { /* 轮换失败不阻塞启动 */ }
                }
            }

            // 清理超过 7 天的备份
            try
            {
                var cutoff = DateTime.Today.AddDays(-7);
                foreach (var f in Directory.GetFiles(logDir, "treechat.log.*"))
                {
                    if (File.GetLastWriteTime(f) < cutoff)
                        File.Delete(f);
                }
            }
            catch { }
        }
    }
}
