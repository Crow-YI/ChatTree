"""TreeChat Python 后端入口。

支持 CLI 参数:
    --log-level NORMAL | DEBUG    控制日志详细程度（默认 NORMAL）
"""

from __future__ import annotations

import argparse
import logging
import sys
from pathlib import Path

# 确保 src 目录在 sys.path 中
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from src.core.logger import setup_logging

# ============================================================
# CLI 参数解析
# ============================================================
parser = argparse.ArgumentParser(description="TreeChat 后端服务")
parser.add_argument(
    "--log-level",
    default="NORMAL",
    choices=["NORMAL", "DEBUG"],
    help="日志详细程度 (默认 NORMAL)",
)
cli_args, _ = parser.parse_known_args()

# 根据模式映射 Python logging 级别
if cli_args.log_level == "DEBUG":
    _file_level = "DEBUG"
    _console_level = "INFO"
else:
    _file_level = "INFO"
    _console_level = "WARNING"

# 必须在 app 创建前初始化日志
setup_logging(file_level=_file_level, console_level=_console_level)

logger = logging.getLogger(__name__)

# ============================================================
# FastAPI 应用
# ============================================================
from src.core.errors import TreeChatError, LLMError
from src.api.routes import router, treechat_error_handler, llm_error_handler, generic_error_handler

app = FastAPI(
    title="TreeChat Backend",
    version="0.1.0",
    description="Python backend for TreeChat — AI chat with tree-structured conversations",
)

# CORS — 允许 WPF 本机访问
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# 异常处理器
app.add_exception_handler(TreeChatError, treechat_error_handler)
app.add_exception_handler(LLMError, llm_error_handler)
app.add_exception_handler(Exception, generic_error_handler)

# 注册路由
app.include_router(router)


@app.on_event("startup")
async def on_startup() -> None:
    logger.info(
        "TreeChat backend started — host=%s port=%s mode=%s",
        "127.0.0.1",
        8800,
        cli_args.log_level,
    )


@app.on_event("shutdown")
async def on_shutdown() -> None:
    logger.info("TreeChat backend shutting down")


def main() -> None:
    """uvicorn 入口。"""
    import uvicorn
    from src.core.config import settings

    logger.info("Starting uvicorn — %s:%s", settings.host, settings.port)
    uvicorn.run(
        "src.main:app",
        host=settings.host,
        port=settings.port,
        reload=False,
        log_level="info",
    )


if __name__ == "__main__":
    main()
