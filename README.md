# AgentLmLocal - AI Agent Workflow Demo

A C# .NET 9.0 demonstration project showcasing custom AI agent executors in an iterative workflow system using Microsoft.Agents.AI framework with local LM Studio.

## Overview

This project demonstrates how to create and orchestrate multiple AI agents that work together in a feedback loop to iteratively improve content. The sample implements a slogan generation workflow where two specialized agents collaborate:

1. **SloganWriterExecutor**: Generates creative marketing slogans based on a given task
2. **FeedbackExecutor**: Reviews and provides constructive feedback on generated slogans

The agents alternate in a feedback loop until the slogan meets quality standards or maximum attempts are reached.

## Features

- **Custom AI Agent Executors**: Demonstrates how to build specialized agent executors with custom logic
- **Workflow Orchestration**: Shows how to connect multiple agents in an iterative workflow
- **Local LLM Support**: Works with LM Studio and other OpenAI-compatible endpoints
- **Structured Outputs**: Uses JSON schema validation for reliable agent responses
- **Workflow Visualization**: Automatically generates workflow diagrams (Mermaid, DOT, SVG, PNG)
- **Thread-based Conversations**: Maintains conversation context within each agent
- **Configurable Parameters**: Flexible configuration via environment variables

## Project Structure

```
AgentLmLocal/
├── Events/                          # Workflow events
│   ├── FeedbackEvent.cs            # Feedback notification event
│   └── SloganGeneratedEvent.cs     # Slogan generation event
├── Executors/                       # Agent executors
│   ├── FeedbackExecutor.cs         # Reviews and rates slogans
│   └── SloganWriterExecutor.cs     # Generates slogans
├── Models/                          # Data models
│   ├── FeedbackResult.cs           # Feedback response schema
│   └── SloganResult.cs             # Slogan response schema
├── Visualization/                   # Workflow visualization
│   ├── WorkflowBuilderExtensions.cs
│   ├── WorkflowVisualization.cs
│   └── WorkflowVisualizationRecorder.cs
└── Program.cs                       # Main entry point
```

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [LM Studio](https://lmstudio.ai/) (or any OpenAI-compatible API endpoint)
- A language model that supports structured JSON outputs

### Recommended Models

For best results with structured outputs, use models like:
- OpenAI GPT-3.5/4
- Mistral
- Llama-based models with good instruction following

## Setup

### 1. Clone the Repository

```bash
git clone <repository-url>
cd mascot-self
```

### 2. Install Dependencies

```bash
cd AgentLmLocal
dotnet restore
```

### 3. Configure LM Studio

1. Download and install [LM Studio](https://lmstudio.ai/)
2. Load a compatible language model
3. Start the local server (default: `http://localhost:1234`)
4. Note the model ID from LM Studio

### 4. Set Environment Variables (Optional)

Create a `.env` file or set environment variables:

```bash
# LM Studio Configuration
export LMSTUDIO_ENDPOINT="http://localhost:1234/v1"
export LMSTUDIO_API_KEY="lm-studio"
export LMSTUDIO_MODEL="openai/gpt-oss-20b"

# Workflow Configuration
export MINIMUM_RATING="8"      # Minimum rating (1-10) for acceptance
export MAX_ATTEMPTS="3"        # Maximum refinement iterations
```

See `.env.example` for a complete reference.

## Usage

### Running the Application

```bash
cd AgentLmLocal
dotnet run
```

### Expected Output

```
Connecting to LM Studio at: http://localhost:1234/v1
Using model: openai/gpt-oss-20b

Slogan: Power meets affordability - Your electric adventure awaits!
Feedback:
{
  "comments": "Good start, but could be more exciting",
  "rating": 7,
  "actions": "Add more energy and emphasize the fun aspect"
}

Slogan: Electrify your drive - Affordable thrills, zero emissions!
Feedback:
{
  "comments": "Excellent! Captures excitement and key benefits",
  "rating": 9,
  "actions": "None - ready to go!"
}

The following slogan was accepted:

Electrify your drive - Affordable thrills, zero emissions!

Workflow completed successfully!
```

### Generated Artifacts

The application generates workflow visualization files in:
```
AgentLmLocal/bin/Debug/net9.0/WorkflowVisualization/
├── slogan_workflow.mmd    # Mermaid diagram
├── slogan_workflow.dot    # Graphviz DOT file
├── slogan_workflow.svg    # SVG image (requires Graphviz)
└── slogan_workflow.png    # PNG image (requires Graphviz)
```

To generate image files, install [Graphviz](https://graphviz.org/download/).

## Configuration

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `LMSTUDIO_ENDPOINT` | `http://localhost:1234/v1` | LM Studio API endpoint |
| `LMSTUDIO_API_KEY` | `lm-studio` | API key for authentication |
| `LMSTUDIO_MODEL` | `openai/gpt-oss-20b` | Model identifier |
| `MINIMUM_RATING` | `8` | Minimum rating (1-10) for slogan acceptance |
| `MAX_ATTEMPTS` | `3` | Maximum refinement iterations |

### Customizing the Workflow

You can modify the workflow by editing `Program.cs`:

```csharp
// Change the task prompt
await InProcessExecution.StreamAsync(
    workflow,
    input: "Your custom task here"
);

// Adjust feedback criteria
var feedbackProvider = new FeedbackExecutor("FeedbackProvider", chatClient)
{
    MinimumRating = 9,  // Higher quality threshold
    MaxAttempts = 5     // More refinement iterations
};
```

## How It Works

### Workflow Flow

1. **Initial Request**: User provides a task (e.g., "Create a slogan for an electric SUV")
2. **Slogan Generation**: SloganWriterExecutor generates an initial slogan
3. **Feedback Loop**:
   - FeedbackExecutor evaluates the slogan and provides a rating (1-10)
   - If rating ≥ threshold (default: 8), slogan is accepted
   - If rating < threshold and attempts < max, feedback is sent to SloganWriterExecutor
   - SloganWriterExecutor refines the slogan based on feedback
4. **Completion**: Either slogan is accepted or max attempts reached

### Agent Communication

Agents communicate through strongly-typed messages:
- `string` → `SloganWriterExecutor` (initial task)
- `SloganResult` → `FeedbackExecutor` (slogan review)
- `FeedbackResult` → `SloganWriterExecutor` (refinement)

### Structured Outputs

Both agents use JSON schema validation to ensure reliable responses:

```csharp
ChatOptions = new()
{
    ResponseFormat = ChatResponseFormat.ForJsonSchema<SloganResult>()
}
```

## Troubleshooting

### Connection Errors

**Error**: `Network error: Failed to connect to LM Studio endpoint`

**Solutions**:
- Ensure LM Studio is running
- Verify the endpoint URL matches your LM Studio configuration
- Check firewall settings

### Deserialization Errors

**Error**: `Failed to deserialize slogan result`

**Solutions**:
- Use a model with good instruction-following capabilities
- Try a different model in LM Studio
- Check LM Studio logs for model errors

### Missing Visualization Images

**Issue**: SVG/PNG files not generated

**Solution**: Install Graphviz:
```bash
# Ubuntu/Debian
sudo apt-get install graphviz

# macOS
brew install graphviz

# Windows
choco install graphviz
```

## Development

### Building

```bash
dotnet build
```

### Code Formatting

This project uses CSharpier for code formatting:

```bash
dotnet tool restore
dotnet csharpier .
```

### Adding New Agents

1. Create a new executor class in `Executors/`
2. Define input/output models in `Models/`
3. Create custom events in `Events/` if needed
4. Wire up the agent in `Program.cs`

## Dependencies

- `Microsoft.Agents.AI` - AI agent framework
- `Microsoft.Agents.AI.Workflows` - Workflow orchestration
- `Microsoft.Extensions.AI` - AI abstractions
- `Microsoft.Extensions.AI.OpenAI` - OpenAI integration
- `OpenAI` - OpenAI client library

## License

Copyright (c) Microsoft. All rights reserved.

## Contributing

This is a sample/demo project. For production use, consider:
- Adding comprehensive error handling
- Implementing retry logic for API calls
- Adding logging and telemetry
- Implementing unit tests
- Adding configuration validation
- Supporting multiple concurrent workflows

## Resources

- [Microsoft.Agents.AI Documentation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.agents.ai)
- [LM Studio](https://lmstudio.ai/)
- [.NET 9.0](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Graphviz](https://graphviz.org/)
