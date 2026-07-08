"""聊天树节点模型。"""

from __future__ import annotations

from pydantic import BaseModel, ConfigDict

from .chat_message import BaseMessage, AIMessage


class ChatTreeNode(BaseModel):
    """树节点，包含一条用户消息和可选的 AI 回复。

    使用 LangChain BaseMessage 子类型作为消息表示：
    - user_message: SystemMessage（根节点）或 HumanMessage（子节点）
    - reply_message: AIMessage（AI 回复）
    """

    model_config = ConfigDict(arbitrary_types_allowed=True)

    node_id: int
    user_message: BaseMessage
    reply_message: AIMessage | None = None
    name: str | None = None
    children: list[ChatTreeNode] = []

    # 不参与序列化，仅用于 get_full_context 追踪父节点
    _parent: ChatTreeNode | None = None

    def set_parent(self, parent: ChatTreeNode | None) -> None:
        """设置父节点引用（由 TreeManager 在操作树时维护）。"""
        self._parent = parent

    @property
    def parent(self) -> ChatTreeNode | None:
        return self._parent

    def get_full_context(self) -> list[BaseMessage]:
        """收集从根节点到当前节点的完整上下文（按时间顺序排列）。

        递归向上遍历父节点，收集所有 user_message 和 reply_message，
        然后反转得到从 system prompt → ... → 当前用户消息的完整列表。
        """
        messages: list[BaseMessage] = []
        current: ChatTreeNode | None = self

        while current is not None:
            # 先添加 AI 回复（如果存在）
            if current.reply_message and current.reply_message.content:
                messages.append(current.reply_message)

            # 再添加用户消息
            if current.user_message.content:
                messages.append(current.user_message)

            current = current._parent

        messages.reverse()
        return messages

    def add_child(self, child: ChatTreeNode) -> None:
        """添加子节点并设置父引用。"""
        child.set_parent(self)
        self.children.append(child)

    def remove_child(self, child: ChatTreeNode) -> bool:
        """移除子节点，返回是否成功。"""
        for i, c in enumerate(self.children):
            if c.node_id == child.node_id:
                child.set_parent(None)
                self.children.pop(i)
                return True
        return False
