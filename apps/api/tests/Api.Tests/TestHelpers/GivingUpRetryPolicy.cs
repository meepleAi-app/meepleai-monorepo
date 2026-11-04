using Api.Services;

namespace Api.Tests.TestHelpers;

/// <summary>
/// Doppio di <see cref="ICacheInvalidationRetryPolicy"/> che SI ARRENDE (lancia) per le chiamate scelte e
/// registra i nomi di operazione in ordine di arrivo: dice quali passi di evizione un chiamante ha provato.
/// Per le altre chiamate esegue l'operazione una volta, come <see cref="PassthroughRetryPolicy"/>.
/// </summary>
/// <remarks>
/// #4659: la politica di chi scrive i tag a mano è «registra e prosegui», e per provarla serve una retry
/// policy che a un certo punto smetta di riuscire, come Redis che non risponde dopo tutti i tentativi.
/// </remarks>
internal sealed class GivingUpRetryPolicy : ICacheInvalidationRetryPolicy
{
    private readonly Func<string, int, bool> _givesUpFor;
    private readonly List<string> _operations = [];
    private readonly List<CancellationToken> _tokens = [];

    private GivingUpRetryPolicy(Func<string, int, bool> givesUpFor) => _givesUpFor = givesUpFor;

    /// <summary>Si arrende a ogni chiamata.</summary>
    public static GivingUpRetryPolicy Always() => new((_, _) => true);

    /// <summary>Si arrende solo per l'operazione con questo nome.</summary>
    public static GivingUpRetryPolicy ForOperation(string operationName) =>
        new((name, _) => string.Equals(name, operationName, StringComparison.Ordinal));

    /// <summary>Si arrende solo alla chiamata numero <paramref name="callIndex"/> (da zero, nell'ordine di arrivo).</summary>
    public static GivingUpRetryPolicy ForCall(int callIndex) => new((_, index) => index == callIndex);

    /// <summary>Si arrende solo alle chiamate con questi numeri (da zero, nell'ordine di arrivo).</summary>
    public static GivingUpRetryPolicy ForCalls(params int[] callIndexes) => new((_, index) => callIndexes.Contains(index));

    /// <summary>I nomi di operazione ricevuti, nell'ordine, compresi quelli a cui si è arreso.</summary>
    public IReadOnlyList<string> Operations => _operations;

    /// <summary>I token ricevuti, nello stesso ordine di <see cref="Operations"/>: dicono di chi dipende ogni passo.</summary>
    public IReadOnlyList<CancellationToken> Tokens => _tokens;

    public Task ExecuteAsync(
        Func<CancellationToken, ValueTask> operation,
        string operationName,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrEmpty(operationName);

        var index = _operations.Count;
        _operations.Add(operationName);
        _tokens.Add(ct);

        return _givesUpFor(operationName, index)
            ? Task.FromException(new InvalidOperationException($"redis down ({operationName})"))
            : operation(ct).AsTask();
    }
}
