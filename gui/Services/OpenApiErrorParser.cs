using Newtonsoft.Json.Linq;
using System.Net;

namespace TreeChat.Services
{
    /// <summary>
    /// 开放接口错误解析器：根据 HTTP 状态码与返回体中的 detail 字段提取可用的错误信息。
    /// </summary>
    public static class OpenApiErrorParser
    {
        /// <summary>
        /// 从开放接口错误响应中提取错误类型标识与 detail 文本。
        /// </summary>
        /// <param name="statusCode">HTTP 状态码</param>
        /// <param name="responseText">响应体文本</param>
        /// <returns>错误类型标识与错误 detail</returns>
        public static (string errorKey, string? detail) Parse(HttpStatusCode statusCode, string? responseText)
        {
            string? detail = TryExtractDetail(responseText);

            if (statusCode == HttpStatusCode.Unauthorized)
                return ("401", detail);

            if (statusCode == HttpStatusCode.Forbidden)
                return ("403", detail);

            if ((int)statusCode == 422)
                return ("422", NormalizeUnprocessableEntityDetail(responseText) ?? detail);

            if ((int)statusCode == 429)
                return ("429", detail);

            return ("Other", detail);
        }

        private static string? TryExtractDetail(string? responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
                return null;

            try
            {
                var token = JToken.Parse(responseText);
                var detailToken = token["detail"];
                if (detailToken == null)
                    return null;

                if (detailToken.Type == JTokenType.String)
                    return detailToken.ToString();

                // 文档允许：{"detail":[...]}（422 典型）
                return detailToken.ToString();
            }
            catch
            {
                // 非 JSON 时不解析
                return null;
            }
        }

        private static string? NormalizeUnprocessableEntityDetail(string? responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
                return null;

            try
            {
                var token = JToken.Parse(responseText);
                var detail = token["detail"];
                if (detail is not JArray arr || arr.Count == 0)
                    return null;

                // 取第一条错误信息组织成一句话：loc + msg
                var first = arr[0];
                var msg = first?["msg"]?.ToString();
                var loc = first?["loc"]?.ToString();

                if (!string.IsNullOrWhiteSpace(msg) && !string.IsNullOrWhiteSpace(loc))
                    return $"{msg}（位置：{loc}）";
                return msg ?? loc;
            }
            catch
            {
                return null;
            }
        }
    }
}

