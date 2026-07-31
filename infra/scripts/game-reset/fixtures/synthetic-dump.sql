-- Minimal synthetic schema for rollback rehearsal.
-- Mirrors pgvector_embeddings + games surface enough to verify restore.

CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE games (
    id           uuid PRIMARY KEY,
    title        text NOT NULL,
    created_at   timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE pgvector_embeddings (
    id           uuid PRIMARY KEY,
    game_id      uuid NOT NULL,
    text_content text NOT NULL,
    vector       vector(768) NOT NULL,
    created_at   timestamptz NOT NULL DEFAULT now()
);

-- Seed: 3 games, 5 vectors
INSERT INTO games (id, title) VALUES
  ('00000000-0000-0000-0000-000000000001', 'Catan'),
  ('00000000-0000-0000-0000-000000000002', 'Wingspan'),
  ('00000000-0000-0000-0000-000000000003', 'Terraforming Mars');

-- Use a zero-vector for simplicity (HNSW search isn't tested here)
INSERT INTO pgvector_embeddings (id, game_id, text_content, vector)
SELECT
  gen_random_uuid(),
  ('00000000-0000-0000-0000-00000000000' || ((i % 3) + 1))::uuid,
  'chunk ' || i,
  array_fill(0::real, ARRAY[768])::vector
FROM generate_series(1, 5) AS s(i);
