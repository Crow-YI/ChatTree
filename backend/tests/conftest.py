"""pytest 配置和 fixtures。"""

from __future__ import annotations

import sys
from collections.abc import Generator
from pathlib import Path

import pytest

# 确保 src 在 path 中
sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "src"))

from src.services.tree_manager import TreeManager  # noqa: E402
from src.models.chat_tree import ChatTree  # noqa: E402


@pytest.fixture
def tree_manager() -> TreeManager:
    """返回全新的 TreeManager 实例。"""
    return TreeManager()


@pytest.fixture
def sample_tree(tree_manager: TreeManager) -> ChatTree:
    """创建一个示例对话树。"""
    return tree_manager.create_tree(
        title="Test Tree", system_prompt="You are a test assistant."
    )
