---
title: Current SDK validation
version: "1.0.0"
type: concept
permalink: current-sdk-validation
created: 2026-10-09T12:45:00Z
updated: 2026-10-09T12:45:00Z
tags: [a2a, testing, validation]
---

# Current SDK validation

## Observations

- [fact] A2A 1.0 preview uses `IAgentHandler`, `AgentEventQueue`, `Message`
  and `SupportedInterfaces`. Register with `AddA2AAgent`, then use `MapA2A`
  for JSON-RPC and the well-known card. The shared message bridge has
  cancellation, text ordering and response-queue regressions.
- [tip] xUnit discovery requires public test classes. Full `dotnet format`
  recommends internal classes even when the build accepts the xUnit shape.
  Preserve discovery and record formatter evidence separately.
- [tip] Client-construction tests use offline OpenRouter configuration and
  restore environment values in the serialized environment-test collection.
  They do not require a production credential.
- [tip] Null characters provide invalid filesystem paths on every supported
  platform. Punctuation forbidden only by Windows is valid on Linux.
- [tip] After timed `Process.WaitForExit`, call its parameterless overload
  before collecting asynchronous stdout/stderr buffers.
