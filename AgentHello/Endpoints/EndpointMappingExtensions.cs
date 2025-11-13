using AgentHello.AI;

namespace AgentHello.Endpoints;

public static class EndpointMappingExtensions
{
    public static void MapAgentHelloEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Json(new { status = "ok" }));

        app.MapPost("/chat", async (IAiRequestOrchestrator orchestrator, ChatRequest request, HttpContext http, CancellationToken token)
            => await orchestrator.HandleAsync(request, http, token));
    }
}
