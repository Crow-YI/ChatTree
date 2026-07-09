"""测试 TreeManager 服务。"""

from __future__ import annotations

import pytest

from src.core.errors import TreeNotFoundError, NodeNotFoundError
from src.services.tree_manager import TreeManager
from src.models.chat_tree import ChatTree
from src.models.chat_tree_node import ChatTreeNode


class TestTreeManager:
    """对话树 CRUD 测试。"""

    def test_create_tree(self, tree_manager: TreeManager) -> None:
        """创建树——验证标题、根节点、tree_id、系统提示。"""
        tree = tree_manager.create_tree(title="Hello", system_prompt="Be helpful.")
        assert tree.title == "Hello"
        assert tree.root_node is not None
        assert tree.root_node.node_id == 1
        assert tree.root_node.user_message.type == "human"
        assert tree.root_node.user_message.content == ""
        assert tree.system_prompt == "Be helpful."
        assert tree.tree_id != ""
        # 根节点固定为 1，计数器从 2 开始
        assert tree.get_next_node_id() == 2

    def test_list_trees(self, tree_manager: TreeManager) -> None:
        """列出所有树。"""
        t1 = tree_manager.create_tree("A")
        t2 = tree_manager.create_tree("B")
        trees = tree_manager.list_trees()
        assert len(trees) == 2
        ids = {t.tree_id for t in trees}
        assert t1.tree_id in ids
        assert t2.tree_id in ids

    def test_get_tree(self, tree_manager: TreeManager) -> None:
        """按 ID 获取树。"""
        tree = tree_manager.create_tree("Test")
        fetched = tree_manager.get_tree(tree.tree_id)
        assert fetched.title == "Test"

    def test_get_tree_not_found(self, tree_manager: TreeManager) -> None:
        """获取不存在的树应抛出 TreeNotFoundError。"""
        with pytest.raises(TreeNotFoundError):
            tree_manager.get_tree("nonexistent")

    def test_delete_tree(self, tree_manager: TreeManager) -> None:
        """删除树后无法再获取。"""
        tree = tree_manager.create_tree("To Delete")
        tree_id = tree.tree_id
        tree_manager.delete_tree(tree_id)
        with pytest.raises(TreeNotFoundError):
            tree_manager.get_tree(tree_id)

    def test_rename_tree(self, tree_manager: TreeManager) -> None:
        """重命名树。"""
        tree = tree_manager.create_tree("Old")
        tree_manager.rename_tree(tree.tree_id, "New")
        assert tree_manager.get_tree(tree.tree_id).title == "New"


class TestNodeOperations:
    """节点操作测试。"""

    def test_add_child_node(self, tree_manager: TreeManager) -> None:
        """添加子节点——验证 ID、内容、父子关系。"""
        tree = tree_manager.create_tree()
        child, parent = tree_manager.add_child_node(tree.tree_id, 1, "Hello")
        assert child.node_id > 1
        assert child.user_message.content == "Hello"
        assert child.user_message.type == "human"
        assert child in parent.children

    def test_get_full_context(self, tree_manager: TreeManager) -> None:
        """get_full_context 返回 user → assistant 消息链（系统提示由 ChatTree 单独提供）。"""
        tree = tree_manager.create_tree(system_prompt="You are helpful.")
        child, _ = tree_manager.add_child_node(tree.tree_id, 1, "User Q")
        tree_manager.set_ai_reply(tree.tree_id, child.node_id, "AI Answer")

        context = child.get_full_context()
        # 系统提示不再在节点树中，context 仅含 user + assistant
        assert len(context) == 2

        types = [m.type for m in context]
        assert types == ["human", "ai"]

        contents = [m.content for m in context]
        assert contents == ["User Q", "AI Answer"]

        # 系统提示在 ChatTree 层级
        assert tree.system_prompt == "You are helpful."

    def test_branching_context(self, tree_manager: TreeManager) -> None:
        """分支节点的上下文应互相隔离（系统提示在 ChatTree 上单独管理）。"""
        tree = tree_manager.create_tree(system_prompt="SYS")
        # Node A under root
        node_a, _ = tree_manager.add_child_node(tree.tree_id, 1, "Question A")
        tree_manager.set_ai_reply(tree.tree_id, node_a.node_id, "Answer A")
        # Node B under root (branch)
        node_b, _ = tree_manager.add_child_node(tree.tree_id, 1, "Question B")
        tree_manager.set_ai_reply(tree.tree_id, node_b.node_id, "Answer B")

        ctx_a = node_a.get_full_context()
        ctx_b = node_b.get_full_context()

        # 不含系统提示，仅含用户/AI 消息
        assert len(ctx_a) == 2  # Question A, Answer A
        assert len(ctx_b) == 2  # Question B, Answer B
        assert ctx_a[0].content == "Question A"
        assert ctx_b[0].content == "Question B"

        # 系统提示在顶层
        assert tree.system_prompt == "SYS"

    def test_rename_node(self, tree_manager: TreeManager) -> None:
        """重命名节点。"""
        tree = tree_manager.create_tree()
        tree_manager.rename_node(tree.tree_id, 1, "Rooty")
        assert tree_manager.get_node(tree.tree_id, 1).name == "Rooty"

    def test_delete_node(self, tree_manager: TreeManager) -> None:
        """删除子节点后应抛出 NodeNotFoundError。"""
        tree = tree_manager.create_tree()
        child, _ = tree_manager.add_child_node(tree.tree_id, 1, "Msg")
        tree_manager.delete_node(tree.tree_id, child.node_id)
        with pytest.raises(NodeNotFoundError):
            tree_manager.get_node(tree.tree_id, child.node_id)

    def test_delete_root_node_raises(self, tree_manager: TreeManager) -> None:
        """删除根节点应抛出 ValueError。"""
        tree = tree_manager.create_tree()
        with pytest.raises(ValueError):
            tree_manager.delete_node(tree.tree_id, 1)


class TestNodeCount:
    """节点计数测试。"""

    def test_node_count_starts_at_one(self, tree_manager: TreeManager) -> None:
        """新树节点数应为 1（仅根节点）。"""
        tree = tree_manager.create_tree()
        assert tree.node_count == 1

    def test_node_count_after_adding(self, tree_manager: TreeManager) -> None:
        """添加 2 个子节点后节点数应为 3。"""
        tree = tree_manager.create_tree()
        tree_manager.add_child_node(tree.tree_id, 1, "Q1")
        tree_manager.add_child_node(tree.tree_id, 1, "Q2")
        # 需要重新获取树以反映最新状态
        updated: ChatTree = tree_manager.get_tree(tree.tree_id)
        assert updated.node_count == 3
