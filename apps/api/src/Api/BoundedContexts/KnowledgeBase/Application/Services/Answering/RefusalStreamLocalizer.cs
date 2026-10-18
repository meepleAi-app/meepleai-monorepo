using System.Text;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// #3850: la rinuncia sullo stream. Il modello la scrive in inglese e a pezzi («This», « information»,
/// …); l'utente deve leggerla nella lingua della risposta, senza vedere prima la frase inglese.
/// </summary>
/// <remarks>
/// <para>
/// Ogni pezzo passa da <see cref="Push"/>, che restituisce ciò che si può mostrare subito. Si trattiene
/// soltanto la coda del testo che potrebbe ancora diventare la frase inglese (a partire da un inizio
/// di parola). Appena il testo se ne discosta, ciò che era trattenuto esce tutto insieme; quando la
/// frase è completa, esce il testo localizzato al suo posto. A fine stream <see cref="Complete"/>
/// rilascia ciò che resta: un inizio di frase mai completato esce com'era, non si perde.
/// </para>
/// <para>
/// In una risposta in inglese, o in una lingua senza testo, ogni pezzo esce appena arriva.
/// </para>
/// <para>
/// <see cref="Delivered"/> è il testo che l'utente ha ricevuto: è quello da salvare (cache, chat) e su
/// cui calcolare le posizioni delle citazioni in linea. I token da conteggiare restano quelli del
/// modello.
/// </para>
/// </remarks>
internal sealed class RefusalStreamLocalizer
{
    /// <summary>La frase nella lingua della risposta; <c>null</c> se non c'è niente da sostituire.</summary>
    private readonly string? _replacement;
    private readonly StringBuilder _delivered = new();
    private string _held = string.Empty;

    internal RefusalStreamLocalizer(string? language)
    {
        var sentence = RulebookRefusal.SentenceFor(language);
        _replacement = string.Equals(sentence, RulebookRefusal.EnglishSentence, StringComparison.Ordinal)
            ? null
            : sentence;
    }

    /// <summary>Tutto ciò che è uscito finora, cioè ciò che l'utente ha letto.</summary>
    internal string Delivered => _delivered.ToString();

    /// <summary>Un pezzo del modello; ritorna ciò che si può mostrare adesso, anche vuoto.</summary>
    internal string Push(string chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);

        if (_replacement is null)
        {
            _delivered.Append(chunk);
            return chunk;
        }

        return Release(_held + chunk, textIsComplete: false);
    }

    /// <summary>Lo stream è finito: ritorna ciò che era ancora trattenuto, localizzato se è la frase.</summary>
    internal string Complete() =>
        _replacement is null || _held.Length == 0
            ? string.Empty
            : Release(_held, textIsComplete: true);

    private string Release(string text, bool textIsComplete)
    {
        _held = string.Empty;
        var output = new StringBuilder(text.Length);
        var cursor = 0;
        while (true)
        {
            var (position, kind, end) = RulebookRefusal.FindEnglish(text, cursor, textIsComplete);
            output.Append(text, cursor, position - cursor);
            if (kind == SentenceMatch.None)
            {
                break;
            }

            if (kind == SentenceMatch.Partial)
            {
                _held = text[position..];
                break;
            }

            output.Append(_replacement);
            cursor = end;
        }

        var released = output.ToString();
        _delivered.Append(released);
        return released;
    }
}
