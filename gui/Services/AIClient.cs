using System.ClientModel;
using OpenAI;
using OpenAI.Chat;

namespace TreeChat.Services
{
    public class AIClient
    {
        public static AIClient Instance;
        static AIClient()
        {
            if (Instance != null)
                return;
            Instance = new AIClient();
        }

        private ChatClient? _chatClient;

        public AIClient()
        {
            SetChatClient();
        }

        public void SetChatClient()
        {
            var client = new OpenAIClient(
                new ApiKeyCredential(ApiConfig.ApiKey),
                new OpenAIClientOptions
                {
                    Endpoint = new Uri(ApiConfig.ApiEndpoint)
                });
            _chatClient = client.GetChatClient(ApiConfig.ModelName);
        }


    }
}
