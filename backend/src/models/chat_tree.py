"""对话树模型。"""

from __future__ import annotations

import uuid
from datetime import datetime, timezone

from pydantic import BaseModel, Field, PrivateAttr

from .chat_message import HumanMessage
from .chat_tree_node import ChatTreeNode


class ChatTree(BaseModel):
    """对话树，包含系统提示、根节点和元数据。"""

    tree_id: str = Field(default_factory=lambda: uuid.uuid4().hex[:12])
    title: str = "新对话"
    system_prompt: str = ""
    root_node: ChatTreeNode
    created_at: str = Field(
        default_factory=lambda: datetime.now(timezone.utc).isoformat()
    )

    # 节点 ID 计数器（私有属性，不参与序列化，每棵树独立管理）
    _next_node_id: int = PrivateAttr(default=2)

    def get_next_node_id(self) -> int:
        """返回当前节点 ID 并递增。根节点固定为 1，子节点从 2 开始。"""
        current = self._next_node_id
        self._next_node_id += 1
        return current

    def reset_node_id_counter(self, max_id: int) -> None:
        """将计数器重设为 max_id + 1（用于反序列化后避免 ID 冲突）。"""
        self._next_node_id = max_id + 1

    @classmethod
    def create(cls, title: str = "新对话", system_prompt: str | None = None) -> ChatTree:
        """工厂方法：创建新的对话树。"""
        final_prompt = system_prompt or "你是一个有帮助的AI助手。"
        root = ChatTreeNode(
            node_id=1,
            user_message=HumanMessage(content=""),
        )
        return cls(title=title, root_node=root, system_prompt=final_prompt)

    def find_node(self, node_id: int) -> ChatTreeNode | None:
        """递归搜索节点。"""

        def _search(node: ChatTreeNode) -> ChatTreeNode | None:
            if node.node_id == node_id:
                return node
            for child in node.children:
                result = _search(child)
                if result:
                    return result
            return None

        return _search(self.root_node)

    def get_all_nodes(self) -> list[ChatTreeNode]:
        """获取树中所有节点（深度优先遍历）。"""
        result: list[ChatTreeNode] = []

        def _dfs(node: ChatTreeNode) -> None:
            result.append(node)
            for child in node.children:
                _dfs(child)

        _dfs(self.root_node)
        return result

    @property
    def node_count(self) -> int:
        return len(self.get_all_nodes())
