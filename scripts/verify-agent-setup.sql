-- ============================================================================
-- Verification Script: Default POC Agent RAG Setup
-- ============================================================================
-- Verifies the complete POC agent configuration is ready for RAG testing
-- ============================================================================

\echo '========================================================================'
\echo 'POC Agent RAG Setup Verification'
\echo '========================================================================'
\echo ''

-- Check 1: Agent exists and is active
\echo '✓ Check 1: Agent Creation'
SELECT
    "Id",
    "Name",
    "Type",
    "StrategyName",
    "IsActive",
    "InvocationCount"
FROM agents
WHERE "Name" = 'MeepleAssistant POC';

\echo ''
\echo '✓ Check 2: Agent Configuration'
SELECT
    ac.id,
    ac.llm_model,
    ac.agent_mode,
    ac.temperature,
    ac.max_tokens,
    ac.is_current,
    LENGTH(ac.system_prompt_override) as prompt_length
FROM agent_configurations ac
JOIN agents a ON ac.agent_id = a."Id"
WHERE a."Name" = 'MeepleAssistant POC';

\echo ''
\echo '✓ Check 3: Linked VectorDocuments'
SELECT
    ac.selected_document_ids_json,
    jsonb_array_length(ac.selected_document_ids_json::jsonb) as doc_count
FROM agent_configurations ac
JOIN agents a ON ac.agent_id = a."Id"
WHERE a."Name" = 'MeepleAssistant POC';

\echo ''
\echo '✓ Check 4: VectorDocument Details (Azul)'
SELECT
    vd."Id",
    g."Name" as game_name,
    vd."ChunkCount",
    vd."IndexingStatus",
    vd."EmbeddingModel",
    vd."IndexedAt"
FROM vector_documents vd
JOIN games g ON vd."GameId" = g."Id"
WHERE vd."Id" = '8b78c72a-b5bc-454e-875b-22754a673c40';

\echo ''
\echo '✓ Check 5: System Prompt RAG Placeholder'
SELECT
    CASE
        WHEN ac.system_prompt_override LIKE '%{RAG_CONTEXT}%'
        THEN '✓ RAG placeholder present'
        ELSE '✗ RAG placeholder MISSING'
    END as rag_ready
FROM agent_configurations ac
JOIN agents a ON ac.agent_id = a."Id"
WHERE a."Name" = 'MeepleAssistant POC';

\echo ''
\echo '✓ Check 6: Professional Prompt Structure'
SELECT
    CASE
        WHEN ac.system_prompt_override LIKE '%ROLE & EXPERTISE%'
         AND ac.system_prompt_override LIKE '%RESPONSE GUIDELINES%'
         AND ac.system_prompt_override LIKE '%INTERACTION PATTERNS%'
        THEN '✓ Professional structure complete'
        ELSE '✗ Prompt structure incomplete'
    END as prompt_quality
FROM agent_configurations ac
JOIN agents a ON ac.agent_id = a."Id"
WHERE a."Name" = 'MeepleAssistant POC';

\echo ''
\echo '========================================================================'
\echo 'Summary: RAG Integration Status'
\echo '========================================================================'

SELECT
    a."Name" as agent_name,
    a."IsActive" as is_active,
    ac.llm_model as model,
    ac.is_current as config_active,
    jsonb_array_length(ac.selected_document_ids_json::jsonb) as linked_docs,
    EXISTS(
        SELECT 1 FROM vector_documents
        WHERE "Id" = (ac.selected_document_ids_json::jsonb->0)::text::uuid
          AND "IndexingStatus" = 'completed'
    ) as docs_indexed,
    ac.system_prompt_override LIKE '%{RAG_CONTEXT}%' as rag_ready
FROM agents a
JOIN agent_configurations ac ON ac.agent_id = a."Id"
WHERE a."Name" = 'MeepleAssistant POC';

\echo ''
\echo '✓✓✓ If all checks show true/completed/present, RAG integration is READY ✓✓✓'
\echo ''
\echo 'Next Step: Test agent via API'
\echo '  curl -X POST http://localhost:8080/api/v1/agents/{agent-id}/chat \'
\echo '    -H "Authorization: Bearer {token}" \'
\echo '    -d '\''{"message": "How do you score in Azul?"}'\'''
\echo ''
