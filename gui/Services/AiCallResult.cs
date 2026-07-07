using System.Net;

namespace TreeChat.Services
{
    /// <summary>
    /// AI 接口调用结果。用于区分成功回复与失败原因，避免将错误信息写入对话上下文。
    /// </summary>
    public sealed class AiCallResult
    {
        /// <summary>
        /// 调用是否成功。
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// 成功时的模型回复内容；失败时为 null。
        /// </summary>
        public string? Content { get; }

        /// <summary>
        /// 失败时用于映射中文提示的错误类型标识（可作为 key 使用）。
        /// </summary>
        public string? ErrorKey { get; }

        /// <summary>
        /// 失败时服务端返回的 detail / 解析信息（用于展示或调试）。
        /// </summary>
        public string? ErrorDetail { get; }

        /// <summary>
        /// HTTP 状态码（可空）。
        /// </summary>
        public HttpStatusCode? StatusCode { get; }

        private AiCallResult(bool isSuccess, string? content, string? errorKey, string? errorDetail, HttpStatusCode? statusCode)
        {
            IsSuccess = isSuccess;
            Content = content;
            ErrorKey = errorKey;
            ErrorDetail = errorDetail;
            StatusCode = statusCode;
        }

        /// <summary>
        /// 构造成功结果。
        /// </summary>
        /// <param name="content">模型回复内容</param>
        public static AiCallResult Success(string content) =>
            new AiCallResult(isSuccess: true, content: content, errorKey: null, errorDetail: null, statusCode: null);

        /// <summary>
        /// 构造失败结果。
        /// </summary>
        /// <param name="errorKey">错误类型标识（用于映射中文提示）</param>
        /// <param name="errorDetail">错误细节（通常来自服务端 detail 字段）</param>
        /// <param name="statusCode">HTTP 状态码</param>
        public static AiCallResult Fail(string errorKey, string? errorDetail, HttpStatusCode? statusCode) =>
            new AiCallResult(isSuccess: false, content: null, errorKey: errorKey, errorDetail: errorDetail, statusCode: statusCode);
    }
}

