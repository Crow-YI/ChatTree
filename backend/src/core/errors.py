"""自定义异常体系。"""

import httpx


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


# --- HTTP 错误映射 ---

_HTTP_ERROR_MAP: dict[int, tuple[str, str]] = {
    401: ("AuthenticationError", "API密钥无效，请检查配置。"),
    403: ("Forbidden", "禁止访问，请检查API密钥权限。"),
    422: ("ValidationError", "请求体验证失败，请检查参数。"),
    429: ("RateLimitExceeded", "请求过于频繁，请稍后重试。"),
    500: ("ServerError", "服务器内部错误。"),
    503: ("ServiceUnavailable", "服务暂时不可用，请稍后重试。"),
}


def map_http_error(exc: httpx.HTTPStatusError) -> LLMError:
    """将 httpx HTTP 错误映射为 LLMError。"""
    status = exc.response.status_code
    key, message = _HTTP_ERROR_MAP.get(
        status,
        ("HttpError", f"HTTP {status} 错误"),
    )

    # 尝试从响应体提取详细错误信息
    detail: str | None = None
    try:
        # 流式响应需要先 read() 才能访问 .json() / .text
        # 直接尝试 .json()，如果失败再尝试 .text，都失败则忽略
        try:
            body = exc.response.json()
        except Exception:
            # .json() 失败（可能是流式响应尚未读取），回退到 .text
            try:
                text = exc.response.text
                if text and len(text) <= 500:
                    detail = text
                elif text:
                    detail = text[:500]
            except Exception:
                pass
        else:
            if isinstance(body, dict):
                detail = body.get("error", {}).get("message", str(body))
    except Exception:
        pass

    return LLMError(key=key, message=message, detail=detail, status_code=status)
