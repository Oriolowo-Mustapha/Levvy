# Levvy — REST & WebSocket API Reference

Base URL (Production): `https://levvy.tech/api` (or configured `NEXT_PUBLIC_API_URL`)  
Interactive OpenAPI / Swagger UI: `/swagger`

---

## 1. Authentication (`/auth`)

Authentication uses Bearer JWTs (`Authorization: Bearer <token>`). Tokens default to a **7-day (`10080` minute)** expiry to match the frontend's **"Keep me signed in for 7 days"** preference. Rate-limited per IP (`auth` policy) to prevent credential stuffing.

| Method | Path | Auth | Summary |
|---|---|---|---|
| `POST` | `/auth/register` | Public | Register a new account with email, password, role (`student` \| `instructor`), and accessibility preferences. Returns `201 Created` with `{ token, expiresAtUtc, user }`. |
| `POST` | `/auth/login` | Public | Authenticate with email and password. Returns `200 OK` with `{ token, expiresAtUtc, user }`. |
| `POST` | `/auth/google` | Public | Verify a Google ID token (`idToken`) and sign in or provision the user. |
| `GET` | `/auth/config` | Public | Returns `{ googleClientId }` for browser Google Identity initialization. |
| `GET` | `/auth/me` | Bearer | Returns the currently authenticated user's `UserProfileDto`. |

---

## 2. Users & Dashboard (`/users`)

| Method | Path | Auth | Summary |
|---|---|---|---|
| `GET` | `/users/me/dashboard` | Bearer | Returns `UserDashboardDto` (profile, metrics, hosted/attended sessions, and study packs). |
| `GET` | `/users/{id}/dashboard` | Bearer | Returns dashboard for `{id}` (`403 Forbidden` if Bearer user ID does not match `{id}`). |
| `PUT` | `/users/me/profile` | Bearer | Updates profile (`firstName`, `lastName`, `role`, `institution`, `department`, `preferredLanguage`, `accessibilityMode`, `captionFontSize`). |
| `PUT` | `/users/{id}/profile` | Bearer | Updates profile by user ID (owner-checked). |

---

## 3. Lecture Sessions (`/sessions`)

| Method | Path | Auth | Summary |
|---|---|---|---|
| `POST` | `/sessions` | Bearer / Optional | Create a new live lecture session (`{ title, hostId? }`). Returns `201 Created` with `{ id, code, title, hostId, startedAtUtc }`. |
| `POST` | `/sessions/demo` | Public | Idempotently seeds or retrieves the `K4F-8Q2` demo lecture session. |
| `GET` | `/sessions/{code}` | Public | Fetch session metadata by 6-character room code (e.g. `K4F-8Q2` or `K4F8Q2`). |
| `POST` | `/sessions/{code}/join` | Optional Bearer | Join a session as a participant (`{ displayName, preferredLanguage }`). Links `UserId` when authenticated. |
| `GET` | `/sessions/{code}/transcript` | Public | Returns `{ sessionId, code, title, startedAtUtc, endedAtUtc, hasRecording, segments }`. |
| `POST` | `/sessions/{code}/ask` | Public | Ask a grounded question (`{ question, targetLanguage? }`). Enforces `cite-or-say-not-covered` via `GroundingVerifier`. |
| `POST` | `/sessions/{code}/translate` | Public | Batch-translate existing transcript segments into `{ targetLanguage }`. |
| `POST` | `/sessions/{code}/speak` | Public | Synthesize MP3 speech (`audio/mpeg`) for `{ text, language?, voiceModel? }` via **Deepgram Aura-2 TTS**. |
| `POST` | `/sessions/{id}/ws-ticket` | Bearer (Host) | Issue a 30-second, single-use WebSocket ticket for host audio streaming (`{ ticket, expiresInSeconds: 30 }`). |
| `POST` | `/sessions/{id}/audio` | Host (`HostAccess`) | Upload recorded lecture audio (`audio/webm` raw body or multipart `file`) to Vultr Object Storage. |
| `GET` | `/sessions/{code}/audio` | Public | Stream recorded lecture audio with HTTP Range (`206 Partial Content`) support for `[mm:ss]` citation seeking. |
| `POST` | `/sessions/{id}/end` | Host (`HostAccess`) | Mark session ended (`EndedAtUtc`), broadcast `session_ended`, and enqueue background Study Pack generation. |
| `GET` | `/sessions/{code}/pack` | Public | Retrieve generated Study Pack (`{ status: "ready" \| "generating", studyPack }`). |
| `POST` | `/sessions/{code}/pack/generate` | Public | Synchronously generate or regenerate the Study Pack & flashcards for an ended session with transcript segments. |
| `POST` | `/sessions/online-class` | Public / Host | Create a session bound to Google Meet / Zoom / Teams and dispatch the Levvy Note-Taker bot. |
| `POST` | `/sessions/{id}/connect-meet` | Host (`HostAccess`) | Attach an external meeting URL to an existing session. |
| `POST` | `/sessions/meet-webhook` | Webhook Secret | Account-level Meeting BaaS / Recall.ai webhook ingestion. |
| `POST` | `/sessions/{id}/meet-webhook` | Webhook Secret | Session-scoped meeting-bot caption and lifecycle webhook ingestion. |

---

## 4. Real-Time WebSocket Contract (`/ws/{sessionId}`)

### Connection URL
- **Viewer:** `wss://<host>/ws/{sessionId}?role=viewer&lang={bcp47}`
- **Host:** `wss://<host>/ws/{sessionId}?role=host&ticket={singleUseTicket}&sourceLang={auto|en|...}`

### Client → Server Frames

| Frame Type | Role | Payload Example | Description |
|---|---|---|---|
| **Binary Frame** | `host` | `Uint8Array` (`audio/webm`) | 5s WebM/Opus audio chunk piped directly to `ResilientAsrStreamer` (Deepgram Nova-3 / Groq Whisper). |
| `offset` | `host` | `{ "type": "offset", "offsetMs": 15000 }` | Synchronizes the host's recording timeline offset before the next binary chunk. |
| `set_source_lang` | `host` | `{ "type": "set_source_lang", "lang": "auto" }` | Updates spoken source language (`auto` uses Deepgram language detection). |
| `set_lang` | `viewer` / `host` | `{ "type": "set_lang", "lang": "fr" }` | Switches the client's active subtitle translation language on the fly. |
| `ask` | `viewer` / `host` | `{ "type": "ask", "text": "What is elasticity?", "lang": "fr" }` | Submits a grounded mid-lecture question over the socket. |
| `ping` | `any` | `{ "type": "ping" }` | Heartbeat check; server replies with `pong`. |

### Server → Client Frames

| Frame Type | Recipient | Payload Example | Description |
|---|---|---|---|
| `connected` | Caller | `{ "type": "connected", "sessionId": "...", "role": "host", "lang": "en", "sourceLang": "auto" }` | Confirms WebSocket handshake and effective role. |
| `asr_status` | Host | `{ "type": "asr_status", "state": "processing" \| "ready" \| "error", "offsetMs": 5000 }` | Real-time ASR chunk feedback for the host studio visualizer and safe wrap-up. |
| `caption` | Room broadcast | `{ "type": "caption", "segmentId": "...", "ts": "00:05", "offsetMs": 5000, "text": "...", "sourceLanguage": "en", "final": true }` | Finalized original-language caption anchored to `offsetMs`. |
| `translation` | Language subscribers | `{ "type": "translation", "for": "fr", "ts": "00:05", "offsetMs": 5000, "text": "..." }` | Translated subtitle for viewers subscribed to language `for`. |
| `answer` | Caller | `{ "type": "answer", "question": "...", "text": "...", "status": "ok" \| "not_covered", "receipts": [5000], "citations": [{ "offsetMs": 5000, "ts": "00:05", "text": "..." }] }` | Grounded Ask Levvy response with verified audio receipts. |
| `session_ended` | Room broadcast | `{ "type": "session_ended", "sessionId": "...", "endedAtUtc": "..." }` | Notifies all connected clients that the host ended the lecture. |
| `error` | Caller | `{ "type": "error", "message": "..." }` | Non-fatal validation or upstream provider error notice. |
