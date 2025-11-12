# Configuration Guide

This document explains how to configure the application to use different LLM providers.

## Supported Providers

1. **LM Studio** - Local/self-hosted models or OpenAI-compatible endpoints
2. **Azure OpenAI** - Azure OpenAI Service or proxy endpoints

## Quick Start

### Option 1: LM Studio (Local Development)

```bash
# Copy LM Studio example
cp .env.lmstudio.example .env

# Edit with your settings (if different from defaults)
nano .env

# Run the application
dotnet run
```

### Option 2: Azure OpenAI (Cloud)

```bash
# Copy Azure OpenAI example
cp .env.azureopenai.example .env

# Edit with your Azure credentials
nano .env

# Run the application
dotnet run
```

## Switching Between Providers

To switch providers, you only need to change **one variable**: `LLM_PROVIDER`

### Switch to LM Studio

```bash
# In your .env file
LLM_PROVIDER=LmStudio

# Required variables:
LMSTUDIO_ENDPOINT=http://localhost:1234/v1
LMSTUDIO_API_KEY=lm-studio
LMSTUDIO_MODEL=openai/gpt-oss-20b
```

### Switch to Azure OpenAI

```bash
# In your .env file
LLM_PROVIDER=AzureOpenAI

# Required variables:
AZURE_OPENAI_ENDPOINT=https://your-resource-name.openai.azure.com/
AZURE_OPENAI_API_KEY=your-api-key-here
AZURE_OPENAI_DEPLOYMENT=gpt-4o
AZURE_OPENAI_API_VERSION=2024-05-01-preview  # Optional
```

## Complete Configuration Reference

### LM Studio Configuration

| Variable | Default | Description |
|----------|---------|-------------|
| `LMSTUDIO_ENDPOINT` | `http://localhost:1234/v1` | API endpoint URL |
| `LMSTUDIO_API_KEY` | `lm-studio` | Authentication key (usually default) |
| `LMSTUDIO_MODEL` | `openai/gpt-oss-20b` | Model identifier |

**Popular LM Studio Models:**
- `openai/gpt-oss-20b` - Good for general tasks
- `mistralai/mistral-7b-instruct` - Fast and efficient
- `meta-llama/llama-2-13b-chat` - Conversational
- `qwen/qwen-2.5-7b-instruct` - Strong reasoning

### Azure OpenAI Configuration

| Variable | Required | Description |
|----------|----------|-------------|
| `AZURE_OPENAI_ENDPOINT` | ✅ Yes | Azure resource URL |
| `AZURE_OPENAI_API_KEY` | ✅ Yes | API authentication key |
| `AZURE_OPENAI_DEPLOYMENT` | ✅ Yes | Deployment/model name |
| `AZURE_OPENAI_API_VERSION` | ⚠️ Optional | API version (defaults to latest) |

**Common Azure Deployments:**
- `gpt-4o` - Latest GPT-4 Omni model
- `gpt-4o-mini` - Smaller, faster variant
- `gpt-4-turbo` - Turbo-optimized GPT-4
- `gpt-35-turbo` - GPT-3.5 for cost-effective use

**API Version Options:**
- Enum name: `V2024_10_21`
- Version string: `2024-10-21-preview`
- Omit for automatic latest version

### Workflow Configuration (Both Providers)

| Variable | Default | Description |
|----------|---------|-------------|
| `MINIMUM_RATING` | `7` | Quality threshold (1-10) |
| `MAX_ATTEMPTS` | `3` | Maximum retry attempts |

## Examples

### Example 1: Local Development with LM Studio

**File: `.env`**
```bash
LLM_PROVIDER=LmStudio
LMSTUDIO_ENDPOINT=http://localhost:1234/v1
LMSTUDIO_API_KEY=lm-studio
LMSTUDIO_MODEL=mistralai/mistral-7b-instruct
MINIMUM_RATING=7
MAX_ATTEMPTS=3
```

### Example 2: Production with Azure OpenAI

**File: `.env`**
```bash
LLM_PROVIDER=AzureOpenAI
AZURE_OPENAI_ENDPOINT=https://my-company.openai.azure.com/
AZURE_OPENAI_API_KEY=sk-abc123...xyz789
AZURE_OPENAI_DEPLOYMENT=gpt-4o
AZURE_OPENAI_API_VERSION=2024-05-01-preview
MINIMUM_RATING=8
MAX_ATTEMPTS=5
```

### Example 3: Using an API Proxy

**File: `.env`**
```bash
LLM_PROVIDER=AzureOpenAI
AZURE_OPENAI_ENDPOINT=https://api-gateway.mycompany.com/
AZURE_OPENAI_API_KEY=proxy-key-123
AZURE_OPENAI_DEPLOYMENT=production-gpt4
MINIMUM_RATING=7
MAX_ATTEMPTS=3
```

> **Note:** The proxy must forward requests using Azure OpenAI REST format.

### Example 4: Remote LM Studio Instance

**File: `.env`**
```bash
LLM_PROVIDER=LmStudio
LMSTUDIO_ENDPOINT=http://192.168.1.100:1234/v1
LMSTUDIO_API_KEY=my-custom-key
LMSTUDIO_MODEL=openai/gpt-oss-20b
MINIMUM_RATING=7
MAX_ATTEMPTS=3
```

## Verifying Your Configuration

After configuring, verify the setup:

```bash
# Run the application
dotnet run

# Check the logs for successful connection
# You should see:
# - "Starting AgentLmLocal"
# - Chat client initialization messages
# - No authentication errors
```

**Successful LM Studio connection:**
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: AgentLmLocal[0]
      Using LM Studio endpoint: http://localhost:1234/v1
```

**Successful Azure OpenAI connection:**
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: AgentLmLocal[0]
      Using Azure OpenAI deployment: gpt-4o
```

## Troubleshooting

### "Provider selected but credentials missing"

**Error:**
```
InvalidOperationException: Azure OpenAI provider selected, but credentials are not configured.
```

**Solution:**
Ensure all required Azure OpenAI variables are set:
```bash
AZURE_OPENAI_ENDPOINT=https://...
AZURE_OPENAI_API_KEY=...
AZURE_OPENAI_DEPLOYMENT=...
```

### Connection refused (LM Studio)

**Error:**
```
HttpRequestException: Connection refused (localhost:1234)
```

**Solution:**
1. Start LM Studio
2. Load a model
3. Enable Local Server (must be running on port 1234)
4. Verify: `curl http://localhost:1234/v1/models`

### Authentication failed (Azure OpenAI)

**Error:**
```
401 Unauthorized
```

**Solution:**
1. Verify your API key is correct
2. Check the endpoint URL format (should end with `/`)
3. Ensure your Azure resource has the model deployed
4. Check API key permissions in Azure Portal

### Wrong model/deployment

**Error:**
```
404 Not Found: The API deployment for this resource does not exist.
```

**Solution:**
1. Check your deployment name matches exactly (case-sensitive)
2. Verify the model is deployed in Azure Portal
3. For LM Studio, ensure the model is loaded

## Environment-Specific Configuration

### Development

```bash
cp .env.lmstudio.example .env.development
# Use LM Studio for fast local iteration
```

### Staging

```bash
cp .env.azureopenai.example .env.staging
# Use Azure OpenAI with less powerful model
LLM_PROVIDER=AzureOpenAI
AZURE_OPENAI_DEPLOYMENT=gpt-4o-mini
```

### Production

```bash
cp .env.azureopenai.example .env.production
# Use Azure OpenAI with production model
LLM_PROVIDER=AzureOpenAI
AZURE_OPENAI_DEPLOYMENT=gpt-4o
MINIMUM_RATING=8
MAX_ATTEMPTS=5
```

## Security Best Practices

1. **Never commit `.env` files** - Already in `.gitignore`
2. **Use environment-specific keys** - Rotate between environments
3. **Restrict API key permissions** - Use least-privilege principle
4. **Use secrets management** - Azure Key Vault, AWS Secrets Manager, etc.
5. **Audit API usage** - Monitor costs and usage patterns

## Next Steps

- [README.md](README.md) - Main project documentation
- [Frontend Configuration](AgentLmLocal/ClientApp/TELEMETRY_CONFIG.md) - Frontend telemetry options
- [Observability Guide](docs/SERILOG-OTEL-HYBRID.md) - Logging and tracing
