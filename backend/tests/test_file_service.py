"""测试 FileService 序列化/反序列化。"""

from __future__ import annotations

from src.services.file_service import file_service
from src.services.tree_manager import TreeManager
from src.models.chat_tree import ChatTree


class TestFileService:
    """FileService 单元测试。"""

    def test_serialize_roundtrip(self) -> None:
        """序列化 → 反序列化 → 数据一致。"""
        tm = TreeManager()
        tree = tm.create_tree(title="Test Tree", system_prompt="Be helpful.")
        tm.add_child_node(tree.tree_id, 1, "Hello")
        child, _ = tm.add_child_node(tree.tree_id, 1, "World")
        tm.set_ai_reply(tree.tree_id, child.node_id, "AI says hi")

        # 序列化
        json_str = file_service.serialize(tree)
        assert "Test Tree" in json_str
        assert "Hello" in json_str
        assert "AI says hi" in json_str

        # 反序列化
        loaded: ChatTree = file_service.deserialize(json_str)
        assert loaded.title == "Test Tree"
        assert loaded.root_node.node_id == 1
        assert loaded.root_node.user_message.content == ""
        assert loaded.root_node.user_message.type == "human"
        assert loaded.system_prompt == "Be helpful."
        assert len(loaded.root_node.children) == 2

    def test_deserialize_preserves_children(self) -> None:
        """反序列化保留递归子节点结构。"""
        tm = TreeManager()
        tree = tm.create_tree()
        c1, _ = tm.add_child_node(tree.tree_id, 1, "Q1")
        tm.add_child_node(tree.tree_id, c1.node_id, "Q1.1")

        json_str = file_service.serialize(tree)
        loaded: ChatTree = file_service.deserialize(json_str)

        root_children = loaded.root_node.children
        assert len(root_children) == 1
        assert len(root_children[0].children) == 1
        assert root_children[0].children[0].user_message.content == "Q1.1"

    def test_serialize_empty_tree(self) -> None:
        """空树序列化往返测试。"""
        tm = TreeManager()
        tree = tm.create_tree()
        json_str = file_service.serialize(tree)
        loaded: ChatTree = file_service.deserialize(json_str)
        assert loaded.title == "新对话"
        assert loaded.root_node.children == []

    def test_custom_title_on_load(self) -> None:
        """加载时自定义标题覆盖原标题。"""
        tm = TreeManager()
        tree = tm.create_tree(title="Original")
        json_str = file_service.serialize(tree)
        loaded: ChatTree = file_service.deserialize(json_str, title="Custom Title")
        assert loaded.title == "Custom Title"
