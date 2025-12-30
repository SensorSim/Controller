# Controller

Live distribution service.

Consumes Kafka measurements and pushes them to connected clients via SignalR. Redis is used as a SignalR backplane so multiple Controller instances can broadcast consistently.

## Branching

- `dev` – development
- `main` – stable / demo-ready

## Requirements

- .NET SDK (tested with 10.0.101)
- Kafka
- Redis

## Run

Recommended: run the full stack with Docker Compose (see `infra/docker`).

Local run (you still need Kafka + Redis running):

```bash
dotnet run
```

## Configuration

Environment variables:

- `Kafka__BootstrapServers` – Kafka bootstrap servers
- `Redis__ConnectionString` – Redis connection string (e.g. `localhost:6379`)

## API

Swagger (docker default): `http://localhost:8082/swagger`

SignalR hub:

- `GET /hub/measurements`

Subscriptions (simple REST CRUD used for demo / PRPO REST requirement):

- `GET /subscriptions`
- `POST /subscriptions/{clientId}?filter=...`
- `PUT /subscriptions/{clientId}?filter=...`
- `DELETE /subscriptions/{clientId}`

Health:

- `GET /health/live`
- `GET /health/ready`
