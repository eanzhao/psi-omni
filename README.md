# Psi Omni Agent

Welcome to the Psi Omni Agent demo! This guide provides step-by-step instructions to run the complete workflow from agent creation to state visualization.

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) installed
- Git and a Unix-like shell (macOS/Linux recommended)
- Valid API keys for Azure OpenAI, Tavily, and Google Search

## Environment Variables Setup

Before running the demo, you need to set up the following environment variables. All of these are required for the Psi Agent to function properly:

```bash
export AZURE_OPENAI_DEPLOYMENT_NAME="your-deployment-name"
export AZURE_OPENAI_API_KEY="your-azure-openai-api-key"
export AZURE_OPENAI_API_VERSION="2025-01-01-preview"
export AZURE_OPENAI_ENDPOINT="https://your-resource.cognitiveservices.azure.com/"
```

Alternatively, you can create a `.env` file in the project root with these variables.

## Build the Project

```bash
git clone https://github.com/gldeng/psi-omni.git
cd psi-omni
dotnet build
```

## Execution Steps

### Step 1: Start the Host Service

Start the Host service which provides the Orleans cluster and agent hosting:

```bash
cd src/Aevatar.Workshop.Host/bin/Debug/net9.0
./Aevatar.Workshop.Host
```

**Expected Output:**
- You should see Orleans cluster startup messages
- The service will indicate when it's ready to accept connections
- Look for messages indicating the Orleans port is open

### Step 2: Run the Create Command

Once the Host is running and Orleans port is open, open a new terminal and run the create command:

```bash
cd src/Psi/bin/Debug/net9.0
./Psi create "Calculate what percentage of US GDP was contributed by New York state in 2024"
```

**Expected Output:**
The Host terminal should show something like:
```
[17:41:32 INF] Result:
{
  "Final": "The GDP of the United States for 2024 is $28.5 trillion, and the GDP of New York state for 2024 is $2.2 trillion. The percentage contribution of New York state to the US GDP is approximately 7.72%."
}
```

### Step 3: Run the Continue Command

After the create command finishes successfully, run the continue command:

```bash
./Psi continue "What about California?"
```

**Expected Output:**
The Host terminal should show something like:
```
[17:41:48 INF] Result:
{
  "Final": "The GDP of the United States for 2024 is $28.5 trillion, and the GDP of California state for 2024 is $4.1 trillion. The percentage contribution of California state to the US GDP is approximately 14.39%."
}
```

### Step 4: Get the Agent State

After the continue command finishes, run the state command to get the current agent state:

```bash
./Psi state
```

**Expected Output:**
This will output a JSON representation of the agent state, including all the nested task relationships and chat history.

### Step 5: Visualize the Agent State

1. Copy the JSON output from the state command
2. Open `tools/visualize-v2.html` in your web browser
3. Paste the JSON data into the text box
4. Click "Render Graph"

> **💡 Quick Demo:** Want to see the visualization in action before running the full demo? You can use the sample data provided in `tools/sample_state.json`. Simply copy the contents of this file and paste it into the visualization tool to see how the agent network looks.

You will see an interactive graph showing:
- Agent hierarchy and relationships
- Task flow and dependencies
- Agent states and chat history
- Tool usage and interactions

The visualization provides a comprehensive view of how the Psi Agent system orchestrated multiple specialized agents to complete the complex tasks.

## Understanding the Demo

This demo showcases the Aevatar framework's Psi Agent system, which:

1. **Creates** an initial analysis task about New York state's GDP contribution
2. **Continues** the conversation by extending the analysis to California
3. **Maintains State** across multiple interactions and agent collaborations
4. **Orchestrates** multiple specialized agents (research, analysis, web search, etc.)
5. **Visualizes** the entire agent interaction network and task flow

The Psi Agent system demonstrates advanced multi-agent orchestration, where different agents collaborate to complete complex analytical tasks requiring web research, data analysis, and synthesis.

## Troubleshooting

### Common Issues

**Host Service Won't Start:**
- Check that .NET 9.0 SDK is installed: `dotnet --version`
- Ensure no other services are using the Orleans ports
- Verify all environment variables are set correctly

**API Key Errors:**
- Verify all API keys are valid and have sufficient quota
- Check that the Azure OpenAI deployment name matches your actual deployment
- Ensure the Azure OpenAI endpoint URL is correct

**Psi Commands Fail:**
- Make sure the Host service is running and ready
- Check that you're running commands from the correct directory
- Verify environment variables are available in the terminal session

**Visualization Issues:**
- Ensure the JSON from the state command is valid
- Try refreshing the browser page
- Check browser console for any JavaScript errors

## Next Steps

- Experiment with different query types in the create command
- Explore the agent state JSON to understand the system architecture
- Try modifying the visualization HTML to add new features
- Investigate the source code to understand the Psi Agent implementation

---

Happy exploring with Aevatar Psi Agents! 🚀 