# Levvy — Architecture

> **System:** The accessibility layer for live lectures (HackGlobal 2026).  
> **Monorepo:** `backend/` (.NET 10 Clean Architecture) + `frontend/` (Next.js App Router + TypeScript + Tailwind CSS).

---

## 1. High-Level System Flow

```text
Host Browser (mic / tab audio / file)
  │  MediaRecorder / WebAudio chunks (~5-10s)
  ▼
WebSocket: wss://api/ws/{sessionId}?role=host
  │
  ▼
Levvy.Api (ASP.NET Core .NET 10 Minimal APIs + WebSocket Hub)
  ├── Hot Audio Pipe (bypasses MediatR)
  │     ├──► IAsrStreamer (Groq whisper-large-v3-turbo; fallback: Deepgram)
  │     │       └── accumulates chunkStartOffsetMs → TranscriptSegment(OffsetMs, Text)
  │     ├──► IAudioStore (Vultr Object Storage, S3-compatible)
  │     └──► ITranslator (Groq LLM) → per-viewer language tracks
  │             └──► Broadcasts `caption` & `translation` over WebSockets
  │
  └── Use-Case Bus (MediatR 12.4.1 + FluentValidation 12.2.0)
        ├── CreateSessionCommand      (POST /sessions)
        ├── JoinSessionCommand        (POST /sessions/{code}/join)
        ├── AskQuestionCommand        (WS `ask` / POST /sessions/{code}/ask)
        │     └── ILlmAnswerer (gpt-oss-120b, fallback llama-3.3-70b-versatile)
        │         STRICT: cite-or-say-not-covered over rolling TranscriptSegments
        ├── EndSessionCommand         (POST /sessions/{id}/end)
        └── GenerateStudyPackCommand  (summary + flashcards with SourceOffsetMs)
```

---

## 2. Backend Architecture (`backend/`)

Clean Architecture across 5 projects in `backend/Levvy.slnx`:

| Project | Responsibility | Dependencies |
|---|---|---|
| `Levvy.Domain` | Core entities: `Session`, `Participant`, `TranscriptSegment`, `Question`, `Answer`, `AnswerCitation`, `StudyPack`, `Flashcard` | None |
| `Levvy.Application` | MediatR commands/queries/events, FluentValidation validators, interfaces (`IAsrStreamer`, `ITranslator`, `ILlmAnswerer`, `ISessionRepository`, `IAudioStore`) | `Levvy.Domain`, MediatR, FluentValidation |
| `Levvy.Infrastructure` | `LevvyDbContext` (EF Core: SQLite dev / Npgsql prod), `GroqAsrStreamer`, `GroqLlmClient`, S3-compatible `IAudioStore` | `Levvy.Application`, EF Core, Npgsql, Sqlite |
| `Levvy.Api` | Minimal API endpoints, `/ws/{sessionId}` WebSocket hub, DI wiring, health check | `Levvy.Application`, `Levvy.Infrastructure` |
| `Levvy.Tests` | xUnit tests for DI pipeline, grounding (`cite-or-say-not-covered`), and timestamp offset math | `Levvy.Application`, `Levvy.Domain` |

### Design Patterns Used (Backend)
1. **Clean Architecture (Dependency Inversion):** `Levvy.Domain` (zero deps) ← `Levvy.Application` (interfaces, commands, queries, events) ← `Levvy.Infrastructure` (EF Core, Groq, S3) & `Levvy.Api` (Composition Root).
2. **CQRS + Mediator (`MediatR`):** Write operations (`CreateSessionCommand`, `JoinSessionCommand`, `AskQuestionCommand`, `EndSessionCommand`, `GenerateStudyPackCommand`) and read operations (`GetSessionByCodeQuery`, `GetTranscriptQuery`) are separated and dispatched through `IMediator`.
3. **Pipeline Behavior + Validation (`FluentValidation`):** `ValidationBehavior<TRequest, TResponse>` intercepts MediatR requests to fail fast before handlers execute.
4. **Domain Events / Observer (`INotification`):** `CaptionFinalized`, `QuestionAsked`, and `SessionEnded` decouple real-time streaming from persistence, translation fan-out, and async study-pack generation.
5. **Repository Pattern (`ISessionRepository`):** Isolates EF Core persistence (targeting **Vultr Managed PostgreSQL** via `Npgsql` directly for prod-first build; SQLite fallback remains wired if needed).
6. **Strategy / Adapter Pattern (`IAsrStreamer`, `ITranslator`, `ILlmAnswerer`, `IAudioStore`):** Allows hot-swapping providers (`GroqAsrStreamer` ↔ `Deepgram`, `gpt-oss-120b` ↔ `llama-3.3-70b-versatile`, **Vultr Object Storage** S3 adapter) without touching Application logic.
7. **Hot-Pipe Bypass:** Binary audio chunks skip MediatR overhead to preserve the `<3s` caption latency budget, emitting `CaptionFinalized` only at chunk boundaries.
8. **Strict Grounding Guardrail (`cite-or-say-not-covered`):** `ILlmAnswerer` and `GenerateStudyPackCommand` must only cite `TranscriptSegment.OffsetMs` values that exist in the session's recorded transcript, or return `status: "not_covered"`.

---

## 3. Frontend Architecture (`frontend/`)

Next.js (App Router) + TypeScript + Tailwind CSS inside `frontend/src/`:

| Route / Module | Purpose |
|---|---|
| `app/page.tsx` | Landing / Join-by-code + "Host a session" entry |
| `app/host/page.tsx` | Host console: mic / tab-audio / file capture, room code + QR, live status |
| `app/session/[code]/page.tsx` | Viewer workspace: live transcript, language switcher, Ask Levvy panel, audio receipt player |
| `app/session/[code]/pack/page.tsx` | Post-class study pack: clean transcript, cited summary, cited flashcards |
| `lib/ws.ts` | Reconnecting WebSocket client for `/ws/{sessionId}?lang=xx` |
| `lib/api.ts` | Typed REST client for `/sessions`, `/join`, `/transcript`, `/end`, `/audio` |
| `components/` | `TranscriptFeed`, `ReceiptChip`, `AudioReceiptPlayer`, `AskLevvyPanel`, `SubtitleSelector`, `StudyPackView` |

---

## 4. Frozen API & WebSocket Contract

### REST Endpoints
- `GET /health` → `{ status: "ok", service: "levvy-api" }`
- `POST /sessions` → Create session, returns `{ id, code, title, hostId }`
- `POST /sessions/{code}/join` → Join session, returns `{ sessionId, participantId, title }`
- `GET /sessions/{code}/transcript` → Full `TranscriptSegment[]` + `StudyPack?`
- `POST /sessions/{id}/end` → Ends session, triggers `GenerateStudyPackCommand`
- `GET /sessions/{code}/audio` → Streams/redirects to recording in Vultr Object Storage for receipt playback

### WebSocket (`ws[s]://host/ws/{sessionId}?lang=es`)
- **Server → Client:**
  - `{ "type": "caption", "ts": "43:12", "offsetMs": 2592000, "text": "...", "final": true }`
  - `{ "type": "translation", "for": "es", "ts": "43:12", "offsetMs": 2592000, "text": "..." }`
  - `{ "type": "answer", "text": "...", "receipts": [2507000, 2636000], "status": "ok" | "not_covered" }`
- **Client → Server:**
  - `{ "type": "ask", "text": "..." }`
  - Binary audio frames (host only)

---

## 5. Infrastructure & Deployment (Vultr)

- **Vultr Compute:** Hosts `Levvy.Api` (and serves/reverse-proxies `frontend/` behind HTTPS/WSS so REST and WebSockets share one origin).
- **Vultr Managed PostgreSQL:** Activated via `Database:Provider=postgres` + `ConnectionStrings:Levvy`.
- **Vultr Object Storage (S3-compatible):** Stores raw session audio + generated study packs via `IAudioStore`.
