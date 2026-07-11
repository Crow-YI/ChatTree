using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TreeChat.Infrastructure;
using TreeChat.Models;

namespace TreeChat.Services
{
    /// <summary>
    /// 最近打开文件列表管理器。持久化到项目根目录的 recent_files.json。
    /// 自动去重、上限 10 条、最新的在最前面。
    /// </summary>
    public static class RecentFilesManager
    {
        private static List<RecentFileItem>? _cache;
        private const int MaxRecentFiles = 10;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
        };

        private static string? ConfigFilePath
        {
            get
            {
                var root = FindProjectRoot();
                return root != null ? Path.Combine(root, "recent_files.json") : null;
            }
        }

        /// <summary>
        /// 查找项目根目录（先找 backend/，再取其父目录）。
        /// </summary>
        private static string? FindProjectRoot()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var dir = new DirectoryInfo(baseDir);

            for (int i = 0; i < 8; i++)
            {
                var backendDir = Path.Combine(dir.FullName, "backend");
                if (File.Exists(Path.Combine(backendDir, "pyproject.toml")))
                    return dir.FullName;

                var pyVerBackend = Path.Combine(dir.FullName, "py_version", "backend");
                if (File.Exists(Path.Combine(pyVerBackend, "pyproject.toml")))
                    return dir.FullName;

                if (dir.Parent == null) break;
                dir = dir.Parent;
            }

            return null;
        }

        /// <summary>
        /// 从 recent_files.json 加载最近文件列表。
        /// </summary>
        public static List<RecentFileItem> Load()
        {
            if (_cache != null)
                return _cache;

            var path = ConfigFilePath;
            if (path == null || !File.Exists(path))
            {
                _cache = new List<RecentFileItem>();
                return _cache;
            }

            try
            {
                var json = File.ReadAllText(path);
                var items = JsonSerializer.Deserialize<List<RecentFileItem>>(json, JsonOptions);
                _cache = items ?? new List<RecentFileItem>();
            }
            catch (Exception ex)
            {
                AppLogger.Warn(ex, "Failed to load recent files from {Path}", path);
                _cache = new List<RecentFileItem>();
            }

            return _cache;
        }

        /// <summary>
        /// 写入 recent_files.json。
        /// </summary>
        public static void Save()
        {
            var path = ConfigFilePath;
            if (path == null || _cache == null) return;

            try
            {
                var json = JsonSerializer.Serialize(_cache, JsonOptions);
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                AppLogger.Warn(ex, "Failed to save recent files to {Path}", path);
            }
        }

        /// <summary>
        /// 添加最近文件（去重、移到顶部、上限 10 条）。
        /// 自动持久化。
        /// </summary>
        public static void AddFile(string path, string title)
        {
            if (string.IsNullOrEmpty(path)) return;

            Load(); // 确保已加载

            // 去重：如果已存在则移除旧位置
            var existing = _cache!.FirstOrDefault(f =>
                string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                _cache!.Remove(existing);
                // 如果标题变了，用新标题
                if (!string.IsNullOrEmpty(title))
                    existing.Title = title;
            }

            // 在最前面插入
            _cache!.Insert(0, new RecentFileItem
            {
                Path = path,
                Title = string.IsNullOrEmpty(title)
                    ? Path.GetFileNameWithoutExtension(path)
                    : title,
            });

            // 超出上限，移除末尾
            if (_cache.Count > MaxRecentFiles)
                _cache.RemoveRange(MaxRecentFiles, _cache.Count - MaxRecentFiles);

            Save();
        }

        /// <summary>
        /// 从最近文件列表中移除指定路径。
        /// 自动持久化。
        /// </summary>
        public static void RemoveFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            Load(); // 确保已加载

            var existing = _cache!.FirstOrDefault(f =>
                string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                _cache!.Remove(existing);
                Save();
            }
        }

        /// <summary>
        /// 清空最近文件列表并持久化。
        /// </summary>
        public static void Clear()
        {
            _cache = new List<RecentFileItem>();
            Save();
        }

        /// <summary>
        /// 获取当前最近文件列表（从缓存读取，不触发文件 I/O）。
        /// </summary>
        public static List<RecentFileItem> GetAll()
        {
            Load();
            return _cache!;
        }
    }
}
