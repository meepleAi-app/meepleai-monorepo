using System.Reflection;

namespace Api.Filters;

/// <summary>
/// #4063 — una data senza designatore di fuso, ricevuta come argomento di un endpoint, si intende
/// <b>UTC</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>La regola.</b> <c>2026-01-01</c> e <c>2026-01-01T10:00:00</c> valgono
/// <c>2026-01-01T00:00:00Z</c> e <c>2026-01-01T10:00:00Z</c>: l'orologio scritto resta quello, e il
/// fuso è UTC. Le forme con <c>Z</c> o con un offset non cambiano (il binding le porta già a UTC).
/// Un valore che non è una data resta un 400 del binding: questo filtro gira <i>dopo</i> il binding,
/// quindi non vede mai un valore rifiutato. È la stessa lettura che i siti corretti uno per uno
/// avevano già scelto (<c>RagExecutionDateWindow</c>, <c>GetLowQualityResponsesQueryHandler</c>,
/// <c>QualityReportService</c>), resa valida per tutti.
/// </para>
/// <para>
/// <b>Il difetto.</b> Il binding delle minimal API legge un <see cref="DateTime"/> con
/// <c>DateTimeStyles.AdjustToUniversal</c>: senza designatore il valore resta
/// <see cref="DateTimeKind.Unspecified"/>, e Npgsql rifiuta un <c>Unspecified</c> su una colonna
/// <c>timestamp with time zone</c>. L'<see cref="ArgumentException"/> diventa un 400 «Invalid
/// request parameters» che sembra un parametro rifiutato ed è una query fallita. Dieci endpoint
/// misurati, tre user-facing; corretto prima caso per caso (#255, #4064), mai come classe.
/// </para>
/// <para>
/// <b>Perché qui e non altrove.</b> Il punto in cui nasce l'ambiguità è il binding, e le minimal API
/// non permettono di sostituire il parser di un tipo primitivo. Un filtro di endpoint sul gruppo
/// <c>/api/v1</c> è il primo punto dopo il binding che vale per tutti gli endpoint del gruppo,
/// presenti e futuri. L'alternativa scartata è una conversione EF su ogni <c>DateTime</c>: cambierebbe
/// come viene scritto ogni valore del modello, non solo quelli arrivati da una richiesta.
/// </para>
/// <para>
/// <b>Cosa copre.</b> Ogni argomento dell'handler di tipo <see cref="DateTime"/> o
/// <c>DateTime?</c> (query, route, header), e le proprietà <see cref="DateTime"/> scrivibili di un
/// argomento <c>[AsParameters]</c>. <b>Cosa non copre</b>: le date dentro un corpo JSON (sono
/// proprietà di un DTO, non argomenti) e le collezioni di date. Un argomento che il filtro non sa
/// normalizzare — un array, una proprietà senza setter — non viene ignorato in silenzio:
/// <see cref="FindSites"/> lo riporta con <c>IsNormalized = false</c>, e il test d'inventario
/// <c>QueryDateWithoutOffsetIntegrationTests</c> fallisce.
/// </para>
/// <para>
/// <b>Costo.</b> Sugli endpoint senza argomenti data la factory restituisce <c>next</c> stesso, e
/// RequestDelegateFactory in quel caso salta la pipeline dei filtri: nessun costo per richiesta.
/// </para>
/// </remarks>
internal static class UtcDateTimeArguments
{
    /// <summary>
    /// Applica la regola a ogni endpoint costruito da <paramref name="builder"/>, e lo marca con
    /// <see cref="UtcDateTimeArgumentsMetadata"/> perché il test d'inventario possa verificarlo.
    /// </summary>
    public static TBuilder WithUtcDateTimeArguments<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.WithMetadata(UtcDateTimeArgumentsMetadata.Instance);
        builder.AddEndpointFilterFactory(CreateFilter);
        return builder;
    }

    /// <summary>Gli argomenti data dell'handler, e se il filtro sa normalizzarli.</summary>
    internal static IReadOnlyList<DateTimeArgumentSite> FindSites(MethodInfo method) =>
        DateTimeArgumentPlan.For(method).Sites;

    /// <summary>
    /// La regola su un valore: <see cref="DateTimeKind.Unspecified"/> diventa UTC con lo stesso
    /// orologio; <see cref="DateTimeKind.Local"/> viene convertito, perché nomina già un istante.
    /// </summary>
    internal static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => value,
    };

    internal static EndpointFilterDelegate CreateFilter(
        EndpointFilterFactoryContext context,
        EndpointFilterDelegate next)
    {
        var plan = DateTimeArgumentPlan.For(context.MethodInfo);
        if (plan.IsEmpty)
        {
            return next;
        }

        return invocationContext =>
        {
            plan.Apply(invocationContext.Arguments);
            return next(invocationContext);
        };
    }
}

/// <summary>
/// Marca un endpoint i cui argomenti <see cref="DateTime"/> passano da
/// <see cref="UtcDateTimeArguments"/>. Lo legge il test d'inventario, non il codice di produzione.
/// </summary>
internal sealed class UtcDateTimeArgumentsMetadata
{
    public static readonly UtcDateTimeArgumentsMetadata Instance = new();

    private UtcDateTimeArgumentsMetadata()
    {
    }
}

/// <summary>
/// Un argomento data di un handler. <paramref name="Property"/> è valorizzata per una proprietà di un
/// argomento <c>[AsParameters]</c>.
/// </summary>
internal sealed record DateTimeArgumentSite(string Parameter, string? Property, bool IsNormalized);

/// <summary>
/// Calcolato una volta per endpoint, alla costruzione: quali posizioni degli argomenti normalizzare.
/// </summary>
internal sealed class DateTimeArgumentPlan
{
    private readonly int[] _direct;
    private readonly (int Index, PropertyInfo[] Properties)[] _surrogates;

    private DateTimeArgumentPlan(
        int[] direct,
        (int Index, PropertyInfo[] Properties)[] surrogates,
        IReadOnlyList<DateTimeArgumentSite> sites)
    {
        _direct = direct;
        _surrogates = surrogates;
        Sites = sites;
    }

    public IReadOnlyList<DateTimeArgumentSite> Sites { get; }

    public bool IsEmpty => _direct.Length == 0 && _surrogates.Length == 0;

    public static DateTimeArgumentPlan For(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);

        var direct = new List<int>();
        var surrogates = new List<(int, PropertyInfo[])>();
        var sites = new List<DateTimeArgumentSite>();

        var parameters = method.GetParameters();
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            var name = parameter.Name ?? $"#{i}";

            if (IsDateTime(parameter.ParameterType))
            {
                direct.Add(i);
                sites.Add(new DateTimeArgumentSite(name, null, IsNormalized: true));
            }
            else if (IsDateTimeCollection(parameter.ParameterType))
            {
                sites.Add(new DateTimeArgumentSite(name, null, IsNormalized: false));
            }
            else if (parameter.IsDefined(typeof(AsParametersAttribute), inherit: false))
            {
                var writable = new List<PropertyInfo>();
                foreach (var property in parameter.ParameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (IsDateTime(property.PropertyType))
                    {
                        var canWrite = property.SetMethod is not null;
                        if (canWrite)
                        {
                            writable.Add(property);
                        }

                        sites.Add(new DateTimeArgumentSite(name, property.Name, canWrite));
                    }
                    else if (IsDateTimeCollection(property.PropertyType))
                    {
                        sites.Add(new DateTimeArgumentSite(name, property.Name, IsNormalized: false));
                    }
                }

                if (writable.Count > 0)
                {
                    surrogates.Add((i, writable.ToArray()));
                }
            }
        }

        return new DateTimeArgumentPlan(direct.ToArray(), surrogates.ToArray(), sites);
    }

    public void Apply(IList<object?> arguments)
    {
        foreach (var index in _direct)
        {
            if (arguments[index] is DateTime value && value.Kind != DateTimeKind.Utc)
            {
                arguments[index] = UtcDateTimeArguments.AsUtc(value);
            }
        }

        foreach (var (index, properties) in _surrogates)
        {
            var surrogate = arguments[index];
            if (surrogate is null)
            {
                continue;
            }

            var changed = false;
            foreach (var property in properties)
            {
                if (property.GetValue(surrogate) is DateTime value && value.Kind != DateTimeKind.Utc)
                {
                    property.SetValue(surrogate, UtcDateTimeArguments.AsUtc(value));
                    changed = true;
                }
            }

            // Riscrivere l'argomento serve a un [AsParameters] di tipo valore: il getter del contesto
            // restituisce una copia inscatolata, e la modifica resterebbe su quella copia.
            if (changed)
            {
                arguments[index] = surrogate;
            }
        }
    }

    private static bool IsDateTime(Type type) => type == typeof(DateTime) || type == typeof(DateTime?);

    private static bool IsDateTimeCollection(Type type) =>
        type.IsArray && type.GetElementType() is { } element && IsDateTime(element);
}
