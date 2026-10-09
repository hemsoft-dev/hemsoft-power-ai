// <copyright file="HostingRegressionTests.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Console.Tests;

using System.Net.Http.Json;

using A2A;

using HemSoft.PowerAI.AgentHost.Configuration;
using HemSoft.PowerAI.AgentHost.Extensions;
using HemSoft.PowerAI.Common.Agents;
using HemSoft.PowerAI.Console.Hosting;
using HemSoft.PowerAI.Console.Services;

using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

using Moq;

/// <summary>
/// Verifies real discovery, configured handler resolution, and bounded progress cleanup.
/// </summary>
public class HostingRegressionTests
{
    private static readonly HttpClient Client = new();

    /// <summary>
    /// Verifies the console host serves metadata and resolves its configured handler.
    /// </summary>
    /// <param name="address">The card endpoint.</param>
    /// <param name="configured">Whether the endpoint is publicly configured.</param>
    /// <returns>The asynchronous verification.</returns>
    [Theory]
    [InlineData("http://localhost:5001/", false)]
    [InlineData("https://agents.example.test/research", true)]
    public async Task ConsoleHostServesReachableCard(string address, bool configured)
    {
        var card = AgentCards.CreateResearchAgentCard(new Uri(address));
        var host = new A2AAgentHost(Mock.Of<AIAgent>(), card, 0);
        await using var ownedHost = host.ConfigureAwait(true);
        await host.StartAsync().ConfigureAwait(true);
        var listening = Assert.IsType<Uri>(host.ListeningUri);
        var discovered = await Client.GetFromJsonAsync<AgentCard>(
            new Uri(listening, "/.well-known/agent-card.json"), CancellationToken.None)
            .ConfigureAwait(true);
        Assert.NotNull(discovered);
        Assert.Equal(configured ? address : listening.ToString(), Assert.Single(discovered.SupportedInterfaces).Url);
        Assert.Equal(address, Assert.Single(card.SupportedInterfaces).Url);
        Assert.Equal(card.Name, discovered.Name);
        await host.StopAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Verifies the standalone host advertises request-visible or explicitly configured endpoints.
    /// </summary>
    /// <param name="configuredAddress">The optional public address.</param>
    /// <returns>The asynchronous verification.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData("https://agents.example.test/research")]
    public async Task StandaloneHostServesReachableCard(string? configuredAddress)
    {
        var builder = WebApplication.CreateSlimBuilder();
        if (configuredAddress is not null)
        {
            builder.Configuration.AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.Ordinal) { ["AgentHost:PublicUrl"] = configuredAddress });
        }

        builder.Services.AddResearchAgent(new AgentHostOptions());
        var app = builder.Build();
        await using var ownedApp = app.ConfigureAwait(true);
        app.MapAgentEndpoints("ResearchAgent");
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync(CancellationToken.None).ConfigureAwait(true);
        var listening = new Uri(Assert.Single(app.Urls));
        var discovered = await Client.GetFromJsonAsync<AgentCard>(
            new Uri(listening, "/.well-known/agent-card.json"), CancellationToken.None)
            .ConfigureAwait(true);
        Assert.NotNull(discovered);
        Assert.Equal(configuredAddress ?? listening.ToString(), Assert.Single(discovered.SupportedInterfaces).Url);
        await app.StopAsync(CancellationToken.None).ConfigureAwait(true);
    }

    /// <summary>
    /// Verifies failed optional progress cleanup does not replace the task result.
    /// </summary>
    /// <returns>The asynchronous verification.</returns>
    [Fact]
    public async Task ProgressFaultIsObservedWithoutThrowing()
    {
        using var cancellation = new CancellationTokenSource();
        var messages = new List<string>();
        await ProgressSubscription.CompleteAsync(
            Task.FromException(new InvalidOperationException("fixture")), cancellation, messages.Add, TimeSpan.FromSeconds(1))
            .ConfigureAwait(true);
        Assert.True(cancellation.IsCancellationRequested);
        Assert.Contains("fixture", Assert.Single(messages), StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies stalled optional cleanup returns within its limit and observes a later failure.
    /// </summary>
    /// <returns>The asynchronous verification.</returns>
    [Fact]
    public async Task ProgressStallIsBoundedAndLateFaultObserved()
    {
        using var cancellation = new CancellationTokenSource();
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var late = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var messages = new List<string>();
        void Log(string message)
        {
            messages.Add(message);
            if (message.Contains("late fixture", StringComparison.Ordinal))
            {
                late.SetResult();
            }
        }

        await ProgressSubscription.CompleteAsync(pending.Task, cancellation, Log, TimeSpan.FromMilliseconds(10))
            .ConfigureAwait(true);
        Assert.True(cancellation.IsCancellationRequested);
        Assert.Contains("time limit", Assert.Single(messages), StringComparison.Ordinal);
        pending.SetException(new InvalidOperationException("late fixture"));
        await late.Task.WaitAsync(TimeSpan.FromSeconds(1), CancellationToken.None).ConfigureAwait(true);
        Assert.Equal(2, messages.Count);
    }
}
