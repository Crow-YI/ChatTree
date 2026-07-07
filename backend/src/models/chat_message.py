"""聊天消息单元。"""

from pydantic import BaseModel, Field
from typing import Literal


class ChatMessage(BaseModel):
    """单条消息（不可变值对象）。"""

    role: Literal["system", "user", "assistant"]
    content: str = ""

    def to_api_dict(self) -> dict[str, str]:
        """转为 OpenAI 兼容的 dict 格式。"""
        return {"role": self.role, "content": self.content}
