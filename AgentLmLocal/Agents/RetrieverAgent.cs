using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using WorkflowCustomAgentExecutorsSample.Models;
using ChatResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat;

namespace WorkflowCustomAgentExecutorsSample.Agents;

/// <summary>
/// RetrieverAgent: Binds to external knowledge systems and retrieves relevant information.
///
/// This agent handles retrieval-augmented generation (RAG) by querying external
/// knowledge bases, documents, and data sources. It processes retrieval queries,
/// ranks results by relevance, and provides contextualized knowledge to other agents.
///
/// Based on the agentic workflows pattern described in the research paper.
/// </summary>
public sealed class RetrieverAgent : Executor<RetrievalQuery>
{
    private readonly AIAgent _agent;
    private readonly AgentThread _thread;
    private readonly Dictionary<string, List<KnowledgeItem>> _knowledgeCache = [];

    /// <summary>
    /// Whether to enable caching of retrieval results.
    /// </summary>
    public bool EnableCaching { get; init; } = true;

    /// <summary>
    /// Minimum relevance score (0-1) for results to be included.
    /// </summary>
    public double MinimumRelevanceScore { get; init; } = 0.6;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetrieverAgent"/> class.
    /// </summary>
    /// <param name="id">A unique identifier for the retriever agent.</param>
    /// <param name="chatClient">The chat client to use for the AI agent.</param>
    public RetrieverAgent(string id, IChatClient chatClient) : base(id)
    {
        ChatClientAgentOptions agentOptions = new(
            instructions: """
            You are an expert knowledge retrieval agent that finds and ranks relevant information.

            Your responsibilities:
            1. Analyze retrieval queries to understand information needs
            2. Search knowledge bases and external sources
            3. Rank results by relevance and quality
            4. Extract key information and provide context
            5. Synthesize information from multiple sources
            6. Provide metadata about source reliability

            For each query:
            - Understand the context and intent
            - Identify the most relevant knowledge sources
            - Retrieve and rank results
            - Filter out low-quality or irrelevant results
            - Provide confidence scores
            - Include source attribution

            Prioritize accuracy, relevance, and source quality in your retrievals.
            """)
        {
        };

        this._agent = new ChatClientAgent(chatClient, agentOptions);
        this._thread = this._agent.GetNewThread();
    }

    /// <summary>
    /// Handles retrieval queries and returns relevant knowledge.
    /// </summary>
    public override async ValueTask HandleAsync(
        RetrievalQuery query,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (EnableCaching && _knowledgeCache.TryGetValue(query.Query, out var cachedResults))
        {
            await context.AddEventAsync(
                new RetrievalEvent($"Retrieved {cachedResults.Count} items from cache"),
                cancellationToken);

            var cachedKnowledge = new RetrievedKnowledge
            {
                Query = query.Query,
                Results = cachedResults,
                Confidence = 0.95 // High confidence for cached results
            };

            await context.SendMessageAsync(cachedKnowledge, cancellationToken: cancellationToken);
            return;
        }

        // Perform retrieval
        var knowledge = await PerformRetrievalAsync(query, cancellationToken);

        await context.AddEventAsync(
            new RetrievalEvent($"Retrieved {knowledge.Results.Count} items for query: {query.Query}"),
            cancellationToken);

        // Cache results
        if (EnableCaching)
        {
            _knowledgeCache[query.Query] = knowledge.Results;
        }

        // Send results to next agent
        await context.SendMessageAsync(knowledge, cancellationToken: cancellationToken);

        await context.YieldOutputAsync(
            $"Knowledge Retrieved: {knowledge.Results.Count} items (confidence: {knowledge.Confidence:P0})",
            cancellationToken);
    }

    /// <summary>
    /// Performs the actual retrieval operation.
    /// </summary>
    private async Task<RetrievedKnowledge> PerformRetrievalAsync(
        RetrievalQuery query,
        CancellationToken cancellationToken)
    {
        // In a real implementation, this would:
        // 1. Query vector databases
        // 2. Search document stores
        // 3. Call external APIs
        // 4. Perform semantic search
        // 5. Rank and filter results

        // For demonstration, we'll simulate retrieval with LLM-generated content
        var prompt = $"""
            You are querying a knowledge base with the following parameters:

            Query: {query.Query}
            Context: {query.Context}
            Maximum Results: {query.MaxResults}
            Knowledge Source: {query.KnowledgeSource}

            Please generate {query.MaxResults} simulated knowledge items that would be relevant to this query.
            Each item should include:
            - Content (the actual knowledge/information)
            - Source (where this information came from)
            - Relevance score (0.0 to 1.0)
            - Metadata (additional context)

            Make the content realistic and relevant to the query. Vary the relevance scores
            to simulate a real retrieval system.
            """;

        var response = await _agent.RunAsync(prompt, _thread, cancellationToken: cancellationToken);

        // Parse response into knowledge items
        var knowledge = await GenerateKnowledgeItemsAsync(
            query.Query,
            response.Text,
            query.MaxResults,
            cancellationToken);

        // Filter by minimum relevance score
        knowledge.Results = knowledge.Results
            .Where(item => item.RelevanceScore >= MinimumRelevanceScore)
            .OrderByDescending(item => item.RelevanceScore)
            .Take(query.MaxResults)
            .ToList();

        // Calculate overall confidence based on result quality
        knowledge.Confidence = knowledge.Results.Count > 0
            ? knowledge.Results.Average(r => r.RelevanceScore)
            : 0.0;

        return knowledge;
    }

    /// <summary>
    /// Generates knowledge items from LLM response.
    /// </summary>
    private async Task<RetrievedKnowledge> GenerateKnowledgeItemsAsync(
        string query,
        string llmResponse,
        int maxResults,
        CancellationToken cancellationToken)
    {
        // Create simulated knowledge items
        // In a real system, this would parse actual search results
        var items = new List<KnowledgeItem>();

        // For demonstration, create mock items based on the query
        var random = new Random();
        for (int i = 0; i < Math.Min(maxResults, 5); i++)
        {
            items.Add(new KnowledgeItem
            {
                Content = $"Knowledge item {i + 1} related to: {query}. {llmResponse.Substring(0, Math.Min(200, llmResponse.Length))}...",
                Source = $"KnowledgeBase/{query.GetHashCode():X8}/{i}",
                RelevanceScore = Math.Round(0.6 + (random.NextDouble() * 0.4), 2), // 0.6 to 1.0
                Metadata = new Dictionary<string, string>
                {
                    ["retrieval_timestamp"] = DateTime.UtcNow.ToString("O"),
                    ["query_hash"] = query.GetHashCode().ToString("X8"),
                    ["result_rank"] = (i + 1).ToString()
                }
            });
        }

        await Task.Delay(50, cancellationToken); // Simulate retrieval latency

        return new RetrievedKnowledge
        {
            Query = query,
            Results = items,
            Confidence = 0.85
        };
    }

    /// <summary>
    /// Clears the knowledge cache.
    /// </summary>
    public void ClearCache()
    {
        _knowledgeCache.Clear();
    }

    /// <summary>
    /// Pre-loads knowledge into the cache.
    /// </summary>
    public void PreloadKnowledge(string query, List<KnowledgeItem> items)
    {
        _knowledgeCache[query] = items;
    }
}

/// <summary>
/// Event emitted during retrieval operations.
/// </summary>
public sealed class RetrievalEvent(string message) : WorkflowEvent(message)
{
    public override string ToString() => $"Retrieval: {message}";
}
