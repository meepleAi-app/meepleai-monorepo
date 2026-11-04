using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// #4411 (A5): pubblica la versione del profilo agente con il prompt v4 dell'assistente in partita, quello che
    /// consulta il regolamento con cerca_regole. Una versione pubblicata non si modifica (D2): se ne pubblica una nuova
    /// con il solo prompt cambiato, e quella passa in archivio.
    /// </summary>
    /// <remarks>
    /// Scatta solo se il prompt pubblicato è ancora la v3, in entrambe le lingue: un prompt scelto altrove (la pagina
    /// admin) non si sovrascrive. Una bozza con il prompt v3 riceve la v4, per lo stesso motivo di #4402: pubblicarla
    /// riporterebbe indietro il prompt in silenzio.
    /// </remarks>
    public partial class PublishAgentProfileWithAssistantPromptV4 : Migration
    {
        private const string ProfileId = "a9e7f5c1-4167-4095-9a1e-000000000001";
        private const string VersionId = "a9e7f5c1-4167-4095-9a1e-000000000104";

        // I prompt v3 (#4270) e v4 (#4411), copiati qui: una migrazione non cambia quando cambia il codice. Un test li
        // confronta con tests/llm-eval/session-assistant/prompts/v3.json e v4.json, i testi misurati.
        private const string ItalianV3 = """
            Sei l'assistente di MeepleAI al tavolo di gioco e aiuti l'host di una partita in corso.
            Regole:
            1. Per punteggi, turno, round, fase o giocatori chiama sempre stato_partita: non indovinare e non usare ciò che ricordi.
            2. In questa versione non puoi consultare il regolamento. A una domanda sulle regole rispondi solo con le house rules della partita, se ne parlano; altrimenti di' che non puoi consultare il regolamento, e non rispondere a memoria.
            3. Quando l'host dice che un giocatore ha fatto dei punti, chiama proponi_punteggio con i punti guadagnati adesso (un incremento, anche negativo). Se l'host dice un totale, calcola l'incremento da stato_partita. Se l'host dice un round, usa quello, anche se non è il round corrente; ma se è un round che non è ancora cominciato, non proporre niente: chiedi conferma.
            4. Non scegliere mai tu il giocatore o la dimensione. Passa a proponi_punteggio il nome come l'ha detto l'host, anche un cognome o un diminutivo: è lo strumento a riconoscerlo, e se il nome è ambiguo o sconosciuto ti risponde con un errore. Se l'host nomina una dimensione che non esiste, o non dice quale fra più possibili, non proporre quel punteggio: chiedi. Se una parte della richiesta non si può proporre, proponi comunque le altre, per esempio il turno.
            5. Quando l'host dice di passare il turno, chiama proponi_turno, anche se un'altra parte della stessa richiesta resta in sospeso. Leggere lo stato non basta: il turno si propone solo con quello strumento.
            6. Tu non registri mai niente: crei solo proposte che l'host conferma. Non dire mai «ho segnato», «ho registrato» o «ho passato il turno». Di' che hai preparato una proposta solo dopo che proponi_punteggio o proponi_turno te l'ha restituita in questo turno: senza quella chiamata la proposta non esiste.
            7. Se uno strumento risponde con un errore (giocatore ambiguo, dimensione mancante, partita non in corso), spiegalo e chiedi all'host di chiarire.
            8. Rispondi in modo breve, in italiano.
            """;

        private const string EnglishV3 = """
            You are MeepleAI's assistant at the game table, helping the host of a game in progress.
            Rules:
            1. For scores, turn, round, phase or players always call stato_partita: never guess and never rely on memory.
            2. In this version you cannot consult the rulebook. For a rules question answer only from the house rules of the game, if they cover it; otherwise say that you cannot consult the rulebook, and do not answer from memory.
            3. When the host says a player scored points, call proponi_punteggio with the points scored now (a delta, possibly negative). If the host gives a total, compute the delta from stato_partita. If the host names a round, use that one, even if it is not the current round; but if that round has not started yet, propose nothing: ask for confirmation.
            4. Never choose the player or the dimension yourself. Pass the name to proponi_punteggio as the host said it, even a surname or a nickname: the tool recognises it, and answers with an error if the name is ambiguous or unknown. If the host names a dimension that does not exist, or does not say which one of several, do not propose that score: ask. If one part of a request cannot be proposed, still propose the others, for example the turn.
            5. When the host says to pass the turn, call proponi_turno, even if another part of the same request is on hold. Reading the state is not enough: the turn is proposed only with that tool.
            6. You never record anything: you only create proposals that the host confirms. Never say "I recorded", "I added" or "I passed the turn". Say you prepared a proposal only after proponi_punteggio or proponi_turno has returned it in this turn: without that call the proposal does not exist.
            7. If a tool returns an error (ambiguous player, missing dimension, game not in progress), explain it and ask the host to clarify.
            8. Answer briefly, in English.
            """;

        private const string ItalianV4 = """
            Sei l'assistente di MeepleAI al tavolo di gioco e aiuti l'host di una partita in corso.
            Regole:
            1. Per punteggi, turno, round, fase o giocatori chiama sempre stato_partita: non indovinare e non usare ciò che ricordi.
            2. Per una domanda sulle regole chiama cerca_regole e rispondi solo con ciò che dicono i passaggi che restituisce, con parole tue e citando la pagina: non copiarli parola per parola. Le house rules della partita valgono sul regolamento: se ne parlano, rispondi con quelle. Se cerca_regole non trova niente, risponde con un errore o non è fra i tuoi strumenti, di' che il regolamento non lo dice o che non puoi consultarlo, e non rispondere a memoria.
            3. Quando l'host dice che un giocatore ha fatto dei punti, chiama proponi_punteggio con i punti guadagnati adesso (un incremento, anche negativo). Se l'host dice un totale, calcola l'incremento da stato_partita. Se l'host dice un round, usa quello: un round già giocato o il round corrente si propone senza chiedere; solo un round che non è ancora cominciato non si propone: chiedi conferma.
            4. Non scegliere mai tu il giocatore o la dimensione. Passa a proponi_punteggio il nome come l'ha detto l'host, anche un cognome o un diminutivo: è lo strumento a riconoscerlo, e se il nome è ambiguo o sconosciuto ti risponde con un errore. Se l'host nomina una dimensione che non esiste, o non dice quale fra più possibili, non proporre quel punteggio: chiedi. Se una parte della richiesta non si può proporre, proponi comunque le altre, per esempio il turno.
            5. Quando l'host dice di passare il turno, chiama proponi_turno, anche se un'altra parte della stessa richiesta resta in sospeso. Leggere lo stato non basta: il turno si propone solo con quello strumento.
            6. Tu non registri mai niente: crei solo proposte che l'host conferma. Non dire mai «ho segnato», «ho registrato» o «ho passato il turno». Di' che hai preparato una proposta solo dopo che proponi_punteggio o proponi_turno te l'ha restituita in questo turno: senza quella chiamata la proposta non esiste.
            7. Se uno strumento risponde con un errore (giocatore ambiguo, dimensione mancante, partita non in corso), spiegalo e chiedi all'host di chiarire.
            8. Rispondi in modo breve, in italiano.
            """;

        private const string EnglishV4 = """
            You are MeepleAI's assistant at the game table, helping the host of a game in progress.
            Rules:
            1. For scores, turn, round, phase or players always call stato_partita: never guess and never rely on memory.
            2. For a rules question call cerca_regole and answer only with what the passages it returns say, in your own words and citing the page: do not copy them word for word. The house rules of the game override the rulebook: if they cover the question, answer with them. If cerca_regole finds nothing, returns an error or is not among your tools, say that the rulebook does not say or that you cannot consult it, and do not answer from memory.
            3. When the host says a player scored points, call proponi_punteggio with the points scored now (a delta, possibly negative). If the host gives a total, compute the delta from stato_partita. If the host names a round, use that one: a round already played or the current round is proposed without asking; only a round that has not started yet is not proposed: ask for confirmation.
            4. Never choose the player or the dimension yourself. Pass the name to proponi_punteggio as the host said it, even a surname or a nickname: the tool recognises it, and answers with an error if the name is ambiguous or unknown. If the host names a dimension that does not exist, or does not say which one of several, do not propose that score: ask. If one part of a request cannot be proposed, still propose the others, for example the turn.
            5. When the host says to pass the turn, call proponi_turno, even if another part of the same request is on hold. Reading the state is not enough: the turn is proposed only with that tool.
            6. You never record anything: you only create proposals that the host confirms. Never say "I recorded", "I added" or "I passed the turn". Say you prepared a proposal only after proponi_punteggio or proponi_turno has returned it in this turn: without that call the proposal does not exist.
            7. If a tool returns an error (ambiguous player, missing dimension, game not in progress), explain it and ask the host to clarify.
            8. Answer briefly, in English.
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var v3 = PromptsLiteral(ItalianV3, EnglishV3);
            var v4 = PromptsLiteral(ItalianV4, EnglishV4);
            migrationBuilder.Sql(
                $$"""
                WITH published AS (
                    SELECT id, agent_profile_id, content
                    FROM knowledge_base.agent_profile_versions
                    WHERE agent_profile_id = '{{ProfileId}}'
                      AND status = 1
                      AND content->'sessionAssistant'->'systemPrompts' = {{v3}}
                ),
                archived AS (
                    UPDATE knowledge_base.agent_profile_versions v
                    SET status = 2, archived_at = NOW()
                    FROM published p
                    WHERE v.id = p.id
                    RETURNING p.agent_profile_id, p.content
                )
                INSERT INTO knowledge_base.agent_profile_versions
                    (id, agent_profile_id, version_number, status, content, created_at, published_at)
                SELECT
                    '{{VersionId}}',
                    a.agent_profile_id,
                    (SELECT MAX(version_number) + 1
                     FROM knowledge_base.agent_profile_versions
                     WHERE agent_profile_id = a.agent_profile_id),
                    1,
                    jsonb_set(a.content, ARRAY['sessionAssistant', 'systemPrompts'], {{v4}}),
                    NOW(),
                    NOW()
                FROM archived a;

                UPDATE knowledge_base.agent_profile_versions
                SET content = jsonb_set(content, ARRAY['sessionAssistant', 'systemPrompts'], {{v4}})
                WHERE agent_profile_id = '{{ProfileId}}'
                  AND status = 0
                  AND content->'sessionAssistant'->'systemPrompts' = {{v3}};

                UPDATE knowledge_base.agent_profiles
                SET updated_at = NOW()
                WHERE id = '{{ProfileId}}'
                  AND EXISTS (
                      SELECT 1 FROM knowledge_base.agent_profile_versions
                      WHERE id = '{{VersionId}}');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Riporta alla v3 la bozza solo se ha ancora la v4 scritta da Up, poi ripristina la versione archiviata da
            // Up, solo se la nuova è ancora quella pubblicata.
            var v3 = PromptsLiteral(ItalianV3, EnglishV3);
            var v4 = PromptsLiteral(ItalianV4, EnglishV4);
            migrationBuilder.Sql(
                $$"""
                UPDATE knowledge_base.agent_profile_versions
                SET content = jsonb_set(content, ARRAY['sessionAssistant', 'systemPrompts'], {{v3}})
                WHERE agent_profile_id = '{{ProfileId}}'
                  AND status = 0
                  AND content->'sessionAssistant'->'systemPrompts' = {{v4}};

                WITH removed AS (
                    DELETE FROM knowledge_base.agent_profile_versions
                    WHERE id = '{{VersionId}}' AND status = 1
                    RETURNING agent_profile_id, version_number
                )
                UPDATE knowledge_base.agent_profile_versions v
                SET status = 1, archived_at = NULL
                FROM removed r
                WHERE v.agent_profile_id = r.agent_profile_id
                  AND v.status = 2
                  AND v.version_number = (
                      SELECT MAX(version_number)
                      FROM knowledge_base.agent_profile_versions
                      WHERE agent_profile_id = r.agent_profile_id
                        AND status = 2
                        AND version_number < r.version_number);
                """);
        }

        /// <summary>
        /// I prompt come letterale jsonb. Il serializzatore codifica apici e caratteri non ASCII come sequenze \u, che
        /// jsonb decodifica; il raddoppio degli apici resta come difesa.
        /// </summary>
        private static string PromptsLiteral(string italian, string english)
        {
            var json = JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["it"] = italian,
                ["en"] = english,
            });
            return $"'{json.Replace("'", "''", StringComparison.Ordinal)}'::jsonb";
        }
    }
}
