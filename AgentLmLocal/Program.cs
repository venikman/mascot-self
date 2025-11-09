using System.ClientModel;
using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

class Program
{
    static async Task Main()
    {
        var baseUrl = Environment.GetEnvironmentVariable("OPENAI_BASE_URL") ?? "http://localhost:1234/v1";
        var apiKey  = Environment.GetEnvironmentVariable("OPENAI_API_KEY")  ?? "lm-studio";
        var model   = Environment.GetEnvironmentVariable("OPENAI_MODEL")     ?? "openai/gpt-oss-20b";

        var client = new OpenAIClient(
            credential: new ApiKeyCredential(apiKey),
            options: new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });

        var weatherTool = AIFunctionFactory.Create(GetWeather);

        try
        {
            // Preferred: /v1/responses
#pragma warning disable OPENAI001
            var agent = client
                .GetOpenAIResponseClient(model)
                .CreateAIAgent(
                    name: "LocalTutor",
                    instructions: "Be concise. Explain to an engineer.",
                    tools: [weatherTool]);
#pragma warning restore OPENAI001
            Console.WriteLine(await agent.RunAsync("One line: what is an agentic framework?"));
        }
        catch
        {
            // Fallback: /v1/chat/completions (older LM Studio builds / certain models)
            var agent = client
                .GetChatClient(model)
                .CreateAIAgent(
                    instructions: "Be concise. Explain to an engineer.",
                    tools: [weatherTool]);
            Console.WriteLine(await agent.RunAsync("One line: what is an agentic framework?"));
        }
    }

    // Sample function tool wired into both CreateAIAgent calls.
    [Description("Get the weather for a given location.")]
    private static string GetWeather([Description("The location to get the weather for.")] string location)
        => $"The weather in {location} is cloudy with a high of 15°C.";
}
