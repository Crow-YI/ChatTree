"""日志配置 — 统一日志初始化。

用法（在 main.py 中）:
    from src.core.logger import setup_logging
    setup_logging(file_level="INFO", console_level="WARNING")

支持通过 ``--log-level NORMAL|DEBUG`` CLI 参数控制日志详细程度。
"""

from __future__ import annotations

import logging
import sys
from logging.handlers import TimedRotatingFileHandler
from pathlib import Path


def setup_logging(
    log_dir: str | None = None,
    file_level: str = "INFO",
    console_level: str | None = None,
) -> None:
    """初始化全局日志配置。

    Args:
        log_dir: 日志目录，默认取项目根目录下的 ``logs/``。
        file_level: 文件日志级别（DEBUG / INFO / WARNING / ERROR）。
        console_level: 控制台日志级别，默认比 *file_level* 高一级。
    """
    # --- 日志目录 ---
    if log_dir is None:
        log_dir = Path(__file__).resolve().parents[3] / "logs"
    else:
        log_dir = Path(log_dir)
    log_dir.mkdir(parents=True, exist_ok=True)

    # --- 控制台默认比文件高一级 ---
    if console_level is None:
        _level_map = {
            "DEBUG": "INFO",
            "INFO": "WARNING",
            "WARNING": "ERROR",
            "ERROR": "ERROR",
        }
        console_level = _level_map.get(file_level.upper(), "WARNING")

    # --- Root logger 取两者中较低的级别，让 Handler 各自过滤 ---
    root_level = min(
        getattr(logging, file_level.upper(), logging.INFO),
        getattr(logging, console_level.upper(), logging.WARNING),
    )
    root_logger = logging.getLogger()
    root_logger.setLevel(root_level)

    # --- Formatters ---
    file_fmt = logging.Formatter(
        "%(asctime)s | %(levelname)-8s | %(name)s:%(lineno)d | %(message)s",
        datefmt="%Y-%m-%d %H:%M:%S",
    )
    console_fmt = logging.Formatter(
        "%(levelname)-8s | %(message)s",
    )

    # --- File Handler（按天轮转，保留 7 天）---
    file_handler = TimedRotatingFileHandler(
        filename=log_dir / "treechat.log",
        when="midnight",
        interval=1,
        backupCount=7,
        encoding="utf-8",
    )
    file_handler.setLevel(getattr(logging, file_level.upper(), logging.INFO))
    file_handler.setFormatter(file_fmt)
    root_logger.addHandler(file_handler)

    # --- Console Handler（默认比文件高一级，减少噪音）---
    console_handler = logging.StreamHandler(sys.stdout)
    console_handler.setLevel(getattr(logging, console_level.upper(), logging.WARNING))
    console_handler.setFormatter(console_fmt)
    root_logger.addHandler(console_handler)

    # 减少第三方库的日志噪音
    logging.getLogger("uvicorn.access").setLevel(logging.WARNING)
    logging.getLogger("httpx").setLevel(logging.WARNING)
