#!/bin/bash

# Update IntegrationTestBase.cs
FILE="tests/Api.Tests/Infrastructure/IntegrationTestBase.cs"

# Add using statement after existing usings
sed -i '6 a using Api.SharedKernel.Application.Services;' "$FILE"

# Add MockEventCollector property after TimeProvider
sed -i '/protected TimeProvider TimeProvider/a \    protected Mock<IDomainEventCollector> MockEventCollector { get; private set; } = null!;' "$FILE"

# Update all MeepleAiDbContext constructor calls to include mockEventCollector
sed -i 's/new Mock<IMediator>();/new Mock<IMediator>();\n        var mockEventCollector = new Mock<IDomainEventCollector>();/g' "$FILE"
sed -i 's/new MeepleAiDbContext(options, mockMediator.Object)/new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object)/g' "$FILE"
sed -i 's/new MeepleAiDbContext(tempOptions, mockMediator.Object)/new MeepleAiDbContext(tempOptions, mockMediator.Object, mockEventCollector.Object)/g' "$FILE"

# Special case: change one of the mockEventCollector declarations to assign to MockEventCollector property
sed -i 's/DbContext = new MeepleAiDbContext/MockEventCollector = new Mock<IDomainEventCollector>();\n        DbContext = new MeepleAiDbContext/g' "$FILE"

echo "Updated IntegrationTestBase.cs"
