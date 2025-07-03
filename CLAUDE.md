# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is the **Aevatar Workshop** - a demonstration project showcasing the **Psi Omni Agent** system built on the Aevatar framework. The system demonstrates multi-agent orchestration using Orleans for distributed computing and Semantic Kernel for AI capabilities.

### Key Components

- **PsiOmniGAgent**: The core multi-agent system that can operate in ORCHESTRATOR or SPECIALIZED modes
- **Aevatar Framework**: Provides the underlying GAgent infrastructure and event sourcing
- **Orleans**: Microsoft's distributed computing framework for agent hosting
- **Semantic Kernel**: AI orchestration and chat completion capabilities

## Architecture

The system uses a **hierarchical multi-agent architecture**:

1. **Root Agent** (depth 0): Always operates in ORCHESTRATOR mode, breaking down tasks into sub-tasks
2. **Child Agents**: Can be either ORCHESTRATOR or SPECIALIZED based on task complexity
3. **Agent Modes**:
   - **ORCHESTRATOR**: Breaks down tasks and delegates to child agents
   - **SPECIALIZED**: Performs specific tasks using designated tools

### Project Structure

```
src/
├── Aevatar.Workshop.Host/     # Orleans host service (silo)
├── Aevatar.Workshop.Client/   # Client connection utilities
├── Psi/                       # CLI tool for interacting with agents
├── PsiGAgent.Common/          # Shared models and interfaces
├── PsiGAgent.Omni/           # Core PsiOmniGAgent implementation
├── PsiGAgent.Plugins/        # Tool plugins (math, etc.)
└── PsiGAgent.Common.Tests/   # Unit tests
```

## Common Commands

### Build the Project
```bash
dotnet build
```

### Run Tests
```bash
dotnet test
```

### Demo Workflow

1. **Start the Host Service**:
   ```bash
   cd src/Aevatar.Workshop.Host/bin/Debug/net9.0
   ./Aevatar.Workshop.Host
   ```

2. **Create an Agent** (in new terminal):
   ```bash
   cd src/Psi/bin/Debug/net9.0
   ./Psi create "Your task description"
   ```

3. **Continue Conversation**:
   ```bash
   ./Psi continue "Follow-up question"
   ```

4. **Get Agent State**:
   ```bash
   ./Psi state
   ```

5. **Visualize Agent Network**:
   - Copy JSON output from `state` command
   - Open `tools/visualize-v2.html` in browser
   - Paste JSON and click "Render Graph"

## Environment Variables

Required for the system to function:
```bash
AZURE_OPENAI_DEPLOYMENT_NAME="your-deployment-name"
AZURE_OPENAI_API_KEY="your-azure-openai-api-key"
AZURE_OPENAI_API_VERSION="2025-01-01-preview"
AZURE_OPENAI_ENDPOINT="https://your-resource.cognitiveservices.azure.com/"
```

Optional for web search functionality:
```bash
GOOGLE_API_KEY="your-google-api-key"
GOOGLE_SEARCH_ENGINE_ID="your-custom-search-engine-id"
BING_API_KEY="your-bing-api-key"  # Optional fallback engine
```

## Key Technical Details

### Agent State Management
- Uses event sourcing via `PsiOmniGAgentState` and `PsiOmniGAgentStateLogEvent`
- Maintains conversation history and agent hierarchy
- Supports serialization for state persistence and visualization

### Tool System
- Agents can use various tools (web search, math, etc.)
- Tool selection determined during agent analysis phase
- Plugin system via `PsiGAgent.Plugins`
- **Web Search**: Complete OpenManus WebSearch implementation with `_try_all_engines`
  - **Multi-Engine Support**: Google (primary), DuckDuckGo, Bing (fallbacks)
  - **Automatic Failover**: Tries engines in order with retry logic
  - **Exponential Backoff**: 3 retries per engine with 1-10 second delays
  - `WebSearch.Execute`: Main search function matching OpenManus exactly
  - `WebSearch.QuickSearch`: Simplified search with text summary
  - `WebSearch.FetchContent`: Download content from specific URLs
  - `WebSearch.FetchMultipleContent`: Concurrent content fetching
- **Content Processing**: Exact match to OpenManus content fetching
  - Removes script, style, header, footer, nav elements (same as OpenManus)
  - BeautifulSoup-equivalent text extraction with HtmlAgilityPack
  - 10-second timeout and 10,000 character limit (matching OpenManus)
  - Same user agent and error handling patterns

### Message Handling
- Internal `ChatMessage` format used for agent communication
- Built-in conversion methods for Semantic Kernel integration
- Supports tool calls, metadata, and role mapping

### Orleans Integration
- Uses Orleans for distributed agent hosting
- Agents are Orleans grains with persistent state
- Supports clustering and scalability

## Development Notes

- **Framework**: .NET 9.0
- **Nullable**: Enabled across all projects
- **Experimental Features**: SK experimental features enabled with warning suppression
- **Logging**: Serilog with console and OpenTelemetry sinks
- **Testing**: xUnit with project references

## Key Classes to Understand

- `PsiOmniGAgent`: Main agent implementation with mode switching
- `PsiOmniGAgentState`: State management and event sourcing
- `ChatMessage`: Internal message format with Semantic Kernel integration
- `KernelFactory`: Creates and configures Semantic Kernel instances
- `AgentConfiguration`: Agent setup and tool selection
- `WebSearchService`: Multi-engine search with `_try_all_engines` matching OpenManus exactly
- `GoogleSearchEngine`, `DuckDuckGoSearchEngine`, `BingSearchEngine`: Individual search engines
- `WebContentFetcher`: Content fetching matching OpenManus fetch_content function

## Visualization

The system includes a web-based visualization tool (`tools/visualize-v2.html`) that renders:
- Agent hierarchy and relationships
- Task flow and dependencies
- Agent states and chat history
- Tool usage and interactions