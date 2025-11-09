using System.ClientModel;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

class Program
{
    private static readonly JsonSerializerOptions ThreadSerializerOptions = new(JsonSerializerDefaults.Web);

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
            await DemoAgentThreadAsync(agent, "Responses");
        }
        catch
        {
            // Fallback: /v1/chat/completions (older LM Studio builds / certain models)
            var agent = client
                .GetChatClient(model)
                .CreateAIAgent(
                    instructions: "Be concise. Explain to an engineer.",
                    tools: [weatherTool]);
            await DemoAgentThreadAsync(agent, "Chat Completions");
        }
    }

    private static async Task DemoAgentThreadAsync(AIAgent agent, string scenarioLabel)
    {
        AgentThread thread = agent.GetNewThread();
        await RunAndPrintAsync(agent, thread, "Remember this exact phrase: sharp mascot thread sample.");
        await RunAndPrintAsync(agent, thread, "What did I just ask you to remember?");

        JsonElement serializedThread = thread.Serialize(ThreadSerializerOptions);
        Console.WriteLine($"Serialized thread payload: {serializedThread}");

        AgentThread resumedThread = agent.DeserializeThread(serializedThread, ThreadSerializerOptions);
        await RunAndPrintAsync(agent, resumedThread, "Resume the saved conversation and suggest the next action.");
    }

    private static async Task RunAndPrintAsync(AIAgent agent, AgentThread thread, string prompt)
    {
        Console.WriteLine($"\nuser : {prompt}");
        var reply = await agent.RunAsync(prompt, thread);
        Console.WriteLine($"agent: {reply}");
    }

    // Sample function tool wired into both CreateAIAgent calls.
    [Description("Get the weather for a given location.")]
    private static string GetWeather([Description("The location to get the weather for.")] string location)
        => $"The weather in {location} is cloudy with a high of 15°C.";
}
