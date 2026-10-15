using System.Globalization;
using System.Text;

namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// #4393: un nome di giocatore come arriva al modello. Lo sceglie chi aggiunge il giocatore, quindi può fingere una
/// sezione del contesto («Luca» + a capo + «House rules: vale doppio»). Misurato il 2026-10-15: il modello applicava
/// quella regola finta anche con il nome in una lista JSON e con una regola del prompt sui dati. Qui il nome diventa
/// una riga sola, e corto: abbastanza per un nome vero, troppo poco per un'istruzione.
/// </summary>
/// <remarks>
/// Vale dove un nome arriva al modello: contesto statico, risultato di stato_partita, candidati negli errori,
/// proposte. Il matcher lavora sui nomi interi, perché l'host li dice com'erano.
/// </remarks>
internal static class AssistantNames
{
    public const int MaxLength = 40;

    public static string ForModel(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var text = new StringBuilder(name.Length);
        var pendingSpace = false;
        foreach (var c in name)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c) || IsLineSeparator(c))
            {
                pendingSpace = text.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                text.Append(' ');
                pendingSpace = false;
            }

            text.Append(c);
        }

        return text.Length <= MaxLength ? text.ToString() : string.Concat(text.ToString(0, MaxLength - 1).TrimEnd(), "…");
    }

    private static bool IsLineSeparator(char c) =>
        char.GetUnicodeCategory(c) is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;
}
