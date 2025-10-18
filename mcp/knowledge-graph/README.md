# Knowledge Graph MCP Server

Server MCP per gestione grafo di conoscenza con Qdrant.

## Funzionalità

- Creazione entità e relazioni
- Query semantiche sul grafo
- Visualizzazione grafo
- Path finding

## Avvio

```bash
docker run -i --rm \
  --read-only \
  --tmpfs /tmp:rw,size=64m \
  --cap-drop ALL \
  --security-opt no-new-privileges \
  --pids-limit 128 \
  --memory 512m \
  --user $(id -u):$(id -g) \
  --network mcp-network \
  -e QDRANT_URL=http://qdrant:6333 \
  -e QDRANT_COLLECTION=knowledge_graph \
  -e OPENROUTER_API_KEY=${OPENROUTER_API_KEY} \
  meepleai/mcp-knowledge-graph:latest
```

## Tools

- `kg_add_entity`: Aggiungi entità al grafo
- `kg_add_relation`: Aggiungi relazione tra entità
- `kg_query`: Esegui query sul grafo
- `kg_get_neighbors`: Ottieni entità vicine (connesse) a un'entità specifica
- `kg_stats`: Statistiche del grafo (numero entità, relazioni, metriche)
