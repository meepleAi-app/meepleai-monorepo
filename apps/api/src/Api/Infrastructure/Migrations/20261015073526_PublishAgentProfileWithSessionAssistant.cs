using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// #4238 (A2): pubblica la versione del profilo agente con l'assistente in partita abilitato. È il passo
    /// che la fetta A2 lasciava a sé, dopo la misura del testo da pubblicare (#4270) e la correzione della
    /// storia e della lingua del turno (#4397). Una versione pubblicata non si modifica (D2): se ne pubblica
    /// una nuova con il contenuto della pubblicata più la sezione «Assistente in partita», e quella passa in
    /// archivio.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La sezione scrive <b>ogni</b> campo: un campo assente varrebbe il default del codice, che può
    /// cambiare dopo la misura. I valori sono quelli misurati e raccomandati in spec §7.1: prompt v3,
    /// temperatura 0,2, 600 token, tre giri con strumenti, scadenza di 15 secondi, nessuna riserva.
    /// </para>
    /// <para>
    /// Scatta solo se la versione pubblicata non ha già l'assistente abilitato. Una bozza esistente senza la
    /// sezione la riceve anch'essa: la pagina admin la conserva dalla bozza, e pubblicarla senza spegnerebbe
    /// l'assistente in silenzio. Il tool calling del modello principale non si controlla qui: il catalogo lo
    /// scrive il seed dei modelli, che gira dopo le migrazioni. Lo ricontrolla ogni turno
    /// (<c>SessionAssistantAvailability</c>), che risponde <c>assistant_model_without_tools</c>.
    /// </para>
    /// </remarks>
    public partial class PublishAgentProfileWithSessionAssistant : Migration
    {
        private const string ProfileId = "a9e7f5c1-4167-4095-9a1e-000000000001";
        private const string VersionId = "a9e7f5c1-4167-4095-9a1e-000000000103";

        // Il prompt v3 di #4270, copiato qui: una migrazione non cambia quando cambia il codice. Un test lo
        // confronta con tests/llm-eval/session-assistant/prompts/v3.json, il testo misurato.
        private const string ItalianPrompt = """
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

        private const string EnglishPrompt = """
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

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var section = SectionLiteral();
            migrationBuilder.Sql(
                $$"""
                WITH published AS (
                    SELECT id, agent_profile_id, content
                    FROM knowledge_base.agent_profile_versions
                    WHERE agent_profile_id = '{{ProfileId}}'
                      AND status = 1
                      AND COALESCE((content->'sessionAssistant'->>'enabled')::boolean, false) = false
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
                    a.content || jsonb_build_object('sessionAssistant', {{section}}),
                    NOW(),
                    NOW()
                FROM archived a;

                UPDATE knowledge_base.agent_profile_versions
                SET content = content || jsonb_build_object('sessionAssistant', {{section}})
                WHERE agent_profile_id = '{{ProfileId}}'
                  AND status = 0
                  AND content->'sessionAssistant' IS NULL;

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
            // Toglie la sezione dalla bozza solo se è ancora quella scritta da Up, poi ripristina la versione
            // archiviata da Up, solo se la nuova è ancora quella pubblicata.
            var section = SectionLiteral();
            migrationBuilder.Sql(
                $$"""
                UPDATE knowledge_base.agent_profile_versions
                SET content = content - 'sessionAssistant'
                WHERE agent_profile_id = '{{ProfileId}}'
                  AND status = 0
                  AND content->'sessionAssistant' = {{section}};

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
        /// La sezione come letterale jsonb. Il serializzatore codifica apici e caratteri non ASCII come
        /// sequenze \u, che jsonb decodifica; il raddoppio degli apici resta come difesa.
        /// </summary>
        private static string SectionLiteral()
        {
            var json = JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["enabled"] = true,
                ["systemPrompts"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["it"] = ItalianPrompt,
                    ["en"] = EnglishPrompt,
                },
                ["temperature"] = 0.2m,
                ["maxResponseTokens"] = 600,
                ["maxToolIterations"] = 3,
                ["turnTimeoutSeconds"] = 15,
                ["useReserveModel"] = false,
            });
            return $"'{json.Replace("'", "''", StringComparison.Ordinal)}'::jsonb";
        }
    }
}
