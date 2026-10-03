namespace Api.Tests.Architecture;

/// <summary>
/// Scanner di sorgenti C# condiviso dai gate di architettura che devono guardare il TESTO del codice
/// e non i metadati.
/// </summary>
/// <remarks>
/// <para>
/// Esiste perché per alcuni invarianti la riflessione non basta: un invariante su ciò che una
/// chiamata PASSA come argomento — il motivo di un <c>Assert.Skip</c>, il nome di un
/// <c>AddHttpClient</c> — vive dentro un corpo di metodo, dove <c>GetCustomAttributesData()</c> non
/// arriva. È il limite che rende inapplicabile il modello di
/// <see cref="TestCategoryGateArchitectureTests"/> a quella famiglia di gate.
/// </para>
/// <para>
/// I tre metodi vengono da <see cref="EgressHttpClientPinArchitectureTests"/> (#3495 finding H7), che
/// li aveva come membri privati. Estratti qui quando il secondo gate della stessa famiglia ha avuto
/// bisogno degli stessi: verificato prima di spostarli che dipendessero solo l'uno dall'altro e non
/// dal resto di quella classe.
/// </para>
/// <para>
/// 🔴 La proprietà che li rende utili, e che va preservata in ogni modifica: <see cref="SkipTrivia"/>
/// salta commenti e letterali, quindi una stringa NOMINATA in un commento non viene contata come una
/// chiamata. Senza quella proprietà un gate denuncia il proprio commento esplicativo — è accaduto
/// davvero al gate dei workflow in #4019 — e, peggio, conta come «sito da classificare» gli esempi
/// dentro la documentazione XML.
/// </para>
/// </remarks>
internal static class SourceScanner
{
    internal static string ReadStatement(string text, int start)
    {
        var code = new System.Text.StringBuilder();
        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            var skipped = SkipTrivia(text, i);
            if (skipped != i)
            {
                // Comment or literal: keep a separator so adjacent identifiers don't fuse.
                code.Append(' ');
                i = skipped - 1;
                continue;
            }

            var c = text[i];
            if (c is '(' or '{' or '[')
            {
                depth++;
            }
            else if (c is ')' or '}' or ']')
            {
                depth--;
            }
            else if (c == ';' && depth <= 0)
            {
                break;
            }

            code.Append(c);
        }

        return code.ToString();
    }

    /// <summary>
    /// Offsets of <paramref name="needle"/> that sit in real code — occurrences inside comments,
    /// string literals or char literals are ignored (the codebase mentions AddHttpClient in prose).
    /// </summary>
    internal static List<int> FindCodeOccurrences(string text, string needle)
    {
        var found = new List<int>();
        for (var i = 0; i < text.Length; i++)
        {
            var skipped = SkipTrivia(text, i);
            if (skipped != i)
            {
                i = skipped - 1;
                continue;
            }

            if (string.CompareOrdinal(text, i, needle, 0, needle.Length) == 0)
            {
                found.Add(i);
                i += needle.Length - 1;
            }
        }

        return found;
    }

    /// <summary>
    /// If <paramref name="index"/> starts a comment or a string/char literal, returns the offset just
    /// past it; otherwise returns <paramref name="index"/> unchanged.
    /// </summary>
    internal static int SkipTrivia(string text, int index)
    {
        if (index + 1 < text.Length && text[index] == '/' && text[index + 1] == '/')
        {
            var end = text.IndexOf('\n', index);
            return end < 0 ? text.Length : end + 1;
        }

        if (index + 1 < text.Length && text[index] == '/' && text[index + 1] == '*')
        {
            var end = text.IndexOf("*/", index + 2, StringComparison.Ordinal);
            return end < 0 ? text.Length : end + 2;
        }

        if (index + 1 < text.Length && text[index] == '@' && text[index + 1] == '"')
        {
            var i = index + 2;
            while (i < text.Length)
            {
                if (text[i] == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        i += 2;
                        continue;
                    }

                    return i + 1;
                }

                i++;
            }

            return text.Length;
        }

        if (text[index] is '"' or '\'')
        {
            var quote = text[index];
            var i = index + 1;
            while (i < text.Length)
            {
                if (text[i] == '\\')
                {
                    i += 2;
                    continue;
                }

                if (text[i] == quote)
                {
                    return i + 1;
                }

                if (text[i] == '\n')
                {
                    // Unterminated on this line — treat as ordinary text rather than swallowing the file.
                    return index;
                }

                i++;
            }

            return index;
        }

        return index;
    }
}
