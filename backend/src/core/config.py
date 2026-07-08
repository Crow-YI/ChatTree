"""应用配置，从 .env 文件和环境变量中加载。支持多 LLM 供应商。

设计说明：保持 flat 字段用于从 .env 加载（兼容原有 DEEPSEEK_API_KEY 等变量名），
在 get_provider_config 中动态构造 LLMProviderConfig 对象。
"""

from pydantic import BaseModel  # noqa: E402 — pydantic BaseModel，非 pydantic_settings
from pydantic_settings import BaseSettings, SettingsConfigDict

from ..core.errors import ConfigError


class LLMProviderConfig(BaseModel):
    """单个 LLM 供应商的配置。"""

    api_key: str = ""
    api_base: str = ""
    model: str = ""
    timeout_seconds: int = 120


class Settings(BaseSettings):
    """全局配置单例。"""

    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",
    )

    # --- 服务器 ---
    host: str = "127.0.0.1"
    port: int = 8800

    # --- LLM 供应商选择 ---
    llm_provider: str = "deepseek"

    # --- DeepSeek（flat 字段，从 .env 加载 DEEPSEEK_API_KEY 等）---
    deepseek_api_key: str = ""
    deepseek_api_base: str = "https://api.deepseek.com"
    deepseek_model: str = "deepseek-v4-flash"
    deepseek_timeout_seconds: int = 120

    # --- OpenAI（备用）---
    openai_api_key: str = ""
    openai_api_base: str = ""
    openai_model: str = "gpt-4o-mini"
    openai_timeout_seconds: int = 120

    # --- 运行时配置（可由 WPF 通过 PUT /api/v1/config 动态覆盖）---
    model: str = "deepseek-v4-flash"
    temperature: float = 0.7
    top_p: float = 0.8
    top_k: int = 20
    max_tokens: int = 800

    @property
    def active_provider(self) -> LLMProviderConfig:
        """返回当前激活的供应商配置。"""
        return self.get_provider_config(self.llm_provider)

    def get_provider_config(self, name: str) -> LLMProviderConfig:
        """按名称构造供应商配置。"""
        prefix = name  # "deepseek" or "openai"
        try:
            return LLMProviderConfig(
                api_key=getattr(self, f"{prefix}_api_key"),
                api_base=getattr(self, f"{prefix}_api_base"),
                model=getattr(self, f"{prefix}_model"),
                timeout_seconds=getattr(self, f"{prefix}_timeout_seconds", 120),
            )
        except AttributeError:
            raise ConfigError(f"未知的 LLM 供应商: {name}")


# 全局单例
settings = Settings()
