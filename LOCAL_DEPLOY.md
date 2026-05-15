# Local Deploy — Docker Compose

Guida per avviare l'intero stack (database + app) con un singolo comando.

## Prerequisiti

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installato e in esecuzione

## Avvio completo

Dalla root del progetto:

```sh
docker compose up --build
```

Docker si occupa di tutto:
1. Costruisce l'immagine SQL Server con gli script di init
2. Avvia il container `db`, esegue automaticamente `01_schema.sql`, `02_procedures.sql`, `03_seed.sql`
3. Attende che il database sia pronto (healthcheck)
4. Costruisce e avvia il container `app` (Blazor Server)

L'app è disponibile su **http://localhost:8080**.

Al primo avvio (~2-3 minuti per il download delle immagini). Gli avvii successivi sono molto più rapidi.

> **Nota:** i dati del database persistono in un volume Docker (`sqlserver-data`). Eliminando il volume si riparte da zero con i dati seed.

## Fermare lo stack

```sh
docker compose down        # ferma i container, il volume persiste
docker compose down -v     # ferma i container e cancella il volume (dati persi)
```

## Ricostruire dopo modifiche

Se hai modificato solo il codice Blazor (nessuna modifica agli script SQL o al Dockerfile del db):

```sh
docker compose up --build -d app
```

Ricostruisce e riavvia solo il container `app`, lasciando il database intatto e operativo. Più rapido della ricostruzione completa.

Per ricostruire tutto lo stack:

```sh
docker compose up --build
```

## Connessione diretta al database

Usa **Azure Data Studio** o **DBeaver** con questi parametri mentre lo stack è attivo:

| Parametro | Valore |
|---|---|
| Host | `localhost` |
| Porta | `1433` |
| Utente | `sa` |
| Password | `StrongPass123!` |
| Database | `LibraryDB` |

## Struttura dei container

| Container | Immagine base | Porta esposta |
|---|---|---|
| `library-app-db-1` | SQL Server 2022 | 1433 |
| `library-app-app-1` | .NET 10 ASP.NET Core | 8080 |

## Healthcheck

Il container `app` dipende da `db` tramite `condition: service_healthy`. Il healthcheck esegue ogni 10 secondi una query T-SQL che verifica l'esistenza del database `LibraryDB`; solo quando passa, Docker avvia l'app. In caso di avvio lento del db, aumentare `retries` in `docker-compose.yml`.
