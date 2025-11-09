// Copyright (c) Microsoft. All rights reserved.

namespace AgentLmLocal;

/// <summary>
/// Entry point for the agentic workflow system.
/// Runs a multi-agent workflow demonstrating coordinated planning, execution,
/// verification, recovery, and knowledge retrieval.
/// </summary>
/// <remarks>
/// Pre-requisites:
/// - LM Studio (or compatible OpenAI API endpoint) running locally or remotely.
/// - A language model that supports structured JSON outputs.
///
/// Optional environment variables:
/// - LMSTUDIO_ENDPOINT: The API endpoint (default: http://localhost:1234/v1)
/// - LMSTUDIO_API_KEY: API key for authentication (default: lm-studio)
/// - LMSTUDIO_MODEL: The model ID to use (default: openai/gpt-oss-20b)
/// </remarks>
public static class Program
{
    private static async Task Main(string[] args)
    {
        await WorkflowCustomAgentExecutorsSample.AgenticWorkflowExample.RunExample();
    }
}
