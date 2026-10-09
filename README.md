# HemSoft Power AI

AI-powered command-line assistant with multi-agent orchestration using Microsoft Agent Framework and OpenRouter.

## Quick Start

1. Set required environment variables:

   ```powershell
   $env:OPENROUTER_API_KEY = "your-api-key"
   $env:OPENROUTER_BASE_URL = "https://openrouter.ai/api/v1"
   ```

2. Run the full stack (Console + AgentWorker + AgentHost):

   ```powershell
   .\run-all.ps1
   ```

   Or run just the Console:

   ```powershell
   .\run.ps1
   ```

3. Chat naturally or type `/` to access the menu.

## Architecture

```text
┌─────────────────────────────────────────────────────────────────────┐
│                    HemSoft.PowerAI.Console (Publisher)               │
│  User: /agents → "Research competitor pricing for widgets"          │
│  → Publishes AgentTaskRequest to Redis                              │
│  → Continues chatting...                                            │
│  → Receives AgentTaskResult notification                            │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│                         REDIS (Pub/Sub)                              │
│  agents:tasks          → Task channel for worker consumption        │
│  agents:results:{id}   → Completion notifications per task          │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────┐
│                 HemSoft.PowerAI.AgentWorker (Subscriber)             │
│  → Subscribes to agents:tasks channel                               │
│  → Executes agent autonomously (ResearchAgent, etc.)                │
│  → Publishes AgentTaskResult to agents:results:{taskId}             │
└─────────────────────────────────────────────────────────────────────┘
```

### Project Structure

| Project | Purpose |
|---------|---------|
| **Console** | Interactive UI, task publisher |
| **AgentWorker** | Background task processor (Redis subscriber) |
| **AgentHost** | A2A HTTP protocol hosting |
| **Shared** | Common models, interfaces, agents |

## Usage

The app has two modes accessible via `/`:

| Option | What it does |
|--------|--------------|
| **Model** | Change the AI model |
| **Agents** | Submit async tasks to agents |

### Agent Tasks (Event-Driven)

Submit research tasks that execute asynchronously:

1. Type `/` and select **Agents**
2. Choose **Submit Research Task**
3. Enter your research query
4. Continue chatting while the task runs in background
5. Get notified when results are ready

### Built-in Tools

The main chat has access to:

- **Terminal** - Execute PowerShell commands
- **WebSearch** - Search the web for information
- **Mail** - Full Outlook/Hotmail access (read, send, delete, search, move, spam management)

## Configuration

### Environment Variables

| Variable              | Required | Description                                      |
| --------------------- | -------- | ------------------------------------------------ |
| `OPENROUTER_API_KEY`  | ✅ Yes    | Your OpenRouter API key                          |
| `OPENROUTER_BASE_URL` | ✅ Yes    | `https://openrouter.ai/api/v1`                   |
| `GRAPH_CLIENT_ID`     | For Mail | Azure app registration client ID                 |
| `RESEARCH_AGENT_URL`  | Optional | Remote research agent URL (for distributed mode) |

### Application Settings (appsettings.json)

```json
{
  "SpamFilter": {
    "BatchSize": 10,
    "DelayBetweenBatchesSeconds": 30,
    "SpamDomainsFilePath": "Data/SpamDomains.json",
    "SpamCandidatesFilePath": "Data/SpamCandidates.json"
  },
  "A2A": {
    "DefaultResearchAgentUrl": "http://localhost:7071/",
    "ResearchAgentHostPort": 5001
  }
}
```

The standalone AgentHost advertises the address used for discovery. Behind a
reverse proxy, set `AgentHost__PublicUrl` to its public JSON-RPC endpoint. This
sets `AgentHost:PublicUrl` in .NET configuration. Console-hosted agents preserve
an explicitly configured public card URL; local cards use the discovery request
address. Both hosts expose `/.well-known/agent-card.json`.

### Microsoft Graph Setup (for Outlook Mail)

1. Register an app at [Microsoft Entra admin center](https://entra.microsoft.com)
2. Set "Supported account types" to **Personal Microsoft accounts only**
3. Add **Mobile and desktop applications** platform with redirect URI: `http://localhost`
4. Enable **Allow public client flows**
5. Add API permissions: `User.Read`, `Mail.Read`, `Mail.ReadWrite`, `Mail.Send`
6. Set `GRAPH_CLIENT_ID` to your Application (client) ID

## Running

### Interactive Mode (default)

```powershell
.\run.ps1
# or
dotnet run --project src/HemSoft.PowerAI.Console
```

### Direct Commands

```powershell
# Run coordinator with a prompt
dotnet run --project src/HemSoft.PowerAI.Console -- coordinate "research quantum computing"

# Run spam filter agent
dotnet run --project src/HemSoft.PowerAI.Console -- spam

# Host research agent as A2A server
dotnet run --project src/HemSoft.PowerAI.Console -- host-research

# Connect to distributed agents
dotnet run --project src/HemSoft.PowerAI.Console -- distributed
```

## Architecture

Built on Microsoft Agent Framework with A2A (Agent-to-Agent) protocol support:

- **Local agents** run in-process for low latency
- **Remote agents** connect via A2A protocol for distributed workloads
- **Coordinator** intelligently routes tasks to specialist agents
