using System.Net.Http;
using System.Windows;
using TreeChat.Commands;
using TreeChat.Models;

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
                    UserMessage = value.Node.UserMessage;
                    AIReply = value.Node.ReplyMessage;
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
                ChatTreeNode newNode = new ChatTreeNode(SelectedNode.Node, InputMessage);
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
                        CurrentChatTree.RootNode.UserMessage);
                    CurrentChatTree.TreeId = treeResponse.TreeId;
                }
                string treeId = CurrentChatTree.TreeId!;

                string fullContent = "";
                // 在后台线程上消费 SSE 流，避免阻塞 UI 线程渲染
                var sseEvents = backend.ChatStreamAsync(
                    treeId, previousSelected.Node.NodeID, message);
                await Task.Run(async () =>
                {
                    await foreach (var sseEvent in sseEvents)
                    {
                        switch (sseEvent.EventType)
                        {
                            case "created":
                                break;

                            case "delta":
                                var delta = System.Text.Json.JsonSerializer.Deserialize<Models.SseDeltaEvent>(sseEvent.Data);
                                if (delta != null)
                                {
                                    fullContent += delta.Content;
                                    var contentToShow = fullContent;
                                    // 以 Background 优先级调度（低于 DataBind 和 Render），
                                    // 确保上一轮 DataBind(8)→Render(7) 完成后再更新
                                    await Application.Current.Dispatcher.InvokeAsync(() =>
                                    {
                                        AIReply = contentToShow;
                                    }, System.Windows.Threading.DispatcherPriority.Background);
                                }
                                break;

                            case "done":
                                var done = System.Text.Json.JsonSerializer.Deserialize<Models.SseDoneEvent>(sseEvent.Data);
                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    if (done?.ReplyMessage != null)
                                    {
                                        SelectedNode.Node.SetAiReply(done.ReplyMessage.Content);
                                    }
                                    // 触发树更新
                                    ChatTreeChanged?.Invoke(SelectedNode, SelectedNode);
                                });
                                return;

                            case "error":
                                var error = System.Text.Json.JsonSerializer.Deserialize<Models.SseErrorEvent>(sseEvent.Data);
                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    // 失败清理
                                    previousSelected.RemoveChild(newNodeVM);
                                    SelectedNode = previousSelected;
                                    ChatTreeChanged?.Invoke(SelectedNode, SelectedNode);
                                    AIReply = string.Empty;
                                    MessageBox.Show(
                                        error?.Message ?? "AI 调用失败。",
                                        "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                                });
                                return;
                        }
                    }
                });
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
    }
}
