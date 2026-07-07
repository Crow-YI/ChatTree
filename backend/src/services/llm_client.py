"""DeepSeek API 客户端 — 流式 + 非流式聊天。"""

from __future__ import annotations

import json
from collections.abc import AsyncGenerator
from typing import Any

import httpx

from ..core.config import settings
from ..core.errors import LLMError, map_http_error


class DeepSeekClient:
    """对 DeepSeek V4 API 的异步 HTTP 封装。"""

    def __init__(self) -> None:
        self._client: httpx.AsyncClient | None = None

    async def _get_client(self) -> httpx.AsyncClient:
        """懒加载获取或创建 httpx 客户端。"""
        if self._client is None:
            self._client = httpx.AsyncClient(
                base_url=settings.deepseek_api_base,
                timeout=httpx.Timeout(settings.deepseek_timeout_seconds),
                headers={
                    "Authorization": f"Bearer {settings.deepseek_api_key}",
                    "Content-Type": "application/json",
                },
            )
        return self._client

    async def close(self) -> None:
        """关闭 HTTP 客户端连接。"""
        if self._client:
            await self._client.aclose()
            self._client = None

    def _build_payload(
        self,
        messages: list[dict[str, str]],
        model: str | None = None,
        temperature: float | None = None,
        top_p: float | None = None,
        top_k: int | None = None,
        max_tokens: int | None = None,
        stream: bool = False,
    ) -> dict[str, object]:
        """构建 API 请求体。

        注意: DeepSeek API 是 OpenAI 兼容的，不支持 top_k 参数。
        top_k 仅保留在项目配置中供 UI 使用，不会发送到 API。
        """
        payload: dict[str, object] = {
            "model": model or settings.model,
            "messages": messages,
            "temperature": temperature if temperature is not None else settings.temperature,
            "top_p": top_p if top_p is not None else settings.top_p,
            "max_tokens": max_tokens if max_tokens is not None else settings.max_tokens,
            "stream": stream,
        }
        return payload

    async def chat(
        self,
        messages: list[dict[str, str]],
        model: str | None = None,
        temperature: float | None = None,
        top_p: float | None = None,
        top_k: int | None = None,
        max_tokens: int | None = None,
    ) -> dict[str, Any]:
        """非流式聊天。返回完整的 API 响应 JSON。"""
        payload = self._build_payload(
            messages=messages,
            model=model,
            temperature=temperature,
            top_p=top_p,
            top_k=top_k,
            max_tokens=max_tokens,
            stream=False,
        )

        client = await self._get_client()
        try:
            response = await client.post("/chat/completions", json=payload)
            response.raise_for_status()
            return response.json()
        except httpx.HTTPStatusError as e:
            raise map_http_error(e)
        except httpx.RequestError as e:
            raise LLMError(
                key="NetworkError",
                message="网络连接失败：无法访问 DeepSeek API，请检查网络或API地址。",
                detail=str(e),
            )

    async def chat_stream(
        self,
        messages: list[dict[str, str]],
        model: str | None = None,
        temperature: float | None = None,
        top_p: float | None = None,
        top_k: int | None = None,
        max_tokens: int | None = None,
    ) -> AsyncGenerator[str, None]:
        """流式聊天。逐 token 产出内容字符串。"""
        payload = self._build_payload(
            messages=messages,
            model=model,
            temperature=temperature,
            top_p=top_p,
            top_k=top_k,
            max_tokens=max_tokens,
            stream=True,
        )

        client = await self._get_client()
        try:
            async with client.stream("POST", "/chat/completions", json=payload) as response:
                response.raise_for_status()
                async for line in response.aiter_lines():
                    if not line or not line.startswith("data: "):
                        continue
                    data_str = line[6:]  # 去掉 "data: " 前缀
                    if data_str.strip() == "[DONE]":
                        break
                    try:
                        data: dict[str, Any] = json.loads(data_str)
                        delta: dict[str, Any] = data.get("choices", [{}])[0].get("delta", {})
                        content: str = delta.get("content", "")
                        if content:
                            yield content
                    except (json.JSONDecodeError, IndexError, KeyError):
                        # 跳过解析失败的行
                        continue
        except httpx.HTTPStatusError as e:
            raise map_http_error(e)
        except httpx.RequestError as e:
            raise LLMError(
                key="NetworkError",
                message="网络连接失败：无法访问 DeepSeek API，请检查网络或API地址。",
                detail=str(e),
            )


# 全局单例
llm_client = DeepSeekClient()
