"""测试 LLMService（LangChain 驱动的 LLM 服务）。"""

from __future__ import annotations

from unittest.mock import MagicMock, patch

import httpx
import pytest
from openai import APIStatusError

from src.services.llm_service import LLMService, _map_openai_error
from src.core.errors import LLMError


class TestLLMService:
    """LLMService 单元测试。"""

    @patch("src.services.llm_service.ChatOpenAI")
    def test_client_caching(self, mock_chat_openai: MagicMock) -> None:
        """相同 model+provider 应复用实例。"""
        service = LLMService()
        client1 = service._get_client(model="test-model")
        client2 = service._get_client(model="test-model")
        assert client1 is client2
        # ChatOpenAI 应只被创建一次
        mock_chat_openai.assert_called_once()

    @patch("src.services.llm_service.ChatOpenAI")
    def test_different_models_different_clients(self, mock_chat_openai: MagicMock) -> None:
        """不同 model 应创建不同实例。"""
        service = LLMService()
        # 每次调用 _get_client 返回不同的 mock
        mock_chat_openai.side_effect = [MagicMock(), MagicMock()]
        client1 = service._get_client(model="model-a")
        client2 = service._get_client(model="model-b")
        assert client1 is not client2
        assert mock_chat_openai.call_count == 2

    def test_provider_resolution(self) -> None:
        """从模型名自动推断供应商。"""
        service = LLMService()
        assert service._resolve_provider("gpt-4o") == "openai"
        assert service._resolve_provider("o1-preview") == "openai"
        assert service._resolve_provider("deepseek-v4-flash") == "deepseek"
        assert service._resolve_provider("unknown-model") == "deepseek"

    def _make_api_error(self, status_code: int, message: str, body: object) -> APIStatusError:
        """创建一个模拟的 APIStatusError。"""
        response = MagicMock(spec=httpx.Response)
        response.status_code = status_code
        response.request = MagicMock()
        response.headers = {"x-request-id": "test"}
        return APIStatusError(
            message=message,
            response=response,
            body=body,
        )

    def test_error_mapping_401(self) -> None:
        """401 错误应映射为 AuthenticationError。"""
        error = self._make_api_error(401, "Unauthorized", {"error": {"message": "Invalid API key"}})
        result = _map_openai_error(error)
        assert isinstance(result, LLMError)
        assert result.key == "AuthenticationError"
        assert result.status_code == 401

    def test_error_mapping_429(self) -> None:
        """429 错误应映射为 RateLimitExceeded。"""
        error = self._make_api_error(429, "Rate Limit", {"error": {"message": "Too many requests"}})
        result = _map_openai_error(error)
        assert isinstance(result, LLMError)
        assert result.key == "RateLimitExceeded"

    def test_error_mapping_unknown(self) -> None:
        """未映射的状态码应使用 HttpError。"""
        error = self._make_api_error(418, "Teapot", {"error": {"message": "I'm a teapot"}})
        result = _map_openai_error(error)
        assert isinstance(result, LLMError)
        assert result.key == "HttpError"
        assert result.status_code == 418
