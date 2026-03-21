using Api.BoundedContexts.Authentication.Application.Commands;
using Api.SharedKernel.Application.Behaviors;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Moq;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.SharedKernel.Application.Behaviors;

/// <summary>
/// Integration tests for ValidationBehavior.
/// Issue #1449: FluentValidation for Authentication CQRS pipeline
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task Should_Continue_To_Handler_When_No_Validators_Are_Registered()
    {
        // Arrange
        var command = new LoginCommand(
            Email: "test@example.com",
            Password: "Password123!"
        );

        var nextCalled = false;
        RequestHandlerDelegate<object> next = (ct) =>
        {
            nextCalled = true;
            return Task.FromResult<object>(new object());
        };

        var validators = Enumerable.Empty<IValidator<LoginCommand>>();
        var behavior = new ValidationBehavior<LoginCommand, object>(validators);

        // Act
        var result = await behavior.Handle(command, next, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Continue_To_Handler_When_Validation_Passes()
    {
        // Arrange
        var command = new LoginCommand(
            Email: "test@example.com",
            Password: "Password123!"
        );

        var mockValidator = new Mock<IValidator<LoginCommand>>();
        mockValidator
            .Setup(x => x.ValidateAsync(It.IsAny<ValidationContext<LoginCommand>>(), default))
            .ReturnsAsync(new ValidationResult());

        var nextCalled = false;
        RequestHandlerDelegate<object> next = (ct) =>
        {
            nextCalled = true;
            return Task.FromResult<object>(new object());
        };

        var validators = new[] { mockValidator.Object };
        var behavior = new ValidationBehavior<LoginCommand, object>(validators);

        // Act
        var result = await behavior.Handle(command, next, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Throw_ValidationException_When_Validation_Fails()
    {
        // Arrange
        var command = new LoginCommand(
            Email: "invalid-email",
            Password: "short"
        );

        var validationFailures = new List<ValidationFailure>
        {
            new("Email", "Email must be a valid email address"),
            new("Password", "Password must be at least 8 characters")
        };

        var mockValidator = new Mock<IValidator<LoginCommand>>();
        mockValidator
            .Setup(x => x.ValidateAsync(It.IsAny<ValidationContext<LoginCommand>>(), default))
            .ReturnsAsync(new ValidationResult(validationFailures));

        var nextCalled = false;
        RequestHandlerDelegate<object> next = (ct) =>
        {
            nextCalled = true;
            return Task.FromResult<object>(new object());
        };

        var validators = new[] { mockValidator.Object };
        var behavior = new ValidationBehavior<LoginCommand, object>(validators);

        // Act & Assert
        var act = () => behavior.Handle(command, next, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<ValidationException>()).Which;

        exception.Errors.Count().Should().Be(2);
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Should_Run_Multiple_Validators()
    {
        // Arrange
        var command = new LoginCommand(
            Email: "test@example.com",
            Password: "Password123!"
        );

        var mockValidator1 = new Mock<IValidator<LoginCommand>>();
        mockValidator1
            .Setup(x => x.ValidateAsync(It.IsAny<ValidationContext<LoginCommand>>(), default))
            .ReturnsAsync(new ValidationResult());

        var mockValidator2 = new Mock<IValidator<LoginCommand>>();
        mockValidator2
            .Setup(x => x.ValidateAsync(It.IsAny<ValidationContext<LoginCommand>>(), default))
            .ReturnsAsync(new ValidationResult());

        var nextCalled = false;
        RequestHandlerDelegate<object> next = (ct) =>
        {
            nextCalled = true;
            return Task.FromResult<object>(new object());
        };

        var validators = new[] { mockValidator1.Object, mockValidator2.Object };
        var behavior = new ValidationBehavior<LoginCommand, object>(validators);

        // Act
        var result = await behavior.Handle(command, next, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        mockValidator1.Verify(
            x => x.ValidateAsync(It.IsAny<ValidationContext<LoginCommand>>(), default),
            Times.Once
        );
        mockValidator2.Verify(
            x => x.ValidateAsync(It.IsAny<ValidationContext<LoginCommand>>(), default),
            Times.Once
        );
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Aggregate_Errors_From_Multiple_Validators()
    {
        // Arrange
        var command = new LoginCommand(
            Email: "invalid-email",
            Password: "short"
        );

        var validationFailures1 = new List<ValidationFailure>
        {
            new("Email", "Email must be a valid email address")
        };

        var validationFailures2 = new List<ValidationFailure>
        {
            new("Password", "Password must be at least 8 characters")
        };

        var mockValidator1 = new Mock<IValidator<LoginCommand>>();
        mockValidator1
            .Setup(x => x.ValidateAsync(It.IsAny<ValidationContext<LoginCommand>>(), default))
            .ReturnsAsync(new ValidationResult(validationFailures1));

        var mockValidator2 = new Mock<IValidator<LoginCommand>>();
        mockValidator2
            .Setup(x => x.ValidateAsync(It.IsAny<ValidationContext<LoginCommand>>(), default))
            .ReturnsAsync(new ValidationResult(validationFailures2));

        var nextCalled = false;
        RequestHandlerDelegate<object> next = (ct) =>
        {
            nextCalled = true;
            return Task.FromResult<object>(new object());
        };

        var validators = new[] { mockValidator1.Object, mockValidator2.Object };
        var behavior = new ValidationBehavior<LoginCommand, object>(validators);

        // Act & Assert
        var act2 = () => behavior.Handle(command, next, TestContext.Current.CancellationToken);
        var exception = (await act2.Should().ThrowAsync<ValidationException>()).Which;

        exception.Errors.Count().Should().Be(2);
        exception.Errors.Should().Contain(e => e.PropertyName == "Email");
        exception.Errors.Should().Contain(e => e.PropertyName == "Password");
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Should_Respect_CancellationToken()
    {
        // Arrange
        var command = new LoginCommand(
            Email: "test@example.com",
            Password: "Password123!"
        );

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var mockValidator = new Mock<IValidator<LoginCommand>>();
        mockValidator
            .Setup(x => x.ValidateAsync(It.IsAny<ValidationContext<LoginCommand>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var nextCalled = false;
        RequestHandlerDelegate<object> next = (ct) =>
        {
            nextCalled = true;
            return Task.FromResult<object>(new object());
        };

        var validators = new[] { mockValidator.Object };
        var behavior = new ValidationBehavior<LoginCommand, object>(validators);

        // Act & Assert
        var act3 = () => behavior.Handle(command, next, cts.Token);
        await act3.Should().ThrowAsync<OperationCanceledException>();

        nextCalled.Should().BeFalse();
    }
}

