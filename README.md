# Controller

**Purpose:** Live distribution layer for measurements.

- Consumes Kafka topic `measurements`
- Streams data to clients via **SignalR**
- Stores subscription state (and/or SignalR scale-out) in **Redis**

Controller does not own Postgres data.

---

## API

Swagger:
- `http://localhost:8082/swagger`

SignalR hub:
- `http://localhost:8082/hub/measurements`

REST endpoints (subscriptions CRUD):
- `GET /subscriptions`
- `POST /subscriptions`
- `PUT /subscriptions/{id}`
- `DELETE /subscriptions/{id}`

Health:
- `GET /health/live`
- `GET /health/ready`

---

## Redis

Used for:
- subscription state and/or backplane (depending on configuration)

Compose service:
- `redis`
