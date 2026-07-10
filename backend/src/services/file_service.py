""".chat 文件序列化/反序列化服务。

注意：文件对话框（SaveFileDialog/OpenFileDialog）在 WPF 端处理。
Python 端只负责 JSON 的序列化与反序列化逻辑。
"""

from __future__ import annotations

import json
import logging
from datetime import datetime, timezone
from typing import Any

from ..models.chat_tree import ChatTree
from ..models.chat_tree_node import ChatTreeNode
from ..models.chat_message import AIMessage, message_to_role, message_from_role

logger = logging.getLogger(__name__)


class FileService:
    """对话树的 JSON 序列化/反序列化。"""

    CURRENT_VERSION: str = "1.0"

    def serialize(self, tree: ChatTree) -> str:
        """将对话树序列化为 JSON 字符串。"""
        data: dict[str, object] = {
            "Version": self.CURRENT_VERSION,
            "TreeTitle": tree.title,
            "CreatedTime": tree.created_at,
            "SystemPrompt": tree.system_prompt,
            "RootNode": self._serialize_node(tree.root_node),
        }
        logger.info("Tree serialized: id=%s title=%s", tree.tree_id, tree.title)
        return json.dumps(data, ensure_ascii=False, indent=2)

    def deserialize(self, json_str: str, title: str | None = None) -> ChatTree:
        """从 JSON 字符串反序列化对话树。"""
        data: dict[str, Any] = json.loads(json_str)

        root_data: dict[str, Any] = data.get("RootNode", {})
        root_node = self._deserialize_node(root_data, parent=None)

        tree_title = title or data.get("TreeTitle", "已导入对话")
        tree = ChatTree(
            title=tree_title,
            root_node=root_node,
            created_at=data.get("CreatedTime", datetime.now(timezone.utc).isoformat()),
            system_prompt=data.get("SystemPrompt", ""),
        )
        logger.info("Tree deserialized: title=%s", tree_title)
        return tree

    def _serialize_node(self, node: ChatTreeNode) -> dict[str, object]:
        """递归序列化单个节点。"""
        return {
            "NodeId": node.node_id,
            "Name": node.name,
            "UserMessage": {
                "Role": message_to_role(node.user_message),
                "Content": node.user_message.content,
            },
            "ReplyMessage": (
                {
                    "Role": message_to_role(node.reply_message),
                    "Content": node.reply_message.content,
                }
                if node.reply_message
                else None
            ),
            "ChildNodes": [self._serialize_node(c) for c in node.children],
        }

    def _deserialize_node(
        self, data: dict[str, Any], parent: ChatTreeNode | None
    ) -> ChatTreeNode:
        """递归反序列化单个节点。"""
        user_msg_data: dict[str, Any] = data.get("UserMessage", {})
        user_message = message_from_role(
            role=user_msg_data.get("Role", "user"),
            content=user_msg_data.get("Content", ""),
        )

        node = ChatTreeNode(
            node_id=data.get("NodeId", 0),
            user_message=user_message,
            name=data.get("Name"),
        )
        node.set_parent(parent)

        reply_data: dict[str, Any] | None = data.get("ReplyMessage")
        if reply_data:
            node.reply_message = AIMessage(
                content=reply_data.get("Content", ""),
            )

        for child_data in data.get("ChildNodes", []):
            child = self._deserialize_node(child_data, parent=node)
            node.children.append(child)

        return node


# 全局单例
file_service = FileService()
