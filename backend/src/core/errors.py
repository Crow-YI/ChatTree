"""自定义异常体系。"""


class TreeChatError(Exception):
    """应用基础异常。"""


class LLMError(TreeChatError):
    """大模型 API 调用相关错误。"""

    def __init__(
        self,
        key: str,
        message: str,
        detail: str | None = None,
        status_code: int | None = None,
    ):
        self.key = key
        self.message = message
        self.detail = detail
        self.status_code = status_code
        super().__init__(message)


class ConfigError(TreeChatError):
    """配置相关错误。"""


class TreeNotFoundError(TreeChatError):
    """对话树不存在。"""


class NodeNotFoundError(TreeChatError):
    """树节点不存在。"""
