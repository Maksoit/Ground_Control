# AIRPORT — API & ROUTING (v1)

This document describes:
- docker-compose service naming (DNS)
- nginx public routing
- REST endpoints per service (v1)
- high-level event contracts (Kafka) that glue the system together

> Simulation step = **1 minute**.  
> Real time: **1 sim-minute ~ 4s** (speed=1) or **~2s** (speed=2).  
> Source of truth for simTime and flight statuses: **Flights**.

---

## 1 Naming / DNS (docker-compose service names)

gateway (nginx)  
frontend (vue)  
flights  
tickets  
passengers  
checkin  
board  
handling  
ground  
bus  
refueler  
catering  
followme  

---

## 2 Public routing (nginx)

GET  `/`                       -> `frontend` (Vue SPA)

ANY  `/api/flights/*`          -> `flights`
ANY  `/api/tickets/*`          -> `tickets`
ANY  `/api/passengers/*`       -> `passengers`
ANY  `/api/checkin/*`          -> `checkin`
ANY  `/api/board/*`            -> `board`
ANY  `/api/handling/*`         -> `handling`
ANY  `/api/ground/*`           -> `ground`
ANY  `/api/bus/*`              -> `bus`
ANY  `/api/refueler/*`         -> `refueler`
ANY  `/api/catering/*`         -> `catering`
ANY  `/api/followme/*`         -> `followme`

---

## 3 Common endpoints (all services)

- GET `/health`
- GET `/version`

---

## 4 Cross-service rules (important)

### 4.1 Idempotency (must-have)
Because Kafka is *at-least-once*, every service must be resilient to duplicates.

**Kafka handlers:**
- Deduplicate by `eventId` (inbox `processed_events` table).
- ACK duplicate events without side effects.

**REST commands:**
- Prefer idempotency by natural key:
  - flights: `flightId`
  - board: `planeId`
  - passengers: `passengerId`
  - bus pickup: `tripId`
  - ground allocate route: `reservationId`
- If needed: add `Idempotency-Key` header (optional), but keep v1 simple.

### 4.2 Outbox publishing (recommended)
For important outgoing events (status changes, task completions, departures), publish via outbox worker.

---

## 5 FLIGHTS — Tablo + Simulation Time
**service:** `flights`  
**prefix:** `/api/flights`

### 5.1 Simulation control (UI -> Flights)
- GET   `/v1/simulation`  
  Returns simTime, paused, speed, tick config.

- POST  `/v1/simulation/start`  
  Starts simulation (paused=false).

- POST  `/v1/simulation/pause`  
  Pauses simulation (paused=true).

- POST  `/v1/simulation/resume`  
  Alias of start/resume (paused=false).

- PATCH `/v1/simulation/speed`  
  Body: `{ "speedMultiplier": 1 | 2 }`

> Optional (if you want): setting time should only be allowed when paused=true  
> - POST `/v1/simulation/time` body `{ "simulationTime": "ISO" }`

### 5.2 Flights CRUD (UI -> Flights)
- POST  `/v1/flights/bulk`  
  Creates flights (idempotent by `flightId` inside items).

- GET   `/v1/flights`  
  Optional filters: `?status=&flightType=`.

- GET   `/v1/flights/{flightId}`

### 5.3 History (UI -> Flights)
- GET   `/v1/flights/{flightId}/history`

### 5.4 Kafka (Flights contract summary)
Produces:
- `sim.events` -> `sim.time.tick`
- `flights.events` -> `flight.created`, `flight.status.changed`, `flight.taxi.start`

Consumes:
- `board.events` -> `plane.departed` (sets `flight.status=Departed`)

---

## 6 TICKETS — Ticket Sales
**service:** `tickets`  
**prefix:** `/api/tickets`

### 6.1 Tickets (UI)
- GET   `/v1/tickets`  
  Filters: `?flightId=&passengerId=&status=`.

- GET   `/v1/tickets/{ticketId}`

- GET   `/v1/tickets/passenger/{passengerId}`

### 6.2 Flight sales state (UI/Debug)
- GET   `/v1/flight-sales`  
  (all flights)

- GET   `/v1/flight-sales/{flightId}`

### 6.3 Manual ops (UI/Debug)
- POST  `/v1/tickets/buy`
- POST  `/v1/tickets/refund`  
  (If your existing spec uses `/v1/tickets/{ticketId}/refund`, keep that; README can note aliases.)

### 6.4 History (UI)
- GET   `/v1/tickets/{ticketId}/history`

### 6.5 Kafka (Tickets contract summary)
Consumes:
- `flights.events` -> `flight.created`, `flight.status.changed` (close sales at RegistrationOpen)
- `passengers.events` -> `passenger.created` (auto-buy if allowed)
- `board.events` -> `board.boarding.result` (bumped -> ticket.bumped/refund)

Produces:
- `tickets.events` -> `ticket.bought`, `ticket.refunded`, `ticket.bumped`

---

## 7 PASSENGERS
**service:** `passengers`  
**prefix:** `/api/passengers`

### 7.1 Create (UI)
- POST  `/v1/passengers/bulk`  
  Creates passengers (idempotent by `passengerId` in items).

### 7.2 Query (UI)
- GET   `/v1/passengers`  
  Filters: `?flightId=&state=`.

- GET   `/v1/passengers/{passengerId}`

### 7.3 History (UI)
- GET   `/v1/passengers/{passengerId}/history`

### 7.4 Bus pickup (Bus -> Passengers, required)
> **Critical endpoint** to make Bus robust and prevent double-pickups.

- POST `/v1/transfers/bus/pickup`  
  Idempotent by `tripId`.

Request:
```json
{ "flightId": "FL123", "tripId": "uuid", "limit": 20 }