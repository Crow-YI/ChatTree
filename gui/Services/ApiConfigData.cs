namespace TreeChat.Services
{
    public class ApiConfigData
    {
        public string ApiKey { get; set; } = "";
        public string PythonBackendUrl { get; set; } = "http://127.0.0.1:8800";
        public string PythonProjectDir { get; set; } = "";
        public string ApiEndpoint { get; set; } = "https://api.deepseek.com/chat/completions";
        public string ModelName { get; set; } = "deepseek-v4";
        public double Temperature { get; set; } = 0.7;
        public double TopP { get; set; } = 0.8;
        public int TopK { get; set; } = 20;
        public int MaxTokens { get; set; } = 800;
    }
}