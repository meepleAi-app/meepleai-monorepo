using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #4552: un mapper che ricostruisce un aggregato da una riga non passa dalla factory di un oggetto NUOVO.
/// </summary>
/// <remarks>
/// <para>
/// <c>AlertConfigurationRepository.MapToDomain</c> ricostruiva la riga con <c>AlertConfiguration.Create</c>.
/// Quella factory genera un id nuovo, data l'oggetto ad "adesso" e azzera <c>IsEncrypted</c>: ogni lettura
/// restituiva un aggregato con un id che nel database non esiste, e la modifica di una configurazione
/// esistente cercava la riga per quell'id e falliva sempre. Prima ancora, #4544: <c>LibraryShareLink.Create</c>
/// rifiutava la scadenza passata di un link salvato. La forma è la stessa: la factory di creazione decide
/// cose che, per una riga già salvata, sono già state decise.
/// </para>
/// <para>
/// <b>Come cerca.</b> Non legge il testo: legge l'IL compilato, quindi vede la chiamata che il compilatore
/// ha risolto, anche dentro le lambda. Un <i>mapper</i> è un metodo che riceve un tipo entità del modello EF
/// e restituisce un tipo di dominio. Una <i>factory di creazione</i> è un metodo statico di un tipo di dominio
/// con identità (una proprietà <c>Id</c>) che restituisce quel tipo e che, direttamente o tramite i
/// costruttori e i metodi di dominio che chiama, genera un id (<c>Guid.NewGuid</c>) o solleva un evento di
/// dominio. Una factory senza nessuno dei due effetti — un <c>Reconstitute</c> — non viene segnalata,
/// qualunque sia il suo nome. L'ora corrente compare nel messaggio ma da sola non basta: molti
/// <c>Reconstitute</c> passano dal costruttore privato, che data l'oggetto ad "adesso", e poi sovrascrivono la
/// data con quella salvata.
/// </para>
/// <para>
/// Ogni altro metodo di dominio che il mapper chiama — statico o d'istanza, per esempio un metodo che ripristina un
/// figlio — viene seguito allo stesso modo, e conta solo l'evento: <c>PlayRecord.RecordScore</c>, usato per
/// ricaricare i punteggi, sollevava un evento per punteggio a ogni lettura. Un evento non conta se il mapper
/// chiama <c>ClearDomainEvents</c>; un'esenzione copre la factory, mai un evento lasciato sull'aggregato.
/// </para>
/// <para>
/// <b>Cosa non vede.</b> Una factory chiamata da un aiuto che non riceve l'entità (per esempio un metodo che
/// prende i campi già estratti). Una factory che riceve l'id ma data ad "adesso" senza che il mapper ripristini
/// la data (oggi <c>GameLabel.CreatePredefinedWithId</c>). Le validazioni della factory: una factory senza
/// effetti che rifiuta una data passata non viene segnalata. Un metodo d'istanza che genera un id per un figlio o
/// data ad "adesso" senza sollevare eventi: oggi conta solo l'evento. L'ordine delle chiamate: un
/// <c>ClearDomainEvents</c> in un punto qualunque del mapper basta. E una colonna che il mapper non ripristina: quella
/// la vedono le guardie per repository che leggono le colonne dal modello EF (per esempio
/// <c>AlertConfigurationRepositoryTests</c>). Il gate restringe il campo; il giudizio resta a chi scrive il
/// mapper.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "4552")]
public sealed class RepositoryReconstitutionArchitectureTests
{
    /// <summary>
    /// Mapper che oggi passano ancora da una factory di creazione, con il motivo per cui la factory non fa danni.
    /// La chiave è «tipo che contiene il mapper → tipo.factory», come la stampa il messaggio d'errore. Ogni voce
    /// è stata controllata leggendo mapper, factory, entità e chiamanti di <c>UpdateAsync</c> (#4552): non
    /// aggiungerne una senza lo stesso controllo. Correggere una voce con un <c>Reconstitute</c> e toglierla è
    /// sempre ammesso; <see cref="EveryExemption_IsStillNeeded"/> chiede di toglierla.
    /// </summary>
    private static readonly Dictionary<string, string> Exempt = new(StringComparer.Ordinal)
    {
        ["Api.BoundedContexts.AgentMemory.Infrastructure.Persistence.GameMemoryRepository → GameMemory.Create"] =
            "Id, CreatedAt e LastProcessedEventId tornano via reflection subito dopo; la factory non solleva eventi.",
        ["Api.BoundedContexts.AgentMemory.Infrastructure.Persistence.GameMemoryRepository → HouseRule.Restore"] =
            "Genera un id solo se la regola salvata nel JSON non ne ha uno (righe precedenti a #1464, migrazione " +
            "pigra voluta); un id salvato lo conserva.",
        ["Api.BoundedContexts.AgentMemory.Infrastructure.Persistence.GroupMemoryRepository → GroupMemory.Create"] =
            "Id, CreatedAt e LastProcessedEventId tornano via reflection, il JoinedAt dei membri subito dopo " +
            "AddMember; nessun evento.",
        ["Api.BoundedContexts.GameManagement.Infrastructure.Persistence.RuleDisputeRepository → RuleDispute.Open"] =
            "Id, CreatedAt e FinalOutcome tornano via reflection; Open non solleva eventi, e l'unico evento " +
            "(RaiseResolvedEvent) il mapper non lo chiama.",
        ["Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.AgentTestResultRepository → AgentTestResult.Create"] =
            "Id ed ExecutedAt tornano via reflection; nessun evento; UpdateAsync non ha chiamanti in produzione.",
        ["Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.ArbitroValidationFeedbackRepository → ArbitroValidationFeedback.Create"] =
            "Id e SubmittedAt tornano via reflection; nessun evento; trim e maiuscolo della factory sono già " +
            "applicati al salvataggio, quindi non cambiano il valore letto.",
        ["Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.DecisoreMoveFeedbackRepository → DecisoreMoveFeedback.Create"] =
            "Id e SubmittedAt tornano via reflection; nessun evento; trim e minuscolo della factory sono già " +
            "applicati al salvataggio, quindi non cambiano il valore letto.",
        ["Api.BoundedContexts.UserLibrary.Infrastructure.Persistence.GameLabelRepository → GameLabel.CreateCustom"] =
            "Id e CreatedAt tornano via reflection; GameLabel è un'Entity senza eventi.",
        ["Api.BoundedContexts.UserLibrary.Infrastructure.Persistence.GameSuggestionRepository → GameSuggestion.Create"] =
            "Id e CreatedAt tornano via reflection; l'evento di Accept, chiamato per ripristinare il flag, è pulito " +
            "con ClearDomainEvents prima del return.",
        ["Api.BoundedContexts.UserLibrary.Infrastructure.Persistence.PrivateGameRepository → PrivateGame.CreateFromBgg"] =
            "Id, date, BggSyncedAt e cancellazione tornano via reflection; eventi puliti con ClearDomainEvents.",
        ["Api.BoundedContexts.UserLibrary.Infrastructure.Persistence.PrivateGameRepository → PrivateGame.CreateManual"] =
            "Id, date e cancellazione tornano via reflection; eventi puliti con ClearDomainEvents.",
    };

    private const int MaxDepth = 4;

    private const string NewIdEffect = "genera un id";
    private const string CurrentTimeEffect = "legge l'ora corrente";
    private const string DomainEventEffect = "solleva un evento di dominio";

    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    private static readonly OpCode[] OneByteOpCodes = new OpCode[0x100];
    private static readonly OpCode[] TwoByteOpCodes = new OpCode[0x100];

#pragma warning disable S3963 // Initialize all static fields inline: the opcode tables are filled by a loop.
    static RepositoryReconstitutionArchitectureTests()
#pragma warning restore S3963
    {
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode code)
            {
                continue;
            }

            var value = (ushort)code.Value;
            if (code.Size == 1)
            {
                OneByteOpCodes[value] = code;
            }
            else
            {
                TwoByteOpCodes[value & 0xFF] = code;
            }
        }
    }

    [Fact]
    public void MappersFromARow_DoNotCallACreationFactory()
    {
        var found = FindCreationFactoryCallsInMappers();

        // Un'esenzione copre la factory, non un evento che il mapper lascia sull'aggregato: quello ripartirebbe
        // al primo salvataggio, quindi resta un errore anche per una chiave esentata.
        var offenders = found
            .Where(f => !Exempt.ContainsKey(f.Key) || f.UnclearedEvent)
            .Select(f => $"{f.Key}: {f.Effects} (in {f.Mapper})" +
                (f.UnclearedEvent ? " — evento non pulito: il mapper non chiama ClearDomainEvents" : ""))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        // Unite in una stringa, così il messaggio elenca ogni caso: BeEmpty su una lista mostra solo il primo.
        string.Join(Environment.NewLine, offenders).Should().BeEmpty(
            "a mapper that rebuilds an aggregate from a stored row must not go through the factory of a NEW " +
            "object, nor leave a domain event on it: the factory generates a new id, stamps the current time or " +
            "raises a creation event, and the row already has all three (#4552, #4544). Add an internal " +
            "Reconstitute that takes every column as it is stored, like GameSession.Reconstitute, and restore " +
            "children with methods that raise no event.");
    }

    [Fact]
    public void EveryExemption_IsStillNeeded()
    {
        var foundKeys = FindCreationFactoryCallsInMappers().Select(f => f.Key).ToHashSet(StringComparer.Ordinal);

        Exempt.Keys.Where(k => !foundKeys.Contains(k)).Order(StringComparer.Ordinal).Should().BeEmpty(
            "an exemption whose mapper no longer calls the factory is stale: remove it, or it will silently " +
            "cover the next mapper that reintroduces the call");
    }

    private sealed record Finding(string Key, string Mapper, string Effects, bool UnclearedEvent);

    private static List<Finding> FindCreationFactoryCallsInMappers()
    {
        var entityTypes = PersistenceEntityTypes();
        var findings = new List<Finding>();
        var effectsCache = new Dictionary<MethodBase, SortedSet<string>>();

        foreach (var mapper in ApiAssembly.GetTypes().SelectMany(DeclaredMethods).Where(m => IsMapper(m, entityTypes)))
        {
            var called = CalledMethods(mapper);

            // Approssimazione voluta: basta che il mapper chiami ClearDomainEvents in un punto qualunque. È lo
            // schema dei mapper che ripristinano lo stato con i metodi di dominio e poi puliscono gli eventi.
            var clearsEvents = called.Any(m => string.Equals(m.Name, "ClearDomainEvents", StringComparison.Ordinal));

            foreach (var target in called.OfType<MethodInfo>())
            {
                var isFactory = IsCreationFactoryCandidate(target, entityTypes);
                if (!isFactory && !IsDomainMethod(target))
                {
                    continue;
                }

                if (!effectsCache.TryGetValue(target, out var effects))
                {
                    effects = CreationEffects(target);
                    effectsCache[target] = effects;
                }

                var unclearedEvent = effects.Contains(DomainEventEffect) && !clearsEvents;

                // Factory: l'ora corrente da sola non basta, perché un Reconstitute passa spesso dal costruttore
                // privato, che data l'oggetto ad "adesso", e poi sovrascrive la data con quella salvata. Un id nuovo,
                // invece, non ha mai senso per una riga che esiste già.
                // Ogni altro metodo di dominio chiamato dal mapper (statico o d'istanza, per esempio un metodo che
                // ripristina un figlio): conta solo l'evento lasciato sull'aggregato.
                if (isFactory ? !effects.Contains(NewIdEffect) && !unclearedEvent : !unclearedEvent)
                {
                    continue;
                }

                var key = $"{OuterType(mapper.DeclaringType!).FullName} → {target.DeclaringType!.Name}.{target.Name}";
                findings.Add(new Finding(key, mapper.Name, string.Join(", ", effects), unclearedEvent));
            }
        }

        return findings;
    }

    /// <summary>
    /// I tipi entità dal modello EF, non da una convenzione di nomi: un tipo è un'entità se il modello lo mappa.
    /// </summary>
    private static HashSet<Type> PersistenceEntityTypes()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"RepositoryReconstitutionArchitecture_{Guid.NewGuid()}")
            .Options;
        using var db = new MeepleAiDbContext(options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);

        return db.Model.GetEntityTypes()
            .Select(e => e.ClrType)
            .Where(t => !t.IsGenericType && !IsDomainType(t))
            .ToHashSet();
    }

    private static IEnumerable<MethodInfo> DeclaredMethods(Type type) =>
        type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

    private static bool IsDomainType(Type? type) =>
        type?.Namespace is { } ns &&
        (ns.Contains(".Domain.", StringComparison.Ordinal) || ns.EndsWith(".Domain", StringComparison.Ordinal));

    private static bool IsMapper(MethodInfo method, HashSet<Type> entityTypes) =>
        IsDomainType(method.ReturnType) &&
        method.ReturnType.IsClass &&
        method.GetParameters().Any(p => entityTypes.Contains(p.ParameterType));

    private static bool IsDomainMethod(MethodInfo target) =>
        target.DeclaringType is { } owner &&
        owner.Assembly == ApiAssembly &&
        IsDomainType(owner) &&
        !string.Equals(target.Name, "ClearDomainEvents", StringComparison.Ordinal);

    private static bool IsCreationFactoryCandidate(MethodInfo target, HashSet<Type> entityTypes) =>
        target.IsStatic &&
        target.DeclaringType is { } owner &&
        owner.Assembly == ApiAssembly &&
        IsDomainType(owner) &&
        owner.IsAssignableFrom(target.ReturnType) &&
        owner.GetProperty("Id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) is not null &&
        !target.GetParameters().Any(p => entityTypes.Contains(p.ParameterType));

    /// <summary>
    /// Cosa fa la factory che una riga ricostruita non deve subire. Segue i costruttori e i metodi dei tipi di
    /// dominio che la factory chiama, fino a <see cref="MaxDepth"/> livelli.
    /// </summary>
    private static SortedSet<string> CreationEffects(MethodBase factory)
    {
        var effects = new SortedSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<MethodBase>();
        var queue = new Queue<(MethodBase Method, int Depth)>();
        queue.Enqueue((factory, 0));

        while (queue.Count > 0)
        {
            var (method, depth) = queue.Dequeue();
            if (!seen.Add(method))
            {
                continue;
            }

            foreach (var target in CalledMethods(method))
            {
                var owner = target.DeclaringType;
                if (owner == typeof(Guid) && target.Name is "NewGuid" or "CreateVersion7")
                {
                    effects.Add(NewIdEffect);
                }
                else if ((owner == typeof(DateTime) || owner == typeof(DateTimeOffset)) && target.Name is "get_UtcNow" or "get_Now")
                {
                    effects.Add(CurrentTimeEffect);
                }
                else if (owner == typeof(TimeProvider) && target.Name is "GetUtcNow" or "GetLocalNow")
                {
                    effects.Add(CurrentTimeEffect);
                }
                else if (string.Equals(target.Name, "AddDomainEvent", StringComparison.Ordinal))
                {
                    effects.Add(DomainEventEffect);
                }
                else if (depth < MaxDepth && owner is not null && owner.Assembly == ApiAssembly && IsDomainType(owner))
                {
                    queue.Enqueue((target, depth + 1));
                }
            }
        }

        return effects;
    }

    /// <summary>
    /// I metodi chiamati da <paramref name="method"/>, letti dall'IL: <c>call</c>, <c>callvirt</c>, <c>newobj</c>
    /// e ogni altra istruzione con un operando metodo.
    /// </summary>
    private static List<MethodBase> CalledMethods(MethodBase method)
    {
        var called = new List<MethodBase>();
        var il = method.GetMethodBody()?.GetILAsByteArray();
        if (il is null)
        {
            return called;
        }

        var typeArguments = method.DeclaringType is { IsGenericType: true } declaring ? declaring.GetGenericArguments() : null;
        var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;

        var position = 0;
        while (position < il.Length)
        {
            OpCode code;
            if (il[position] == 0xFE)
            {
                code = TwoByteOpCodes[il[position + 1]];
                position += 2;
            }
            else
            {
                code = OneByteOpCodes[il[position]];
                position += 1;
            }

            var operandSize = code.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, position)),
                _ => 4,
            };

            if (code.OperandType == OperandType.InlineMethod)
            {
                try
                {
                    if (method.Module.ResolveMethod(BitConverter.ToInt32(il, position), typeArguments, methodArguments) is { } target)
                    {
                        called.Add(target);
                    }
                }
                catch (ArgumentException)
                {
                    // Un token che il contesto generico non risolve non è una factory di dominio.
                }
            }

            position += operandSize;
        }

        return called;
    }

    /// <summary>
    /// Il tipo che il lettore riconosce: le lambda vivono in classi annidate generate dal compilatore.
    /// </summary>
    private static Type OuterType(Type type)
    {
        var current = type;
        while (current.DeclaringType is { } outer && current.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
        {
            current = outer;
        }

        return current;
    }
}
