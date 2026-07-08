"""对话树模型。"""

from __future__ import annotations

import uuid
from datetime import datetime, timezone

from pydantic import BaseModel, Field

from .chat_message import SystemMessage
from .chat_tree_node import ChatTreeNode


class ChatTree(BaseModel):
    """对话树，包含根节点和元数据。"""

    tree_id: str = Field(default_factory=lambda: uuid.uuid4().hex[:12])
    title: str = "新对话"
    root_node: ChatTreeNode
    created_at: str = Field(
        default_factory=lambda: datetime.now(timezone.utc).isoformat()
    )

    @classmethod
    def create(cls, title: str = "新对话", system_prompt: str | None = None) -> ChatTree:
        """工厂方法：创建新的对话树。"""
        final_prompt = system_prompt or "你是一个有帮助的AI助手。"
        root = ChatTreeNode(
            node_id=1,
            user_message=SystemMessage(content=final_prompt),
        )
        return cls(title=title, root_node=root)

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
