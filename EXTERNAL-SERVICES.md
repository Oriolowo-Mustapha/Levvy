# External Services

Every outside service Levvy depends on, what it does, and where it plugs in. Secrets live in user-secrets or env vars, never in config files.

---

## 1. Groq (primary AI provider)

**Website:** https://console.groq.com

One vendor, two jobs. OpenAI-compatible REST endpoint, so one `HttpClient` handles both.

### 1a. Speech to Text (live captions)

| | |
|---|---|
| Model | `whisper-large-v3-turbo` |
| Endpoint | `POST /openai/v1/audio/transcriptions` (multipart) |
| Use | Transcribes the live lecture audio in ~5-10s chunks. Each chunk's segments get the accumulated `chunkStartOffsetMs` added, which builds the receipt timeline every citation points into. |
| Code entry point | `Levvy.Infrastructure/Asr/GroqAsrStreamer.cs` → implements `IAsrStreamer` |
| Config key | `Groq:AsrModel` (in `appsettings.json`, no secret) |
| Secret | `Groq:ApiKey` via `dotnet user-secrets` |
| Status | Stub, throws `NotImplementedException` until 14 Nov |

### 1b. LLM (translation + grounded answers + study pack)

| | |
|---|---|
| Models | `gpt-oss-120b` (primary), `llama-3.3-70b-versatile` (fallback) |
| Endpoint | `POST /openai/v1/chat/completions` |
| Uses | 1. Per-segment subtitle translation into each viewer's preferred language. 2. "Ask Levvy" mid-lecture Q&A, grounded in the transcript only, under the cite-or-say-not-covered rule. 3. Post-class study pack: summary + flashcards, each item citing its recording offset. |
| Code entry point | `Levvy.Infrastructure/Llm/GroqLlmClient.cs` → implements `ITranslator` and `ILlmAnswerer` |
| Config keys | `Groq:AnswerModel`, `Groq:FallbackModel`, `Groq:BaseUrl` |
| Secret | `Groq:ApiKey` (same key serves STT and chat) |
| Status | Stub, throws `NotImplementedException` until 14 Nov |

---

## 2. Deepgram (fallback ASR)

**Website:** https://console.deepgram.com

| | |
|---|---|
| Use | Insurance. If Groq's Whisper endpoint degrades or rate-limits during the demo, Deepgram's true streaming API takes over. It only has to satisfy the same `IAsrStreamer` contract, so the swap is one class. |
| Credit | $200 free credit, more than covers a 48h demo |
| Code entry point | New class next to `GroqAsrStreamer`, same `IAsrStreamer` interface |
| Secret | `Deepgram:ApiKey` via user-secrets |
| Status | Not written yet. Only exists if Groq shows weakness |

---

## 3. Vultr (all infrastructure)

**Website:** https://www.vultr.com

Three roles, covered by credits:

### 3a. Vultr Compute

| | |
|---|---|
| Use | Hosts the Levvy API (ASP.NET Core minimal APIs + WebSocket hub) for the demo. Attendees connect their browsers here. |
| Code entry point | Publish `Levvy.Api` as a self-contained deploy |
| Status | Provision on build day |

### 3b. Vultr Object Storage (S3-compatible)

| | |
|---|---|
| Use | Stores the raw lecture recordings and the generated study packs. Every playable receipt ultimately resolves to a position in a recording stored here. |
| Code entry point | `IAudioStore` interface (`SaveRecordingAsync` / `OpenRecordingAsync`), implemented in Infrastructure |
| Secret | `Storage:AccessKey`, `Storage:SecretKey` via user-secrets; bucket name + endpoint in `appsettings.json` |
| Status | Interface defined, implementation pending |

### 3c. Vultr Managed Databases (PostgreSQL)

| | |
|---|---|
| Use | Production persistence. The code already supports it: `Database:Provider` = `postgres` swaps SQLite for Npgsql. Same schema, no code change. |
| Code entry point | `Program.cs` DbContext wiring; connection string `ConnectionStrings:Levvy` |
| Status | SQLite carries local dev; provision Postgres when demo hosting is set up |

---

## 4. GitHub (from the 14th only)

**Website:** https://github.com

| | |
|---|---|
| Use | Source hosting for the team. Per team rule: no init, no push, nothing online until build starts on 14 Nov. |
| Auth | gh CLI credential helper already configured on this machine |
| Identity | Certified07 <abdulsamadtofunmi01@gmail.com> |
| Status | Intentionally absent until build day |

---

## Secrets map (single source of truth)

| Secret | Where it goes | Set with |
|---|---|---|
| `Groq:ApiKey` | GroqAsrStreamer + GroqLlmClient | `dotnet user-secrets set "Groq:ApiKey" "..."` |
| `Deepgram:ApiKey` | fallback streamer (if needed) | user-secrets |
| `Storage:AccessKey` / `Storage:SecretKey` | Vultr Object Storage impl | user-secrets |
| `ConnectionStrings:Levvy` (prod) | Vultr Managed Postgres | env var on the compute instance |

## Dependency order if something fails

1. Groq STT fails → Deepgram streaming behind `IAsrStreamer`
2. Groq LLM fails → `Groq:FallbackModel` (llama-3.3-70b-versatile), same client
3. Both AI providers fail → captions fall back to text-only mode is NOT an option; record a fixture-based offline demo as the safety net
4. Vultr services are owned by us, so failure window is provisioning, not outages
