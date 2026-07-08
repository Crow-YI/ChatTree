"""消息类型定义与 LangChain 集成转换层。

将 LangChain 的 BaseMessage 子类型作为内部消息表示，
同时提供与 WPF 前端（OpenAI 兼容格式）之间的转换函数。
"""

from langchain_core.messages import (
    BaseMessage,
    SystemMessage,
    HumanMessage,
    AIMessage,
)

# LangChain type → API role 映射
# LangChain 使用 "human"/"ai"，OpenAI/WPF 使用 "user"/"assistant"
_TYPE_TO_ROLE: dict[str, str] = {
    "system": "system",
    "human": "user",
    "ai": "assistant",
}

_ROLE_TO_TYPE: dict[str, str] = {v: k for k, v in _TYPE_TO_ROLE.items()}


def message_to_role(msg: BaseMessage) -> str:
    """从 LangChain 消息获取 OpenAI 兼容的角色字符串。"""
    return _TYPE_TO_ROLE.get(msg.type, msg.type)


def message_from_role(role: str, content: str) -> BaseMessage:
    """从 OpenAI 兼容角色字符串创建 LangChain 消息。"""
    type_ = _ROLE_TO_TYPE.get(role, role)
    if type_ == "human":
        return HumanMessage(content=content)
    if type_ == "ai":
        return AIMessage(content=content)
    return SystemMessage(content=content)


def message_to_api_dict(msg: BaseMessage) -> dict[str, str]:
    """将 LangChain 消息转为 OpenAI 兼容的 API dict。"""
    return {"role": message_to_role(msg), "content": msg.content}


__all__ = [
    "BaseMessage",
    "SystemMessage",
    "HumanMessage",
    "AIMessage",
    "message_to_role",
    "message_from_role",
    "message_to_api_dict",
]
