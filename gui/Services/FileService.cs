using System.IO;
using System.Windows;
using Microsoft.Win32;
using TreeChat.Models;

namespace TreeChat.Services
{
    /// <summary>
    /// 文件服务实现类，处理对话树的文件保存和读取操作
    /// </summary>
    public class FileService : IFileService
    {
        private readonly JsonSerializationService _serializationService;
        private const string FileExtension = ".chat";
        private const string FileFilter = "聊天文件 (*.chat)|*.chat|所有文件 (*.*)|*.*";

        /// <summary>
        /// 构造函数，初始化序列化服务
        /// </summary>
        public FileService()
        {
            _serializationService = new JsonSerializationService();
        }

        /// <summary>
        /// 保存对话树到用户指定的文件
        /// 显示保存对话框让用户选择保存位置和文件名
        /// </summary>
        /// <param name="chatTree">要保存的对话树</param>
        /// <returns>是否保存成功</returns>
        public bool SaveChatTree(ChatTree chatTree)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = FileFilter,
                    DefaultExt = FileExtension,
                    FileName = chatTree.TreeTitle,
                    Title = "保存对话"
                };

                if (dialog.ShowDialog() == true)
                {
                    var json = _serializationService.SerializeChatTree(chatTree);
                    File.WriteAllText(dialog.FileName, json);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败：{ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// 从用户选择的文件加载对话树
        /// 显示打开对话框让用户选择要读取的文件
        /// </summary>
        /// <returns>加载的对话树，失败返回null</returns>
        public ChatTree? LoadChatTree()
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = FileFilter,
                    DefaultExt = FileExtension,
                    Title = "打开对话"
                };

                if (dialog.ShowDialog() == true)
                {
                    var json = File.ReadAllText(dialog.FileName);
                    var chatTree = _serializationService.DeserializeChatTree(json);
                    
                    if (chatTree != null)
                    {
                        var fileName = Path.GetFileNameWithoutExtension(dialog.FileName);
                        chatTree.TreeTitle = fileName;
                    }
                    
                    return chatTree;
                }

                return null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取失败：{ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        /// <summary>
        /// 从指定文件路径加载对话树
        /// 不显示对话框，直接从指定路径读取文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>加载的对话树，失败返回null</returns>
        public ChatTree? LoadChatTree(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var chatTree = _serializationService.DeserializeChatTree(json);
                
                if (chatTree != null)
                {
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    chatTree.TreeTitle = fileName;
                }
                
                return chatTree;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取失败：{ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
    }
}
