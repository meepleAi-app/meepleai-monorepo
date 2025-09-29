# Esempio: crea una collection Qdrant e inserisce un vettore demo via API del backend (stub-ready)
Invoke-RestMethod -Method Post -Uri "http://localhost:8080/admin/seed" -ContentType "application/json" -Body "{}"
