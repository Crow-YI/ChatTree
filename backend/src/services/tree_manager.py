"""对话树内存管理器 — 管理所有 ChatTree 实例的 CRUD。"""

from __future__ import annotations

import logging

from ..models.chat_tree import ChatTree
from ..models.chat_tree_node import ChatTreeNode
from ..models.chat_message import HumanMessage, AIMessage
from ..core.errors import TreeNotFoundError, NodeNotFoundError

logger = logging.getLogger(__name__)


class TreeManager:
    """单例服务，在内存中管理所有对话树。"""

    def __init__(self) -> None:
        self._trees: dict[str, ChatTree] = {}

    # === Tree CRUD ===

    def create_tree(self, title: str = "新对话", system_prompt: str | None = None) -> ChatTree:
        tree = ChatTree.create(title=title, system_prompt=system_prompt)
        self._trees[tree.tree_id] = tree
        logger.info("Tree created: id=%s title=%s", tree.tree_id, title)
        return tree

    def get_tree(self, tree_id: str) -> ChatTree:
        if tree_id not in self._trees:
            logger.warning("Tree not found: id=%s", tree_id)
            raise TreeNotFoundError(f"对话树 '{tree_id}' 不存在。")
        logger.debug("Tree get: id=%s", tree_id)
        return self._trees[tree_id]

    def list_trees(self) -> list[ChatTree]:
        count = len(self._trees)
        logger.debug("Trees listed: count=%d", count)
        return list(self._trees.values())

    def delete_tree(self, tree_id: str) -> None:
        if tree_id not in self._trees:
            logger.warning("Tree not found for deletion: id=%s", tree_id)
            raise TreeNotFoundError(f"对话树 '{tree_id}' 不存在。")
        del self._trees[tree_id]
        logger.info("Tree deleted: id=%s", tree_id)

    def rename_tree(self, tree_id: str, new_title: str) -> None:
        tree = self.get_tree(tree_id)
        old_title = tree.title
        tree.title = new_title
        logger.info("Tree renamed: id=%s old=%s new=%s", tree_id, old_title, new_title)

    def add_tree(self, tree: ChatTree) -> None:
        """直接添加一个已构造的树（用于文件反序列化后加入管理）。"""
        # 扫描所有节点，确保树内计数器不冲突
        all_nodes = tree.get_all_nodes()
        if all_nodes:
            max_id = max(n.node_id for n in all_nodes)
            tree.reset_node_id_counter(max_id)
        self._trees[tree.tree_id] = tree
        logger.info("Tree added from deserialization: id=%s title=%s", tree.tree_id, tree.title)

    # === Node Operations ===

    def get_node(self, tree_id: str, node_id: int) -> ChatTreeNode:
        tree = self.get_tree(tree_id)
        node = tree.find_node(node_id)
        if node is None:
            logger.warning("Node not found: tree=%s node=%d", tree_id, node_id)
            raise NodeNotFoundError(f"节点 {node_id} 在树 '{tree_id}' 中不存在。")
        logger.debug("Node get: tree=%s node=%d", tree_id, node_id)
        return node

    def add_child_node(
        self, tree_id: str, parent_node_id: int, message: str
    ) -> tuple[ChatTreeNode, ChatTreeNode]:
        """在指定父节点下创建子节点，返回 (新子节点, 父节点)。"""
        parent = self.get_node(tree_id, parent_node_id)
        tree = self.get_tree(tree_id)
        new_id = tree.get_next_node_id()
        child = ChatTreeNode(
            node_id=new_id,
            user_message=HumanMessage(content=message),
        )
        parent.add_child(child)
        logger.info(
            "Node added: tree=%s parent=%d child=%d",
            tree_id, parent_node_id, new_id,
        )
        return child, parent

    def rename_node(self, tree_id: str, node_id: int, name: str) -> None:
        node = self.get_node(tree_id, node_id)
        old_name = node.name
        node.name = name
        logger.info("Node renamed: tree=%s node=%d old=%s new=%s", tree_id, node_id, old_name, name)

    def delete_node(self, tree_id: str, node_id: int) -> None:
        """删除节点及其子树。根节点不可删除。"""
        tree = self.get_tree(tree_id)
        if node_id == tree.root_node.node_id:
            logger.warning("Attempted to delete root node: tree=%s", tree_id)
            raise ValueError("根节点不可删除。")
        # 在父节点的 children 列表中查找并移除
        node = self.get_node(tree_id, node_id)
        parent = node.parent
        if parent is None:
            logger.warning("Node has no parent: tree=%s node=%d", tree_id, node_id)
            raise ValueError("无法删除没有父节点的节点。")
        parent.remove_child(node)
        logger.info("Node deleted: tree=%s node=%d", tree_id, node_id)

    def set_ai_reply(self, tree_id: str, node_id: int, content: str) -> None:
        node = self.get_node(tree_id, node_id)
        node.reply_message = AIMessage(content=content)
        logger.debug("AI reply set: tree=%s node=%d chars=%d", tree_id, node_id, len(content))


# 全局单例
tree_manager = TreeManager()
