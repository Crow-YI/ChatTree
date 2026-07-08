"""FastAPI 依赖注入。"""

from typing import Annotated

from fastapi import Depends

from ..services.tree_manager import tree_manager, TreeManager
from ..services.llm_service import llm_service, LLMService
from ..services.file_service import file_service, FileService


def get_tree_manager() -> TreeManager:
    """返回全局 TreeManager 单例。"""
    return tree_manager


def get_llm_service() -> LLMService:
    """返回全局 LLMService 单例。"""
    return llm_service


def get_file_service() -> FileService:
    """返回全局 FileService 单例。"""
    return file_service


# 类型别名，方便路由函数签名使用
TreeManagerDep = Annotated[TreeManager, Depends(get_tree_manager)]
LLMServiceDep = Annotated[LLMService, Depends(get_llm_service)]
FileServiceDep = Annotated[FileService, Depends(get_file_service)]
