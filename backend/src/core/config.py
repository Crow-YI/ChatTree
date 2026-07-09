"""应用配置，从 config.json 和 config.example.json 中加载。支持多 LLM 供应商。

设计说明：
- config.json（项目根目录，.gitignore 中排除）存放用户配置（含 API Key）
- config.example.json（进 git）存放默认值模板
- 当 config.json 缺失时，自动从 config.example.json 复制一份
- HOST / PORT 使用代码默认值，无需配置文件
"""

import json
import shutil
from pathlib import Path

from pydantic import BaseModel

from ..core.errors import ConfigError


class LLMProviderConfig(BaseModel):
    """单个 LLM 供应商的配置。"""

    api_key: str = ""
    api_base: str = ""
    model: str = ""
    timeout_seconds: int = 120


class Settings(BaseModel):
    """全局配置单例。"""

    # --- 服务器（代码默认值，非用户可配）---
    host: str = "127.0.0.1"
    port: int = 8800

    # --- LLM 供应商选择 ---
    llm_provider: str = "deepseek"

    # --- DeepSeek（从 config.json 加载，或从 config.example.json 初始化）---
    deepseek_api_key: str = ""
    deepseek_api_base: str = "https://api.deepseek.com"
    deepseek_model: str = "deepseek-v4-flash"
    deepseek_timeout_seconds: int = 120

    # --- OpenAI（备用）---
    openai_api_key: str = ""
    openai_api_base: str = ""
    openai_model: str = "gpt-4o-mini"
    openai_timeout_seconds: int = 120

    # --- 运行时配置（由 WPF 通过 PUT /api/v1/config 动态覆盖，或从 config.json 加载）---
    model: str = "deepseek-v4-flash"
    temperature: float = 0.7
    top_p: float = 0.8
    max_tokens: int = 800

    def load_config_file(self) -> None:
        """从项目根目录的 config.json 加载用户配置。

        若 config.json 不存在，则尝试从 config.example.json 复制一份。
        映射关系：
          api_key      → {provider}_api_key
          api_endpoint → {provider}_api_base
          model        → model
          temperature  → temperature
          top_p        → top_p
          max_tokens   → max_tokens
        """
        config_path = Path(__file__).resolve().parents[3] / "config.json"

        if not config_path.exists():
            example_path = config_path.with_name("config.example.json")
            if example_path.exists():
                shutil.copy(example_path, config_path)
            else:
                return  # 都缺失则使用代码默认值

        try:
            with open(config_path, encoding="utf-8") as f:
                data = json.load(f)
        except (json.JSONDecodeError, OSError):
            return  # 文件损坏或不可读时静默忽略

        # 供应商相关字段（根据当前激活的供应商映射）
        provider = self.llm_provider
        if "api_key" in data:
            setattr(self, f"{provider}_api_key", data["api_key"])
        if "api_endpoint" in data:
            setattr(self, f"{provider}_api_base", data["api_endpoint"])

        # 直接映射字段
        for field in ("model", "temperature", "top_p", "max_tokens"):
            if field in data:
                setattr(self, field, data[field])

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


# 全局单例（启动时自动从 config.json 加载）
settings = Settings()
settings.load_config_file()
