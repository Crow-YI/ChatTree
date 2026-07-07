using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using TreeChat.Models;

namespace TreeChat.Services
{
    /// <summary>
    /// 提供与大模型服务器交互的服务类
    /// </summary>
    public class OpenAIChat
    {
        public static OpenAIChat Instance { get; private set; } = new OpenAIChat();

        public OpenAIChat()
        {
        }

        /// <summary>
        /// 调用AI接口（传入上下文和配置）
        /// </summary>
        /// <param name="context">完整上下文</param>
        /// <param name="chatTree">对话树，包含配置信息</param>
        public async Task<AiCallResult> CallAiApi(List<ChatMessage> context, ChatTree chatTree)
        {
            List<OpenAIMessage> tempList = new List<OpenAIMessage>();
            foreach (ChatMessage message in context)
            {
                OpenAIMessage item = new OpenAIMessage { role = message.Role, content = message.Content };
                tempList.Add(item);
            }

            try
            {
                // 使用传入的配置
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", ApiConfig.ApiKey);
                    httpClient.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json"));

                    // 构造强类型请求体
                    var request = new ChatCompletionRequest
                    {
                        model = ApiConfig.ModelName,
                        temperature = ApiConfig.Temperature,
                        top_p = ApiConfig.TopP,
                        top_k = ApiConfig.TopK,
                        messages = tempList
                    };

                    // 序列化强类型对象
                    var jsonContent = new StringContent(
                        JsonConvert.SerializeObject(request),
                        Encoding.UTF8,
                        "application/json");

                    // 请求与解析返回内容
                    HttpResponseMessage response;
                    try
                    {
                        response = await httpClient.PostAsync(ApiConfig.ApiEndpoint, jsonContent);
                    }
                    catch (HttpRequestException ex)
                    {
                        return AiCallResult.Fail("NetworkError", ex.Message, statusCode: null);
                    }
                    catch (TaskCanceledException)
                    {
                        // 含 HttpClient 超时、DNS 慢等导致的取消
                        return AiCallResult.Fail("Timeout", "请求超时或连接被取消。", statusCode: null);
                    }

                    var responseText = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        var (errorKey, detail) = OpenApiErrorParser.Parse(response.StatusCode, responseText);
                        return AiCallResult.Fail(errorKey, detail, response.StatusCode);
                    }

                    var parseResult = TryParseAssistantContent(responseText);
                    if (!parseResult.Ok)
                        return AiCallResult.Fail("InvalidResponse", parseResult.Error, statusCode: response.StatusCode);

                    if (string.IsNullOrWhiteSpace(parseResult.Content))
                        return AiCallResult.Fail("EmptyModelReply", "模型返回了空内容。", response.StatusCode);

                    return AiCallResult.Success(parseResult.Content!);
                }
            }
            catch (TaskCanceledException)
            {
                return AiCallResult.Fail("Timeout", "请求超时或连接被取消。", statusCode: null);
            }
            catch (HttpRequestException ex)
            {
                return AiCallResult.Fail("NetworkError", ex.Message, statusCode: null);
            }
            catch (Exception ex)
            {
                return AiCallResult.Fail("ClientException", ex.Message, statusCode: null);
            }
        }

        private static (bool Ok, string? Content, string? Error) TryParseAssistantContent(string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
                return (false, null, "响应体为空。");

            try
            {
                var root = JToken.Parse(responseText);
                var content = root.SelectToken("choices[0].message.content")?.ToString();
                if (content == null)
                    return (false, null, "响应 JSON 中缺少 choices[0].message.content 字段。");
                return (true, content.Trim(), null);
            }
            catch (JsonException ex)
            {
                return (false, null, $"响应不是合法 JSON：{ex.Message}");
            }
        }
    }
}
