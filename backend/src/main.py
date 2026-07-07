"""TreeChat Python 后端入口。"""

import sys
from pathlib import Path

# 确保 src 目录在 sys.path 中
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

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


def main() -> None:
    """uvicorn 入口。"""
    import uvicorn
    from src.core.config import settings

    uvicorn.run(
        "src.main:app",
        host=settings.host,
        port=settings.port,
        reload=False,
        log_level="info",
    )


if __name__ == "__main__":
    main()
