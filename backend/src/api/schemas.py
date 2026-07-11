"""Pydantic 请求/响应模型。"""

from __future__ import annotations

from typing import TYPE_CHECKING, Literal

from pydantic import BaseModel, Field

if TYPE_CHECKING:
    from ..models.chat_tree_node import ChatTreeNode


# isort: off
from ..models.chat_message import message_to_role  # noqa: E402
# isort: on


# === Tree ===

class CreateTreeRequest(BaseModel):
    title: str = "新对话"
    system_prompt: str = "你是一个有帮助的AI助手。"


class RenameTreeRequest(BaseModel):
    title: str


class TreeSummary(BaseModel):
    tree_id: str
    title: str
    created_at: str
    node_count: int


class TreeListResponse(BaseModel):
    trees: list[TreeSummary]


# === Node (serialized for WPF) ===

class NodeMessage(BaseModel):
    role: str
    content: str


class TreeNodeData(BaseModel):
    """递归节点结构，用于 WPF 渲染树图。"""
    node_id: int
    name: str | None = None
    user_message: NodeMessage
    reply_message: NodeMessage | None = None
    children: list[TreeNodeData] = []


class TreeDetailResponse(BaseModel):
    tree_id: str
    title: str
    created_at: str
    system_prompt: str = ""
    root_node: TreeNodeData


# === Chat ===

class ChatRequest(BaseModel):
    parent_node_id: int
    message: str = Field(..., min_length=1)
    profile_name: str | None = None  # NEW: 指定使用的 profile
    model: str | None = None
    temperature: float | None = Field(default=None, ge=0.0, le=2.0)
    top_p: float | None = Field(default=None, ge=0.0, le=1.0)
    max_tokens: int | None = Field(default=None, ge=1, le=8192)


# === Node Operations ===

class RenameNodeRequest(BaseModel):
    name: str = Field(..., min_length=1)


# === Config ===

class ConfigData(BaseModel):
    model: str = "deepseek-v4-flash"
    temperature: float = Field(default=0.7, ge=0.0, le=2.0)
    top_p: float = Field(default=0.8, ge=0.0, le=1.0)
    max_tokens: int = Field(default=800, ge=1, le=8192)


# === Profile ===

class ProfileData(BaseModel):
    """配置画像 — 一组完整的模型连接参数。"""
    name: str
    provider: str = "deepseek"
    api_key: str = ""
    api_endpoint: str = "https://api.deepseek.com"
    model: str = "deepseek-v4-flash"
    temperature: float = Field(default=0.7, ge=0.0, le=2.0)
    top_p: float = Field(default=0.8, ge=0.0, le=1.0)
    max_tokens: int = Field(default=800, ge=1, le=8192)


class ProfileListResponse(BaseModel):
    profiles: list[ProfileData]
    active_profile: str


class ActivateProfileResponse(BaseModel):
    active_profile: str
    config: ConfigData


# === Error ===

class ErrorDetail(BaseModel):
    key: str
    message: str
    detail: str | None = None
    status_code: int | None = None


# === Generic Responses ===

class SuccessResponse(BaseModel):
    success: bool = True


class HealthResponse(BaseModel):
    status: str = "ok"
    version: str = "0.1.0"


# === Serialization ===

class SerializeResponse(BaseModel):
    json_content: str


class DeserializeRequest(BaseModel):
    json_content: str
    title: str | None = None


# === Helper: convert internal model to API response ===

def _node_to_data(node: ChatTreeNode) -> TreeNodeData:
    """将内部 ChatTreeNode 转为 API 响应 TreeNodeData。"""
    return TreeNodeData(
        node_id=node.node_id,
        name=node.name,
        user_message=NodeMessage(
            role=message_to_role(node.user_message),
            content=node.user_message.content,
        ),
        reply_message=(
            NodeMessage(
                role=message_to_role(node.reply_message),
                content=node.reply_message.content,
            )
            if node.reply_message
            else None
        ),
        children=[_node_to_data(c) for c in node.children],
    )
