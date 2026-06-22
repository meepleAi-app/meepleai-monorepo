-- KB coverage query for SP4 seed games
-- Run via: pwsh -c "docker cp infra/scripts/kb-coverage-query.sql meepleai-postgres:/tmp/; docker exec meepleai-postgres psql -U meepleai -d meepleai_staging -f /tmp/kb-coverage-query.sql"

SELECT
  sg.title,
  sg.has_knowledge_base AS kb_flag,
  COUNT(DISTINCT pd."Id") FILTER (WHERE pd.processing_state = 'Ready') AS pdfs_ready,
  COUNT(DISTINCT tc."Id") AS chunks,
  COUNT(DISTINCT pe.id) AS embeds,
  COALESCE(MAX(vd."IndexingStatus"), 'no_vd') AS vector_status,
  CASE
    WHEN COUNT(DISTINCT pe.id) > 0 THEN 'complete'
    WHEN COUNT(DISTINCT tc."Id") > 0 AND COUNT(DISTINCT pe.id) = 0 THEN 'embeddings_missing'
    WHEN COUNT(DISTINCT tc."Id") = 0 THEN 'no_kb'
    ELSE 'partial'
  END AS status
FROM shared_games sg
LEFT JOIN pdf_documents pd ON pd.shared_game_id = sg.id AND pd.is_active_for_rag = true
LEFT JOIN text_chunks tc ON tc."PdfDocumentId" = pd."Id"
LEFT JOIN vector_documents vd ON vd."PdfDocumentId" = pd."Id"
LEFT JOIN pgvector_embeddings pe ON pe.vector_document_id = vd."Id"
WHERE sg.is_deleted = false
  AND sg.title IN (
    'Azul',
    'I Coloni di Catan',
    'Wingspan',
    'Brass: Birmingham',
    'Gloomhaven',
    'Ark Nova',
    'Spirit Island',
    '7 Wonders Duel',
    'Codenames',
    'Carcassonne',
    'Ticket to Ride',
    'Pandemic',
    'Terraforming Mars'
  )
GROUP BY sg.id, sg.title, sg.has_knowledge_base
ORDER BY sg.title;
