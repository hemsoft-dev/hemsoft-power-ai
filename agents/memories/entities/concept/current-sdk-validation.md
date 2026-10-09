---
title: Current SDK validation
version: "1.0.1"
type: concept
permalink: current-sdk-validation
created: 2026-10-09T12:52:03Z
updated: 2026-10-09T13:12:30Z
tags: [a2a, testing, validation]
---

## Observations

- [fact] A2A 1.0 preview uses `IAgentHandler`, `AgentEventQueue`, `Message`
  and `SupportedInterfaces`. `AddA2AAgent` registers its own handler; bind
  `IAgentHandler` explicitly to the configured instance afterwards.
  `MapA2A` serves JSON-RPC only despite its preview XML comment. Map the
  well-known card separately and advertise the request-visible or configured
  public endpoint. The shared message bridge has
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
- [tip] Optional progress cleanup must be bounded and observe late faults
  without replacing an already completed research result.
