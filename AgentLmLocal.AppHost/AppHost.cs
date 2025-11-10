var builder = DistributedApplication.CreateBuilder(args);

// Minimal agent project with a predictable HTTP endpoint
builder.AddProject("agentlm-local", "../AgentLmLocal/AgentLmLocal.csproj")
    .WithHttpEndpoint(name: "http", port: 5088, isProxied: false)
    .WithEnvironment("ASPNETCORE_URLS", "http://0.0.0.0:5088");

builder.Build().Run();
