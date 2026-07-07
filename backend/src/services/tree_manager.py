"""对话树内存管理器 — 管理所有 ChatTree 实例的 CRUD。"""

from __future__ import annotations

import threading

from ..models.chat_tree import ChatTree
from ..models.chat_tree_node import ChatTreeNode
from ..models.chat_message import ChatMessage
from ..core.errors import TreeNotFoundError, NodeNotFoundError


class _NextNodeId:
    """线程安全的全局 NodeID 计数器。"""

    def __init__(self) -> None:
        self._lock = threading.Lock()
        self._counter = 2  # 从 2 开始，node_id=1 保留给根节点

    def next(self) -> int:
        with self._lock:
            current = self._counter
            self._counter += 1
            return current

    def ensure_at_least(self, value: int) -> None:
        """确保计数器至少为指定值（用于加载文件后保持 ID 不冲突）。"""
        with self._lock:
            if self._counter <= value:
                self._counter = value + 1


class TreeManager:
    """单例服务，在内存中管理所有对话树。"""

    def __init__(self) -> None:
        self._trees: dict[str, ChatTree] = {}
        self._node_id_gen = _NextNodeId()

    # === Tree CRUD ===

    def create_tree(self, title: str = "新对话", system_prompt: str | None = None) -> ChatTree:
        tree = ChatTree.create(title=title, system_prompt=system_prompt)
        self._trees[tree.tree_id] = tree
        return tree

    def get_tree(self, tree_id: str) -> ChatTree:
        if tree_id not in self._trees:
            raise TreeNotFoundError(f"对话树 '{tree_id}' 不存在。")
        return self._trees[tree_id]

    def list_trees(self) -> list[ChatTree]:
        return list(self._trees.values())

    def delete_tree(self, tree_id: str) -> None:
        if tree_id not in self._trees:
            raise TreeNotFoundError(f"对话树 '{tree_id}' 不存在。")
        del self._trees[tree_id]

    def rename_tree(self, tree_id: str, new_title: str) -> None:
        tree = self.get_tree(tree_id)
        tree.title = new_title

    def add_tree(self, tree: ChatTree) -> None:
        """直接添加一个已构造的树（用于文件反序列化后加入管理）。"""
        # 确保 NodeID 不冲突
        all_nodes = tree.get_all_nodes()
        if all_nodes:
            max_id = max(n.node_id for n in all_nodes)
            self._node_id_gen.ensure_at_least(max_id)
        self._trees[tree.tree_id] = tree

    # === Node Operations ===

    def get_node(self, tree_id: str, node_id: int) -> ChatTreeNode:
        tree = self.get_tree(tree_id)
        node = tree.find_node(node_id)
        if node is None:
            raise NodeNotFoundError(f"节点 {node_id} 在树 '{tree_id}' 中不存在。")
        return node

    def add_child_node(
        self, tree_id: str, parent_node_id: int, message: str
    ) -> tuple[ChatTreeNode, ChatTreeNode]:
        """在指定父节点下创建子节点，返回 (新子节点, 父节点)。"""
        parent = self.get_node(tree_id, parent_node_id)
        new_id = self._node_id_gen.next()
        child = ChatTreeNode(
            node_id=new_id,
            user_message=ChatMessage(role="user", content=message),
        )
        parent.add_child(child)
        return child, parent

    def rename_node(self, tree_id: str, node_id: int, name: str) -> None:
        node = self.get_node(tree_id, node_id)
        node.name = name

    def delete_node(self, tree_id: str, node_id: int) -> None:
        """删除节点及其子树。根节点不可删除。"""
        tree = self.get_tree(tree_id)
        if node_id == tree.root_node.node_id:
            raise ValueError("根节点不可删除。")
        # 在父节点的 children 列表中查找并移除
        node = self.get_node(tree_id, node_id)
        parent = node.parent
        if parent is None:
            raise ValueError("无法删除没有父节点的节点。")
        parent.remove_child(node)

    def set_ai_reply(self, tree_id: str, node_id: int, content: str) -> None:
        node = self.get_node(tree_id, node_id)
        node.reply_message = ChatMessage(role="assistant", content=content)

    # === Serialization Support ===

    def reset_node_id_counter(self, max_id: int) -> None:
        self._node_id_gen.ensure_at_least(max_id)


# 全局单例
tree_manager = TreeManager()
