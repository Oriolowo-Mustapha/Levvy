# ADR 0005: Scoped Single-Use WebSocket Tickets for Host Audio Streaming

- **Status:** Accepted
- **Date:** 2026-11-14

## Context
Browser `WebSocket` constructors cannot set custom `Authorization: Bearer <jwt>` HTTP headers during the upgrade handshake. Passing a user's long-lived JWT directly in the WebSocket query string (`wss://.../ws/{sessionId}?token=...`) risks leaking the JWT in proxy logs, browser history, or error telemetry. Conversely, trusting a bare `?role=host` query parameter without authentication would allow any participant to inject audio chunks into a lecture room.

## Decision
1. **Authenticated Ticket Issuance (`POST /sessions/{sessionId}/ws-ticket`):**
   - Before opening a host WebSocket, the authenticated host calls `POST /sessions/{sessionId}/ws-ticket` with their `Authorization: Bearer <jwt>` header over HTTPS.
   - The server verifies `session.HostId == userId` and `session.EndedAtUtc == null`, then issues a cryptographically random, session-scoped ticket stored in `WebSocketTicketStore` with a **30-second TTL**.
2. **Single-Use Redemption on Upgrade (`/ws/{sessionId}?role=host&ticket=...`):**
   - `WebSocketEndpoints` atomically consumes the ticket (`tickets.Consume(ticket, sessionId)`).
   - Replay attempts, expired tickets, or tickets issued for a different `sessionId` are rejected with `403 Forbidden`.

## Consequences
- **Positive:** Long-lived Bearer JWTs never appear in WebSocket URLs or server access logs.
- **Positive:** Host WebSocket privileges are strictly bound to the session owner and cannot be replayed.
