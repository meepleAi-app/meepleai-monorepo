-- PostgreSQL initialization script
-- Runs on first container start (when pgdata volume is empty).
-- EF Core migrations handle schema/tables — this script only sets up
-- extensions and roles that need superuser privileges.

-- pgvector extension for vector similarity search (primary vector store — replaces Qdrant)
CREATE EXTENSION IF NOT EXISTS vector;

-- pg_trgm extension for trigram-based fuzzy text search
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- uuid-ossp for UUID generation (fallback when not using .NET Guid)
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
