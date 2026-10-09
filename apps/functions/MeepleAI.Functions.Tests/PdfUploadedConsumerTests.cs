using Azure.Messaging.ServiceBus;
using FluentAssertions;
using MeepleAI.Functions.Contracts;
using MeepleAI.Functions.Forwarding;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace MeepleAI.Functions.Tests;

public sealed class PdfUploadedConsumerTests
{
    private readonly IPdfUploadedForwarder _forwarder = Substitute.For<IPdfUploadedForwarder>();
    private readonly ServiceBusMessageActions _actions = Substitute.For<ServiceBusMessageActions>();

    private PdfUploadedConsumer Sut() => new(_forwarder, NullLogger<PdfUploadedConsumer>.Instance);

    private static ServiceBusReceivedMessage Message(string json) =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString(json), messageId: "m-1");

    [Fact]
    public async Task Run_ValidContract_ForwardsAndCompletes()
    {
        _forwarder.ForwardAsync(Arg.Any<PdfUploadedEventV1>(), Arg.Any<CancellationToken>()).Returns(ForwardOutcome.Settled);
        var message = Message(ContractFixture.ReadV1());

        await Sut().Run(message, _actions, TestContext.Current.CancellationToken);

        await _forwarder.Received(1).ForwardAsync(Arg.Any<PdfUploadedEventV1>(), Arg.Any<CancellationToken>());
        await _actions.Received(1).CompleteMessageAsync(message, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_UnsupportedContract_DeadLettersWithoutForwarding()
    {
        var message = Message(ContractFixture.ReadV1().Replace("\"v1\"", "\"v7\"", StringComparison.Ordinal));

        await Sut().Run(message, _actions, TestContext.Current.CancellationToken);

        await _forwarder.DidNotReceiveWithAnyArgs().ForwardAsync(default!, TestContext.Current.CancellationToken);
        await _actions.Received(1).DeadLetterMessageAsync(
            message,
            deadLetterReason: PdfUploadedContract.ReasonUnsupportedContract,
            deadLetterErrorDescription: Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_MalformedBody_DeadLettersAsMalformed()
    {
        var message = Message("{oops");

        await Sut().Run(message, _actions, TestContext.Current.CancellationToken);

        await _actions.Received(1).DeadLetterMessageAsync(
            message,
            deadLetterReason: PdfUploadedContract.ReasonMalformed,
            deadLetterErrorDescription: Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_TransientFailure_AbandonsForRedelivery()
    {
        _forwarder.ForwardAsync(Arg.Any<PdfUploadedEventV1>(), Arg.Any<CancellationToken>()).Returns(ForwardOutcome.TransientFailure);
        var message = Message(ContractFixture.ReadV1());

        await Sut().Run(message, _actions, TestContext.Current.CancellationToken);

        await _actions.Received(1).AbandonMessageAsync(message, cancellationToken: Arg.Any<CancellationToken>());
        await _actions.DidNotReceiveWithAnyArgs().CompleteMessageAsync(default!, TestContext.Current.CancellationToken);
    }
}
