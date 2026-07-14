"""所有 API 路由。"""

from __future__ import annotations

import asyncio
import json
import logging
import os
import signal
from collections.abc import AsyncGenerator
from typing import Any

from fastapi import APIRouter, Depends, HTTPException, Request
from starlette.responses import JSONResponse
from sse_starlette.sse import EventSourceResponse

from ..core.config import settings
from ..core.config import ProfileData as CoreProfileData
from ..core.errors import (
    TreeChatError,
    LLMError,
    ConfigError,
    TreeNotFoundError,
    NodeNotFoundError,
)
from ..services.tree_manager import TreeManager
from ..services.llm_service import LLMService
from ..services.file_service import FileService
from ..models.chat_message import BaseMessage, HumanMessage, SystemMessage, message_to_role
from .dependencies import get_tree_manager, get_llm_service, get_file_service
from .schemas import (
    CreateTreeRequest,
    RenameTreeRequest,
    TreeListResponse,
    TreeSummary,
    TreeDetailResponse,
    TreeNodeData,
    ChatRequest,
    RenameNodeRequest,
    ConfigData,
    ProfileData,
    ProfileListResponse,
    ActivateProfileResponse,
    ErrorDetail,
    SuccessResponse,
    HealthResponse,
    SerializeResponse,
    DeserializeRequest,
    _node_to_data,
)

router = APIRouter(prefix="/api/v1")
logger = logging.getLogger(__name__)


# === Exception Handlers (registered in main.py) ===

async def treechat_error_handler(request: Request, exc: TreeChatError) -> JSONResponse:
    """统一错误响应格式。"""
    status_code: int = 500
    if isinstance(exc, TreeNotFoundError) or isinstance(exc, NodeNotFoundError):
        status_code = 404
    logger.error(
        "TreeChatError: %s | path=%s | status=%d",
        exc, request.url.path, status_code, exc_info=True,
    )
    content = json.dumps({
        "success": False,
        "error": {
            "key": type(exc).__name__,
            "message": str(exc),
            "detail": None,
            "status_code": status_code,
        },
    })
    return JSONResponse(content=content, status_code=status_code)


async def llm_error_handler(request: Request, exc: LLMError) -> JSONResponse:
    """LLM 错误响应格式。"""
    logger.error(
        "LLMError: [%s] %s | path=%s | detail=%s",
        exc.key, exc.message, request.url.path, exc.detail,
    )
    content = json.dumps({
        "success": False,
        "error": {
            "key": exc.key,
            "message": exc.message,
            "detail": exc.detail,
            "status_code": exc.status_code,
        },
    })
    return JSONResponse(content=content, status_code=exc.status_code or 500)


async def generic_error_handler(request: Request, exc: Exception) -> JSONResponse:
    """通用异常响应格式。"""
    logger.exception("Unhandled exception: path=%s", request.url.path)
    content = json.dumps({
        "success": False,
        "error": {
            "key": "InternalError",
            "message": "服务器内部错误。",
            "detail": str(exc),
            "status_code": 500,
        },
    })
    return JSONResponse(content=content, status_code=500)


# === Health ===

@router.get("/health", response_model=HealthResponse)
async def health() -> HealthResponse:
    """健康检查端点。"""
    return HealthResponse()


# === Tree CRUD ===

@router.post("/trees", response_model=TreeDetailResponse)
async def create_tree(
    req: CreateTreeRequest,
    tm: TreeManager = Depends(get_tree_manager),
) -> TreeDetailResponse:
    """创建新的对话树。"""
    tree = tm.create_tree(title=req.title, system_prompt=req.system_prompt)
    return TreeDetailResponse(
        tree_id=tree.tree_id,
        title=tree.title,
        created_at=tree.created_at,
        system_prompt=tree.system_prompt,
        root_node=_node_to_data(tree.root_node),
    )


@router.get("/trees", response_model=TreeListResponse)
async def list_trees(
    tm: TreeManager = Depends(get_tree_manager),
) -> TreeListResponse:
    """列出所有对话树。"""
    trees = tm.list_trees()
    return TreeListResponse(
        trees=[
            TreeSummary(
                tree_id=t.tree_id,
                title=t.title,
                created_at=t.created_at,
                node_count=t.node_count,
            )
            for t in trees
        ]
    )


@router.get("/trees/{tree_id}", response_model=TreeDetailResponse)
async def get_tree(
    tree_id: str,
    tm: TreeManager = Depends(get_tree_manager),
) -> TreeDetailResponse:
    """获取单个对话树详情。"""
    tree = tm.get_tree(tree_id)
    return TreeDetailResponse(
        tree_id=tree.tree_id,
        title=tree.title,
        created_at=tree.created_at,
        system_prompt=tree.system_prompt,
        root_node=_node_to_data(tree.root_node),
    )


@router.delete("/trees/{tree_id}", response_model=SuccessResponse)
async def delete_tree(
    tree_id: str,
    tm: TreeManager = Depends(get_tree_manager),
) -> SuccessResponse:
    """删除对话树。"""
    tm.delete_tree(tree_id)
    return SuccessResponse()


@router.put("/trees/{tree_id}", response_model=SuccessResponse)
async def rename_tree(
    tree_id: str,
    req: RenameTreeRequest,
    tm: TreeManager = Depends(get_tree_manager),
) -> SuccessResponse:
    """重命名对话树。"""
    tm.rename_tree(tree_id, req.title)
    return SuccessResponse()


# === Chat (流式核心接口) ===

@router.post("/trees/{tree_id}/chat")
async def chat(
    tree_id: str,
    req: ChatRequest,
    tm: TreeManager = Depends(get_tree_manager),
    llm: LLMService = Depends(get_llm_service),
) -> EventSourceResponse:
    """SSE 流式聊天端点。"""
    logger.info("Chat request: tree=%s msg_len=%d attachments=%d files=%s",
                tree_id, len(req.message),
                len(req.attachments) if req.attachments else 0,
                [a.file_name for a in req.attachments] if req.attachments else [])

    # 1. 在指定父节点下创建子节点，传入附件文件名
    try:
        file_names = [att.file_name for att in req.attachments] if req.attachments else None
        child_node, parent_node = tm.add_child_node(
            tree_id, req.parent_node_id, req.message,
            attachment_file_names=file_names,
        )
    except (TreeNotFoundError, NodeNotFoundError) as e:
        raise HTTPException(status_code=404, detail=str(e))

    # 2. 构建完整上下文（直接传 BaseMessage 给 LangChain）
    tree = tm.get_tree(tree_id)
    context_messages: list[BaseMessage] = child_node.get_full_context()
    if tree.system_prompt:
        context_messages.insert(0, SystemMessage(content=tree.system_prompt))

    # 2b. 注入附件内容（作为额外的用户消息放在当前消息之前）
    if req.attachments:
        for att in req.attachments:
            file_msg = (
                f"用户上传了文件「{att.file_name}」，以下是文件内容：\n\n"
                f"```\n{att.content}\n```"
            )
            context_messages.append(HumanMessage(content=file_msg))

    # 3. SSE 事件生成器
    async def event_generator() -> AsyncGenerator[dict[str, str], None]:
        logger.info(
            "SSE stream start: tree=%s parent_node=%d",
            tree_id, req.parent_node_id,
        )
        try:
            # 先发送 created 事件通知 WPF 新节点已创建
            created_data = json.dumps({
                "node_id": child_node.node_id,
                "user_message": {
                    "role": message_to_role(child_node.user_message),
                    "content": child_node.user_message.content,
                },
                "attachment_file_names": child_node.attachment_file_names,
            }, ensure_ascii=False)
            yield {"event": "created", "data": created_data}

            # 流式调用 LLM（LangChain astream）
            full_content: str = ""
            async for token in llm.astream(
                messages=context_messages,
                profile_name=req.profile_name,
                model=req.model,
                temperature=req.temperature,
                top_p=req.top_p,
                max_tokens=req.max_tokens,
            ):
                full_content += token
                delta_data = json.dumps({"content": token}, ensure_ascii=False)
                yield {"event": "delta", "data": delta_data}

            # 保存 AI 回复到节点
            tm.set_ai_reply(tree_id, child_node.node_id, full_content)

            # 发送 done 事件
            done_data = json.dumps({
                "node_id": child_node.node_id,
                "reply_message": {
                    "role": "assistant",
                    "content": full_content,
                },
            }, ensure_ascii=False)
            yield {"event": "done", "data": done_data}

        except LLMError as e:
            # AI 调用失败 → 清理节点
            try:
                tm.delete_node(tree_id, child_node.node_id)
            except Exception:
                pass
            error_data = json.dumps({
                "key": e.key,
                "message": e.message,
                "detail": e.detail,
                "status_code": e.status_code,
            }, ensure_ascii=False)
            yield {"event": "error", "data": error_data}

        except asyncio.CancelledError:
            # 客户端断开连接，不清理节点保留已收到的内容
            logger.warning("SSE stream cancelled (client disconnected): tree=%s", tree_id)
            raise

        except Exception as e:
            # 未知错误 → 清理节点
            logger.exception("SSE stream error: tree=%s", tree_id)
            try:
                tm.delete_node(tree_id, child_node.node_id)
            except Exception:
                pass
            error_data = json.dumps({
                "key": "InternalError",
                "message": "服务器内部错误。",
                "detail": str(e),
                "status_code": 500,
            }, ensure_ascii=False)
            yield {"event": "error", "data": error_data}

        finally:
            logger.info("SSE stream end: tree=%s", tree_id)

    return EventSourceResponse(event_generator())


# === Node Operations ===

@router.put("/trees/{tree_id}/nodes/{node_id}", response_model=SuccessResponse)
async def rename_node(
    tree_id: str,
    node_id: int,
    req: RenameNodeRequest,
    tm: TreeManager = Depends(get_tree_manager),
) -> SuccessResponse:
    """重命名树节点。"""
    tm.rename_node(tree_id, node_id, req.name)
    return SuccessResponse()


@router.delete("/trees/{tree_id}/nodes/{node_id}", response_model=SuccessResponse)
async def delete_node(
    tree_id: str,
    node_id: int,
    tm: TreeManager = Depends(get_tree_manager),
) -> SuccessResponse:
    """删除树节点及其子树。"""
    tm.delete_node(tree_id, node_id)
    return SuccessResponse()


# === Config ===

@router.get("/config", response_model=ConfigData)
async def get_config() -> ConfigData:
    """获取当前运行时 LLM 配置（激活 profile 的参数）。"""
    profile = settings.get_active_profile()
    if profile:
        return ConfigData(
            model=profile.model,
            temperature=profile.temperature,
            top_p=profile.top_p,
            max_tokens=profile.max_tokens,
        )
    return ConfigData(
        model=settings.model,
        temperature=settings.temperature,
        top_p=settings.top_p,
        max_tokens=settings.max_tokens,
    )


@router.put("/config", response_model=SuccessResponse)
async def update_config(req: ConfigData) -> SuccessResponse:
    """更新运行时 LLM 配置（更新激活 profile 的参数并持久化）。"""
    # 更新激活 profile 的运行时参数
    profile_name = settings.active_profile
    settings.update_profile(profile_name, {
        "model": req.model,
        "temperature": req.temperature,
        "top_p": req.top_p,
        "max_tokens": req.max_tokens,
    })
    return SuccessResponse()


# === Profile Management ===

@router.get("/profiles", response_model=ProfileListResponse)
async def list_profiles() -> ProfileListResponse:
    """获取所有 profile 列表及激活的 profile 名称。"""
    return ProfileListResponse(
        profiles=[ProfileData(**p.model_dump()) for p in settings.profiles],
        active_profile=settings.active_profile,
    )


@router.get("/profiles/{name}", response_model=ProfileData)
async def get_profile(name: str) -> ProfileData:
    """获取单个 profile 详情。"""
    profile = settings.get_profile(name)
    if profile is None:
        raise HTTPException(status_code=404, detail=f"Profile '{name}' 不存在")
    return ProfileData(**profile.model_dump())


@router.post("/profiles", response_model=ProfileData, status_code=201)
async def create_profile(req: ProfileData) -> ProfileData:
    """创建新 profile。"""
    try:
        core_profile = CoreProfileData(**req.model_dump())
        settings.add_profile(core_profile)
        return req
    except ConfigError as e:
        raise HTTPException(status_code=409, detail=str(e))


@router.put("/profiles/{name}", response_model=ProfileData)
async def update_profile(name: str, req: ProfileData) -> ProfileData:
    """更新指定 profile。"""
    # 允许通过 URL 中的 name 重命名 profile
    updates = req.model_dump(exclude={"name"}, exclude_none=True)
    try:
        updated = settings.update_profile(name, updates)
        return ProfileData(**updated.model_dump())
    except ConfigError as e:
        raise HTTPException(status_code=404, detail=str(e))


@router.delete("/profiles/{name}", response_model=SuccessResponse)
async def delete_profile(name: str) -> SuccessResponse:
    """删除指定 profile。禁止删除激活中的 profile。"""
    try:
        settings.delete_profile(name)
        return SuccessResponse()
    except ConfigError as e:
        if "激活" in str(e):
            raise HTTPException(status_code=400, detail=str(e))
        raise HTTPException(status_code=404, detail=str(e))


@router.put("/profiles/{name}/activate", response_model=ActivateProfileResponse)
async def activate_profile(name: str) -> ActivateProfileResponse:
    """激活指定 profile。"""
    try:
        profile = settings.set_active_profile(name)
        return ActivateProfileResponse(
            active_profile=profile.name,
            config=ConfigData(
                model=profile.model,
                temperature=profile.temperature,
                top_p=profile.top_p,
                max_tokens=profile.max_tokens,
            ),
        )
    except ConfigError as e:
        raise HTTPException(status_code=404, detail=str(e))


# === File Serialization ===

@router.get("/trees/{tree_id}/serialize", response_model=SerializeResponse)
async def serialize_tree(
    tree_id: str,
    tm: TreeManager = Depends(get_tree_manager),
    fs: FileService = Depends(get_file_service),
) -> SerializeResponse:
    """将对话树序列化为 JSON 字符串。"""
    tree = tm.get_tree(tree_id)
    json_content: str = fs.serialize(tree)
    return SerializeResponse(json_content=json_content)


@router.post("/trees/deserialize", response_model=TreeDetailResponse)
async def deserialize_tree(
    req: DeserializeRequest,
    tm: TreeManager = Depends(get_tree_manager),
    fs: FileService = Depends(get_file_service),
) -> TreeDetailResponse:
    """从 JSON 字符串反序列化并导入对话树。"""
    tree = fs.deserialize(req.json_content, title=req.title)
    tm.add_tree(tree)
    return TreeDetailResponse(
        tree_id=tree.tree_id,
        title=tree.title,
        created_at=tree.created_at,
        system_prompt=tree.system_prompt,
        root_node=_node_to_data(tree.root_node),
    )


# === Shutdown ===

@router.post("/shutdown", response_model=SuccessResponse)
async def shutdown() -> SuccessResponse:
    """优雅关闭服务器。WPF 退出时调用。"""
    from ..services.llm_service import llm_service as llm

    # 关闭 LLM 客户端连接
    await llm.close()

    # 延迟关闭 uvicorn（给响应返回的时间）
    async def _shutdown() -> None:
        await asyncio.sleep(0.5)
        os.kill(os.getpid(), signal.SIGTERM)

    asyncio.create_task(_shutdown())
    return SuccessResponse()
