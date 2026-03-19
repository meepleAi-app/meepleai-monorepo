using Api.BoundedContexts.KnowledgeBase.Application.Commands.AdminStrategy;
using Api.BoundedContexts.KnowledgeBase.Application.Commands.AdminStrategy;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.AdminStrategy;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers.AdminStrategy;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class ListAdminStrategiesHandlerTests
{
    private readonly Mock<IAdminRagStrategyRepository> _mockRepo = new();

    [Fact]
    public async Task Handle_ReturnsAllStrategies()
    {
        var s1 = AdminRagStrategy.Create("Strategy A", "Desc A", "[{\"type\":\"retrieval\"}]", Guid.NewGuid());
        var s2 = AdminRagStrategy.Create("Strategy B", "Desc B", "[{\"type\":\"generation\"}]", Guid.NewGuid());

        _mockRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AdminRagStrategy> { s1, s2 });

        var handler = new ListAdminStrategiesHandler(_mockRepo.Object);
        var result = await handler.Handle(new ListAdminStrategiesQuery(), TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Equal("Strategy A", result[0].Name);
        Assert.Equal("Strategy B", result[1].Name);
    }

    [Fact]
    public async Task Handle_EmptyRepository_ReturnsEmptyList()
    {
        _mockRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AdminRagStrategy>());

        var handler = new ListAdminStrategiesHandler(_mockRepo.Object);
        var result = await handler.Handle(new ListAdminStrategiesQuery(), TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class GetAdminStrategyByIdHandlerTests
{
    private readonly Mock<IAdminRagStrategyRepository> _mockRepo = new();

    [Fact]
    public async Task Handle_ExistingId_ReturnsStrategy()
    {
        var strategy = AdminRagStrategy.Create("Test", "Desc", "[]", Guid.NewGuid());
        _mockRepo.Setup(r => r.GetByIdAsync(strategy.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(strategy);

        var handler = new GetAdminStrategyByIdHandler(_mockRepo.Object);
        var result = await handler.Handle(new GetAdminStrategyByIdQuery(strategy.Id), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("Test", result.Name);
    }

    [Fact]
    public async Task Handle_NonExistingId_ReturnsNull()
    {
        _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AdminRagStrategy?)null);

        var handler = new GetAdminStrategyByIdHandler(_mockRepo.Object);
        var result = await handler.Handle(new GetAdminStrategyByIdQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class CreateAdminStrategyHandlerTests
{
    private readonly Mock<IAdminRagStrategyRepository> _mockRepo = new();
    private readonly Mock<ILogger<CreateAdminStrategyHandler>> _mockLogger = new();

    [Fact]
    public async Task Handle_ValidCommand_CreatesAndReturnsStrategy()
    {
        var adminId = Guid.NewGuid();
        var command = new CreateAdminStrategyCommand("New Strategy", "Description", "[{\"type\":\"retrieval\",\"config\":{},\"order\":0}]", adminId);

        var handler = new CreateAdminStrategyHandler(_mockRepo.Object, _mockLogger.Object);
        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("New Strategy", result.Name);
        Assert.Equal("Description", result.Description);
        _mockRepo.Verify(r => r.AddAsync(It.IsAny<AdminRagStrategy>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class UpdateAdminStrategyHandlerTests
{
    private readonly Mock<IAdminRagStrategyRepository> _mockRepo = new();
    private readonly Mock<ILogger<UpdateAdminStrategyHandler>> _mockLogger = new();

    [Fact]
    public async Task Handle_ExistingStrategy_UpdatesAndReturns()
    {
        var strategy = AdminRagStrategy.Create("Old", "Old desc", "[]", Guid.NewGuid());
        _mockRepo.Setup(r => r.GetByIdAsync(strategy.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(strategy);

        var command = new UpdateAdminStrategyCommand(strategy.Id, "Updated", "New desc", "[{\"type\":\"generation\"}]", Guid.NewGuid());

        var handler = new UpdateAdminStrategyHandler(_mockRepo.Object, _mockLogger.Object);
        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.Equal("Updated", result.Name);
        Assert.Equal("New desc", result.Description);
        _mockRepo.Verify(r => r.UpdateAsync(It.IsAny<AdminRagStrategy>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NonExistingStrategy_ThrowsKeyNotFoundException()
    {
        _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AdminRagStrategy?)null);

        var command = new UpdateAdminStrategyCommand(Guid.NewGuid(), "Name", "Desc", "[]", Guid.NewGuid());

        var handler = new UpdateAdminStrategyHandler(_mockRepo.Object, _mockLogger.Object);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => handler.Handle(command, TestContext.Current.CancellationToken));
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class DeleteAdminStrategyHandlerTests
{
    private readonly Mock<IAdminRagStrategyRepository> _mockRepo = new();
    private readonly Mock<ILogger<DeleteAdminStrategyHandler>> _mockLogger = new();

    [Fact]
    public async Task Handle_ExistingStrategy_SoftDeletesAndReturnsTrue()
    {
        var strategy = AdminRagStrategy.Create("ToDelete", "Desc", "[]", Guid.NewGuid());
        _mockRepo.Setup(r => r.GetByIdAsync(strategy.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(strategy);

        var handler = new DeleteAdminStrategyHandler(_mockRepo.Object, _mockLogger.Object);
        var result = await handler.Handle(new DeleteAdminStrategyCommand(strategy.Id, Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.True(strategy.IsDeleted);
        _mockRepo.Verify(r => r.UpdateAsync(strategy, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NonExistingStrategy_ReturnsFalse()
    {
        _mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AdminRagStrategy?)null);

        var handler = new DeleteAdminStrategyHandler(_mockRepo.Object, _mockLogger.Object);
        var result = await handler.Handle(new DeleteAdminStrategyCommand(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.False(result);
    }
}
