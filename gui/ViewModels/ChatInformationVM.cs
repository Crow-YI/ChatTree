using System.Net.Http;
using System.Windows;
using TreeChat.Commands;
using TreeChat.Models;
using TreeChat.Services;

namespace TreeChat.ViewModels
{
    /// <summary>
    /// 节点信息VM
    /// </summary>
    public class ChatInformationVM : BaseViewModel
    {
        private string? _userMessage;
        private string? _aiReply;

        public string? UserMessage
        {
            get => _userMessage;
            set => SetProperty(ref _userMessage, value);
        }
        public string? AIReply
        {
            get => _aiReply;
            set => SetProperty(ref _aiReply, value);
        }

        private string _inputMessage;
        public string InputMessage
        {
            get => _inputMessage;
            set
            {
                SetProperty(ref _inputMessage, value);
                SendMessage.OnCanExecuteChanged();
            }
        }

        public AsyncRelayCommand SendMessage { get; }

        private TreeNodeVM? _selectedNode;
        public TreeNodeVM? SelectedNode
        {
            get => _selectedNode;
            set
            {
                _selectedNode = value;
                if(value != null)
                {
                    UserMessage = value.Node.UserMessage.Content;
                    AIReply = value.Node.ReplyMessage?.Content;
                }
                SendMessage.OnCanExecuteChanged();
            }
        }

        private ChatTree? _currentChatTree;
        public ChatTree? CurrentChatTree
        {
            get => _currentChatTree;
            set => SetProperty(ref _currentChatTree, value);
        }

        public event Action<TreeNodeVM, TreeNodeVM>? ChatTreeChanged;

        public ChatInformationVM()
        {
            UserMessage = string.Empty;
            AIReply = string.Empty;

            SendMessage = new AsyncRelayCommand(
                execute: ExecuteSendMessageAsync,
                canExecute: CanExecuteSendMessage);
        }

        private bool CanExecuteSendMessage(object? parameter)
        {
            return !string.IsNullOrEmpty(InputMessage) && SelectedNode != null && CurrentChatTree != null;
        }

        private async Task ExecuteSendMessageAsync(object? parameter)
        {
            if (string.IsNullOrWhiteSpace(InputMessage) || SelectedNode == null || CurrentChatTree == null) return;

            TreeNodeVM previousSelected = SelectedNode;
            TreeNodeVM? newNodeVM = null;

            try
            {
                // 乐观 UI 更新：先创建本地临时节点
                ChatTreeNode newNode = new ChatTreeNode(SelectedNode.Node, new ChatMessage("user", InputMessage));
                newNodeVM = SelectedNode.AddChild(newNode);
                ChatTreeChanged?.Invoke(SelectedNode, newNodeVM);
                SelectedNode = newNodeVM;
                AIReply = "";
                var message = InputMessage;
                InputMessage = string.Empty;

                // 通过 Python 后端发送（流式）
                // 首次发送时自动在 Python 端创建对话树
                var backend = App.Backend;
                if (string.IsNullOrEmpty(CurrentChatTree.TreeId))
                {
                    var treeResponse = await backend.CreateTreeAsync(
                        CurrentChatTree.TreeTitle,
                        CurrentChatTree.RootNode.UserMessage.Content);
                    CurrentChatTree.TreeId = treeResponse.TreeId;
                }
                string treeId = CurrentChatTree.TreeId!;

                string fullContent = "";
                await foreach (var sseEvent in backend.ChatStreamAsync(
                    treeId, previousSelected.Node.NodeID, message))
                {
                    switch (sseEvent.EventType)
                    {
                        case "created":
                            // 更新节点 ID
                            var created = System.Text.Json.JsonSerializer.Deserialize<Models.SseCreatedEvent>(sseEvent.Data);
                            if (created != null)
                            {
                                // 更新本地节点的 NodeID 以匹配后端
                                var node = SelectedNode.Node;
                                // Reflect node ID update via reflection or by syncing
                            }
                            break;

                        case "delta":
                            var delta = System.Text.Json.JsonSerializer.Deserialize<Models.SseDeltaEvent>(sseEvent.Data);
                            if (delta != null)
                            {
                                fullContent += delta.Content;
                                // 实时更新 UI（流式打字机效果）
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    AIReply = fullContent;
                                });
                            }
                            break;

                        case "done":
                            var done = System.Text.Json.JsonSerializer.Deserialize<Models.SseDoneEvent>(sseEvent.Data);
                            if (done?.ReplyMessage != null)
                            {
                                SelectedNode.Node.SetAiReply(new ChatMessage("assistant", done.ReplyMessage.Content));
                            }
                            // 触发树更新
                            ChatTreeChanged?.Invoke(SelectedNode, SelectedNode);
                            return;

                        case "error":
                            var error = System.Text.Json.JsonSerializer.Deserialize<Models.SseErrorEvent>(sseEvent.Data);
                            // 失败清理
                            previousSelected.RemoveChild(newNodeVM);
                            SelectedNode = previousSelected;
                            ChatTreeChanged?.Invoke(SelectedNode, SelectedNode);
                            AIReply = string.Empty;
                            MessageBox.Show(
                                error?.Message ?? "AI 调用失败。",
                                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                // 如果已经创建了节点，则将其移除
                if (newNodeVM != null && previousSelected.Children.Contains(newNodeVM))
                {
                    previousSelected.RemoveChild(newNodeVM);
                }
                SelectedNode = previousSelected;
                ChatTreeChanged?.Invoke(SelectedNode, SelectedNode);
                AIReply = string.Empty;
                MessageBox.Show($"无法连接到 Python 后端服务。\n\n{ex.Message}",
                    "连接错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                if (newNodeVM != null && previousSelected.Children.Contains(newNodeVM))
                {
                    previousSelected.RemoveChild(newNodeVM);
                }
                SelectedNode = previousSelected;
                ChatTreeChanged?.Invoke(SelectedNode, SelectedNode);
                AIReply = string.Empty;
                MessageBox.Show($"请求过程中发生错误：{ex.Message}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 将接口错误结果映射为用户可读中文提示
        /// </summary>
        /// <param name="result">AI 调用结果</param>
        /// <returns>用于弹窗展示的中文提示</returns>
        public static string GetUserFriendlyErrorPrompt(AiCallResult result)
        {
            // HTTP 错误码（文档）：401 / 403 / 422 / 429 / Other
            // 客户端/网络类（非文档 HTTP 码）：Timeout / NetworkError / InvalidResponse / EmptyModelReply / ClientException
            string header = result.ErrorKey switch
            {
                "401" => "令牌无效。",
                "403" => "禁止访问。",
                "422" => "请求体验证失败。",
                "429" => "请求过于频繁。",
                "Other" => "服务器返回了非文档约定的错误状态码。",
                "Timeout" => "请求超时：长时间未收到服务器响应，请检查网络或稍后重试。",
                "NetworkError" => "网络连接失败：无法访问服务器，请检查网络、VPN 或接口地址。",
                "InvalidResponse" => "服务器返回了无法解析的响应（可能不是预期的 JSON 格式）。",
                "EmptyModelReply" => "服务器已响应，但模型返回内容为空。",
                "ClientException" => "客户端处理响应时出错。",
                _ => "发生未知错误。"
            };

            // 将 detail 作为补充信息展示，方便定位问题
            if (!string.IsNullOrWhiteSpace(result.ErrorDetail))
                return $"{header}\n\n详情：{result.ErrorDetail}";

            if (result.StatusCode != null)
                return $"{header}\n\nHTTP 状态码：{(int)result.StatusCode}";

            return header;
        }
    }
}
