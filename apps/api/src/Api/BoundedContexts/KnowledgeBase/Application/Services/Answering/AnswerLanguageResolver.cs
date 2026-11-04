using System.Net.Http.Headers;
using Api.Services;

#pragma warning disable MA0048 // File name must match type name - resolver and its result
namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// #4168: la lingua di una risposta, che guida il recupero e la scelta del prompt.
/// <list type="number">
/// <item>la lingua rilevata nel testo della domanda, se il rilevamento è sicuro;</item>
/// <item>altrimenti — una domanda breve, o che non separa bene due lingue — la lingua chiesta dal
/// client, se la richiesta ne porta una supportata;</item>
/// <item>altrimenti la prima lingua supportata di <c>Accept-Language</c>;</item>
/// <item>altrimenti quella rilevata, che su un testo senza segnali è l'inglese.</item>
/// </list>
/// <para>
/// #4480: la sicurezza del rilevamento conta in un punto solo, quando la lingua rilevata è diversa da
/// quella che l'interfaccia chiede: se coincidono, o se l'interfaccia non ne chiede nessuna, la risposta
/// è nella lingua rilevata comunque. Per questo «sicuro» è un requisito alto
/// (<see cref="ILanguageDetectionService.Detect"/>), e il margine si misura proprio sulla lingua
/// dell'interfaccia: è quella che il rilevamento deve smentire. Con un rilevamento solo «probabile»
/// domande italiane con <c>Accept-Language: it</c> avevano risposta in spagnolo o in francese; misurando
/// il margine sulla seconda lingua, domande italiane brevi con l'interfaccia in inglese («Quando si usa
/// il Ladro?») avevano risposta in inglese. Una domanda chiaramente inglese con l'interfaccia in
/// italiano ha ancora risposta in inglese.
/// </para>
/// </summary>
internal static class AnswerLanguageResolver
{
    internal static AnswerLanguage Resolve(
        ILanguageDetectionService detector, string question, string? acceptLanguage, string? requestedLanguage = null)
    {
        ArgumentNullException.ThrowIfNull(detector);

        // La lingua dell'interfaccia: quella chiesta dal client, altrimenti Accept-Language.
        var asked = AskedLanguage(detector, acceptLanguage, requestedLanguage);

        var detection = detector.Detect(question ?? string.Empty, asked?.Language);
        if (detection.IsConfident)
        {
            return new AnswerLanguage(detection.Language, AnswerLanguageSource.Detected);
        }

        return asked ?? new AnswerLanguage(detection.Language, AnswerLanguageSource.DetectedUncertain);
    }

    private static AnswerLanguage? AskedLanguage(
        ILanguageDetectionService detector, string? acceptLanguage, string? requestedLanguage)
    {
        var requested = requestedLanguage?.Trim().ToLowerInvariant();
        if (requested is not null && detector.IsSupportedLanguage(requested))
        {
            return new AnswerLanguage(requested, AnswerLanguageSource.Requested);
        }

        var preferred = FirstSupportedLanguage(detector, acceptLanguage);
        return preferred is not null ? new AnswerLanguage(preferred, AnswerLanguageSource.AcceptLanguage) : null;
    }

    /// <summary>
    /// La lingua con il peso più alto fra quelle supportate; a parità di peso, la prima. «it-IT» vale «it».
    /// Un header malformato vale come assente.
    /// </summary>
    internal static string? FirstSupportedLanguage(ILanguageDetectionService detector, string? acceptLanguage)
    {
        if (string.IsNullOrWhiteSpace(acceptLanguage))
        {
            return null;
        }

        var candidates = new List<(string Language, double Quality, int Order)>();
        var order = 0;
        foreach (var part in acceptLanguage.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!StringWithQualityHeaderValue.TryParse(part, out var value) || value.Quality is <= 0)
            {
                continue;
            }

            var language = value.Value.Split('-', 2)[0].Trim().ToLowerInvariant();
            if (detector.IsSupportedLanguage(language))
            {
                candidates.Add((language, value.Quality ?? 1.0, order++));
            }
        }

        return candidates
            .OrderByDescending(c => c.Quality)
            .ThenBy(c => c.Order)
            .Select(c => c.Language)
            .FirstOrDefault();
    }
}

/// <summary>Da dove viene la lingua della risposta.</summary>
internal enum AnswerLanguageSource
{
    /// <summary>Rilevata con sicurezza nel testo della domanda.</summary>
    Detected,

    /// <summary>La domanda non bastava: la lingua chiesta esplicitamente dal client.</summary>
    Requested,

    /// <summary>La domanda non bastava: la prima lingua supportata di <c>Accept-Language</c>.</summary>
    AcceptLanguage,

    /// <summary>Né l'uno né l'altro: il default del rilevatore.</summary>
    DetectedUncertain,
}

/// <summary>La lingua di una risposta (ISO 639-1) e da dove viene.</summary>
internal sealed record AnswerLanguage(string Language, AnswerLanguageSource Source);
