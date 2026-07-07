"""应用配置，从 .env 文件和环境变量中加载。"""

from pydantic_settings import BaseSettings, SettingsConfigDict


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

    # --- DeepSeek API ---
    deepseek_api_key: str = ""
    deepseek_api_base: str = "https://api.deepseek.com"
    deepseek_model: str = "deepseek-chat"
    deepseek_default_temperature: float = 0.7
    deepseek_default_top_p: float = 0.8
    deepseek_default_top_k: int = 20
    deepseek_default_max_tokens: int = 800
    deepseek_timeout_seconds: int = 120

    # --- 运行时配置（可由 WPF 通过 PUT /api/v1/config 动态覆盖）---
    model: str = "deepseek-chat"
    temperature: float = 0.7
    top_p: float = 0.8
    top_k: int = 20
    max_tokens: int = 800


# 全局单例
settings = Settings()
