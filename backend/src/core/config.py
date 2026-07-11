"""应用配置，从 config.json 和 config.example.json 中加载。支持多 LLM 供应商。

设计说明：
- config.json（项目根目录，.gitignore 中排除）存放用户配置（含 API Key）
- config.example.json（进 git）存放默认值模板
- 当 config.json 缺失时，自动从 config.example.json 复制一份
- HOST / PORT 使用代码默认值，无需配置文件
- 支持多 Profile（配置画像），每个 Profile 自包含 provider + 凭证 + 参数
"""

import json
import logging
import shutil
from pathlib import Path
from typing import Any

from pydantic import BaseModel, Field

from ..core.errors import ConfigError

logger = logging.getLogger(__name__)

# ---------------------------------------------------------------------------
# Profile 数据模型
# ---------------------------------------------------------------------------


class ProfileData(BaseModel):
    """单个模型配置画像。"""

    name: str
    provider: str = "deepseek"
    api_key: str = ""
    api_endpoint: str = "https://api.deepseek.com"
    model: str = "deepseek-v4-flash"
    temperature: float = Field(default=0.7, ge=0.0, le=2.0)
    top_p: float = Field(default=0.8, ge=0.0, le=1.0)
    max_tokens: int = Field(default=800, ge=1, le=8192)


class LLMProviderConfig(BaseModel):
    """单个 LLM 供应商的配置。"""

    api_key: str = ""
    api_base: str = ""
    model: str = ""
    timeout_seconds: int = 120


# ---------------------------------------------------------------------------
# 全局 Settings
# ---------------------------------------------------------------------------


class Settings(BaseModel):
    """全局配置单例。"""

    # --- 服务器（代码默认值，非用户可配）---
    host: str = "127.0.0.1"
    port: int = 8800

    # --- LLM 供应商 ---
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

    # --- Profile（v2 配置格式）---
    version: int = 2
    active_profile: str = "default"
    profiles: list[ProfileData] = []

    # ------------------------------------------------------------------
    # 配置文件加载
    # ------------------------------------------------------------------

    def load_config_file(self) -> None:
        """从项目根目录的 config.json 加载用户配置。

        若 config.json 不存在，则尝试从 config.example.json 复制一份。
        自动检测 v1（旧平面格式）并迁移到 v2（profiles 格式）。
        """
        config_path = Path(__file__).resolve().parents[3] / "config.json"
        logger.info("Loading config from: %s", config_path)

        if not config_path.exists():
            logger.warning("config.json not found, attempting fallback")
            example_path = config_path.with_name("config.example.json")
            if example_path.exists():
                shutil.copy(example_path, config_path)
                logger.info("Copied config.example.json → config.json")
            else:
                logger.warning("config.example.json also missing, using code defaults")
                return

        try:
            with open(config_path, encoding="utf-8") as f:
                data: dict[str, Any] = json.load(f)
        except json.JSONDecodeError:
            logger.warning("config.json is malformed, using code defaults")
            return
        except OSError as e:
            logger.warning("Cannot read config.json: %s, using code defaults", e)
            return

        # ---- 格式检测与迁移 ----
        if "profiles" not in data:
            logger.info("Detected v1 config format, migrating to v2…")
            self._migrate_v1_to_v2(data, config_path)
            # 迁移后重新读取
            try:
                with open(config_path, encoding="utf-8") as f:
                    data = json.load(f)
            except Exception:
                logger.warning("Failed to re-read migrated config, using defaults")
                return
        else:
            logger.info("Detected v2 config format")

        # ---- 解析 profiles ----
        self.active_profile = data.get("active_profile", "default")
        raw_profiles = data.get("profiles", [])
        if raw_profiles:
            self.profiles = [ProfileData(**p) for p in raw_profiles]
        else:
            logger.warning("profiles list is empty, using default")
            self.profiles = [ProfileData(name="default")]

        # 确保 active_profile 有效
        if not any(p.name == self.active_profile for p in self.profiles):
            self.active_profile = self.profiles[0].name
            logger.warning("active_profile not found, falling back to: %s", self.active_profile)

        # 同步激活的 profile 到 flat / provider 字段
        self._sync_active_profile_to_fields()

    def _migrate_v1_to_v2(self, data: dict[str, Any], config_path: Path) -> None:
        """将 v1 平面配置迁移到 v2 profiles 格式。"""
        profile = ProfileData(
            name="default",
            provider="deepseek",
            api_key=data.get("api_key", ""),
            api_endpoint=data.get("api_endpoint", "https://api.deepseek.com"),
            model=data.get("model", "deepseek-v4-flash"),
            temperature=float(data.get("temperature", 0.7)),
            top_p=float(data.get("top_p", 0.8)),
            max_tokens=int(data.get("max_tokens", 800)),
        )
        new_data = {
            "version": 2,
            "active_profile": "default",
            "profiles": [profile.model_dump()],
        }
        try:
            with open(config_path, "w", encoding="utf-8") as f:
                json.dump(new_data, f, indent=2, ensure_ascii=False)
            logger.info("Migrated config.json v1 → v2")
        except OSError as e:
            logger.warning("Failed to write migrated config: %s", e)

    # ------------------------------------------------------------------
    # 同步
    # ------------------------------------------------------------------

    def _sync_active_profile_to_fields(self) -> None:
        """将激活的 profile 值同步到 flat 字段和 provider 前缀字段。"""
        profile = self.get_active_profile()
        if profile is None:
            return

        # Flat 字段（用于运行时 ConfigData / 旧代码路径）
        self.model = profile.model
        self.temperature = profile.temperature
        self.top_p = profile.top_p
        self.max_tokens = profile.max_tokens

        # Provider 前缀字段（用于 get_provider_config）
        provider = profile.provider
        setattr(self, f"{provider}_api_key", profile.api_key)
        setattr(self, f"{provider}_api_base", profile.api_endpoint)
        setattr(self, f"{provider}_model", profile.model)

        # 同步 llm_provider
        self.llm_provider = provider

    # ------------------------------------------------------------------
    # 持久化
    # ------------------------------------------------------------------

    def save_config_file(self) -> None:
        """将当前 profiles 写回 config.json。"""
        config_path = Path(__file__).resolve().parents[3] / "config.json"
        data = {
            "version": 2,
            "active_profile": self.active_profile,
            "profiles": [p.model_dump() for p in self.profiles],
        }
        try:
            with open(config_path, "w", encoding="utf-8") as f:
                json.dump(data, f, indent=2, ensure_ascii=False)
            logger.info("Config saved to: %s (profiles=%d)", config_path, len(self.profiles))
        except OSError as e:
            logger.warning("Failed to save config: %s", e)
            raise ConfigError(f"无法保存配置文件: {e}")

    # ------------------------------------------------------------------
    # Profile CRUD
    # ------------------------------------------------------------------

    def get_profile(self, name: str) -> ProfileData | None:
        """按名称获取 profile。"""
        for p in self.profiles:
            if p.name == name:
                return p
        return None

    def get_active_profile(self) -> ProfileData | None:
        """获取当前激活的 profile。"""
        return self.get_profile(self.active_profile)

    def add_profile(self, profile: ProfileData) -> None:
        """添加新 profile。名称必须唯一。"""
        if self.get_profile(profile.name) is not None:
            raise ConfigError(f"Profile '{profile.name}' 已存在")
        self.profiles.append(profile)
        self.save_config_file()

    def update_profile(self, name: str, updates: dict[str, Any]) -> ProfileData:
        """更新指定 profile 的字段。返回更新后的 profile。"""
        profile = self.get_profile(name)
        if profile is None:
            raise ConfigError(f"Profile '{name}' 不存在")

        for key, value in updates.items():
            if hasattr(profile, key) and value is not None:
                setattr(profile, key, value)

        # 如果更新的是激活中的 profile，同步到 flat 字段
        if name == self.active_profile:
            self._sync_active_profile_to_fields()

        self.save_config_file()
        return profile

    def delete_profile(self, name: str) -> None:
        """删除 profile。禁止删除激活中的 profile。"""
        if name == self.active_profile:
            raise ConfigError("不能删除当前激活的 Profile，请先切换到其他 Profile")

        profile = self.get_profile(name)
        if profile is None:
            raise ConfigError(f"Profile '{name}' 不存在")

        self.profiles = [p for p in self.profiles if p.name != name]
        self.save_config_file()

    def set_active_profile(self, name: str) -> ProfileData:
        """激活指定 profile。"""
        profile = self.get_profile(name)
        if profile is None:
            raise ConfigError(f"Profile '{name}' 不存在")

        self.active_profile = name
        self._sync_active_profile_to_fields()

        # 清理 LLM 客户端缓存
        from ..services.llm_service import llm_service

        llm_service._instances.clear()
        logger.info("LLM client cache cleared due to profile switch")

        self.save_config_file()
        return profile

    # ------------------------------------------------------------------
    # 供应商配置兼容
    # ------------------------------------------------------------------

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
