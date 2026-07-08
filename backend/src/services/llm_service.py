"""LangChain 驱动的 LLM 服务 — 替代原有的 httpx DeepSeekClient。"""

from __future__ import annotations

from collections.abc import AsyncGenerator

from langchain_core.messages import BaseMessage
from langchain_core.language_models.chat_models import BaseChatModel
from langchain_openai import ChatOpenAI
from openai import APIStatusError

from ..core.config import settings
from ..core.errors import LLMError


class LLMService:
    """基于 LangChain ChatOpenAI 的 LLM 服务。

    通过 OpenAI 兼容接口接入 DeepSeek V4 API，
    并为后续添加更多供应商保留扩展点。
    """

    def __init__(self) -> None:
        self._instances: dict[str, BaseChatModel] = {}

    def _get_client(
        self,
        model: str | None = None,
        temperature: float | None = None,
        top_p: float | None = None,
        max_tokens: int | None = None,
    ) -> BaseChatModel:
        """获取或创建 LangChain 聊天模型实例。

        按 (provider, model) 缓存已创建的实例，
        支持按请求覆盖 temperature/top_p/max_tokens（通过 bind）。
        """
        model_name = model or settings.model
        provider = self._resolve_provider(model_name)
        cfg = settings.get_provider_config(provider)

        cache_key = f"{provider}:{model_name}"
        if cache_key not in self._instances:
            self._instances[cache_key] = ChatOpenAI(
                model=model_name,
                api_key=cfg.api_key,
                base_url=cfg.api_base,
                timeout=cfg.timeout_seconds,
                temperature=settings.temperature,
                top_p=settings.top_p,
                max_tokens=settings.max_tokens,
            )

        client = self._instances[cache_key]
        # 按请求覆盖参数
        overrides: dict[str, float | int] = {}
        if temperature is not None:
            overrides["temperature"] = temperature
        if top_p is not None:
            overrides["top_p"] = top_p
        if max_tokens is not None:
            overrides["max_tokens"] = max_tokens
        return client.bind(**overrides) if overrides else client

    def _resolve_provider(self, model: str) -> str:
        """从模型名自动推断供应商。

        例如 "gpt-4o" → "openai"，"deepseek-v4-flash" → "deepseek"。
        """
        m = model.lower()
        if m.startswith(("gpt-", "o1", "o3")):
            return "openai"
        return settings.llm_provider

    async def chat(
        self,
        messages: list[BaseMessage],
        model: str | None = None,
        temperature: float | None = None,
        top_p: float | None = None,
        max_tokens: int | None = None,
    ) -> str:
        """非流式聊天。返回完整回复文本。"""
        client = self._get_client(
            model=model, temperature=temperature, top_p=top_p, max_tokens=max_tokens,
        )
        try:
            response = await client.ainvoke(messages)
            return response.content
        except APIStatusError as e:
            raise _map_openai_error(e)
        except Exception as e:
            raise LLMError(
                key="LLMError",
                message="AI 服务调用失败。",
                detail=str(e),
                status_code=500,
            )

    async def astream(
        self,
        messages: list[BaseMessage],
        model: str | None = None,
        temperature: float | None = None,
        top_p: float | None = None,
        max_tokens: int | None = None,
    ) -> AsyncGenerator[str, None]:
        """流式聊天。逐 token 产出内容字符串。

        直接接受 LangChain BaseMessage 列表（无需 dict 转换）。
        """
        client = self._get_client(
            model=model, temperature=temperature, top_p=top_p, max_tokens=max_tokens,
        )
        try:
            async for chunk in client.astream(messages):
                content = chunk.content
                if content:
                    yield content
        except APIStatusError as e:
            raise _map_openai_error(e)
        except Exception as e:
            raise LLMError(
                key="LLMError",
                message="AI 服务调用失败。",
                detail=str(e),
                status_code=500,
            )

    async def close(self) -> None:
        """清理资源。"""
        self._instances.clear()


def _map_openai_error(error: APIStatusError) -> LLMError:
    """将 OpenAI SDK 异常映射为应用的 LLMError。"""
    status_code = error.status_code
    detail = str(error.body) if error.body else str(error)

    mapping: dict[int, tuple[str, str]] = {
        401: ("AuthenticationError", "API 密钥无效，请检查配置。"),
        403: ("Forbidden", "禁止访问，请检查 API 密钥权限。"),
        422: ("ValidationError", "请求体验证失败，请检查参数。"),
        429: ("RateLimitExceeded", "请求过于频繁，请稍后重试。"),
        500: ("ServerError", "AI 服务内部错误。"),
        503: ("ServiceUnavailable", "服务暂时不可用，请稍后重试。"),
    }

    key, message = mapping.get(
        status_code,
        ("HttpError", f"HTTP {status_code} 错误"),
    )
    return LLMError(key=key, message=message, detail=detail, status_code=status_code)


# 全局单例
llm_service = LLMService()
