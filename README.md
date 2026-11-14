# Levvy

The accessibility layer for live lectures. HackGlobal 2026 build (14-15 Nov).
See the full PRD: `Documents/Levvy/Levvy-PRD.pdf`.

## Stack (frozen)

- **API:** ASP.NET Core minimal APIs + WebSockets, .NET 10
- **CQRS:** MediatR 12.4.1 (Apache license), FluentValidation 12.2.0
- **Persistence:** EF Core. SQLite locally; Vultr Managed Postgres in prod (connection-string swap)
- **LLM + STT:** Groq, OpenAI-compatible endpoint. Answers: `gpt-oss-120b` (fallback `llama-3.3-70b-versatile`). STT: `whisper-large-v3-turbo`, chunked (~5-10s), chunk offsets accumulate into the receipt timeline
- **Storage:** Vultr Object Storage (S3-compatible): recordings + packs
- **Fallback ASR:** Deepgram ($200 free credit), same `IAsrStreamer` interface, swap-in only

## Layers

```
Levvy.Domain          entities only, zero dependencies
Levvy.Application     MediatR commands/queries/events + validators + service interfaces
Levvy.Infrastructure  EF Core, Groq clients, object storage, (WebSocket transport on the 14th)
Levvy.Api             minimal endpoints + /ws/{sessionId}; DI wiring
Levvy.Tests           only the logic that can lose the demo (grounding, timestamp math)
```

## Rules of the product (non-negotiable)

1. **Cite or say not covered.** Every answer/summary/flashcard cites the recording offset it came from, or returns `not_covered`. Never general knowledge dressed as the professor.
2. **Streaming bypasses MediatR.** Audio chunks are a hot pipe; use cases (ask, end session) go through the bus; `CaptionFinalized` / `QuestionAsked` / `SessionEnded` events are the seams.
3. **No keys in config files.** User-secrets or env vars only.

## WebSocket contract (frozen with frontend)

Connect: `ws://host/ws/{sessionId}?lang=es`

Server to client:
```json
{ "type": "caption",     "ts": "43:12", "text": "...", "final": true }
{ "type": "translation", "for": "es",   "ts": "43:12", "text": "..." }
{ "type": "answer",      "text": "...", "receipts": [2507, 2636], "status": "ok" }
```
Client to server: `{ "type": "ask", "text": "..." }`

REST: `POST /sessions` · `POST /sessions/{code}/join` · `GET /sessions/{code}/transcript` · `POST /sessions/{id}/end`

## Before the build day (owner: team)

- [ ] AWS Transcribe keys verified (fallback fallback; Groq is primary)
- [ ] Deepgram account exists (insurance)
- [ ] Groq key in user-secrets, spend checked
- [ ] Vultr Object Storage bucket + keys (owner has Vultr access)
- [ ] Two demo audio fixtures recorded (a real 10 min lecture; a 2 min teammate clip)
- [ ] 10 user conversations per teammate, per country (Reach evidence)
