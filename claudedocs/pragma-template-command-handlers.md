# Pragma Template for Command/Query Handlers

## Pattern Template

Per tutti i Command e Query Handlers con pattern simile agli OAuth handlers completati:

```csharp
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: COMMAND HANDLER PATTERN - CQRS handler boundary
        // Specific exceptions (ValidationException, DomainException, etc.) caught separately above.
        // Generic catch handles unexpected infrastructure failures (DB, network, memory)
        // to prevent exception propagation to API layer. Returns Result<T> or appropriate response.
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in {Handler}", nameof(HandlerClassName));
            return Result.Failure("An unexpected error occurred");
        }
#pragma warning restore CA1031
```

## File List for Batch Application

Rimanenti Command/Query Handlers da completare:

1. ResetPasswordCommandHandler.cs
2. ExtendSessionCommandHandler.cs
3. AdminDisable2FACommandHandler.cs
4. Disable2FACommandHandler.cs
5. Enable2FACommandHandler.cs
6. BulkImportApiKeysCommandHandler.cs (2 catch - simile a BulkImport pattern)
7. CompleteChunkedUploadCommandHandler.cs (4 catch - analisi necessaria)
8. InitChunkedUploadCommandHandler.cs
9. UploadChunkCommandHandler.cs
10. RunEvaluationCommandHandler.cs
11. ConfigureAgentCommandHandler.cs
12. InvokeAgentCommandHandler.cs (già ha pragma S2139)

## Query Handlers (tutti pattern simile)

Tutti in `Application/Queries/**/*QueryHandler.cs` e `Application/Handlers/*QueryHandler.cs`

Pattern: QUERY HANDLER PATTERN (identico a COMMAND HANDLER)
