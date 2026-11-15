---
marp: true
theme: default
paginate: true
backgroundColor: #F5F4F0
color: #141413
---

# **Levvy**
### Live Lectures, in Every Language, with Playable Receipts
**`https://levvy.tech`**

Built with **Next.js 16 · .NET 10 · Deepgram Nova-3 & Aura-2 · Groq `openai/gpt-oss-120b` · Vultr Cloud**

---

## 1. The Classroom Accessibility Gap

- **Language Barrier:** Millions of students attend technical lectures in a second or third language.
- **Cognitive Overload:** Listening, translating mentally, and taking notes simultaneously causes students to miss key explanations.
- **AI Hallucinations:** Generic AI note-takers invent definitions or cite concepts the lecturer never actually taught.

> **Students don't need another chatbot that guesses. They need a live companion that translates, speaks, and proves every answer with the professor's own voice.**

---

## 2. Meet Levvy (`levvy.tech`)

1. **Live Captions & Real-Time Translation:**
   - Lecturers stream mic or browser-tab audio in one click (`/host`).
   - Students join with a 6-character code (`/session/[code]`) and read live captions + translated subtitles side-by-side.
2. **Multilingual Speech Synthesis (TTS):**
   - Click the speaker icon on either card to hear **Deepgram Aura-2** read original or translated captions aloud.
3. **Grounded "Ask Levvy" Q&A (`cite-or-say-not-covered`):**
   - Answers cite exact `[mm:ss]` recording offsets — or explicitly return `not_covered`.
4. **Role-Aware Dashboards & Study Packs:**
   - Hosts wrap up cleanly; students generate cited **Summaries & Interactive Flashcards** backed by playable audio receipts.

---

## 3. Architecture & Hot Audio Pipe

- **Bypassing MediatR on the Hot Path:**
  - 5-second WebM audio chunks stream over WebSockets (`/ws/{sessionId}`) directly into `ResilientAsrStreamer` (**Deepgram Nova-3** primary, **Groq Whisper** circuit-breaker failover).
- **Parallel Subtitle Fan-Out:**
  - **Groq (`openai/gpt-oss-120b`)** translates finalized segments into each active viewer language in parallel.
- **Deterministic Grounding (`GroundingVerifier`):**
  - Every LLM answer and study pack item is verified against real `TranscriptSegment.OffsetMs` values (`±1000ms` tolerance) before reaching the student.

---

## 4. Powered End-to-End by Vultr

| Vultr Service | Role in Levvy |
|---|---|
| **Vultr Compute** | Hosts `.NET 10 Minimal API + SignalR/WS Hub` (`/opt/levvy`) and `Next.js 16 Standalone` (`/opt/levvy-web`) behind HTTPS at `levvy.tech`. |
| **Vultr Object Storage** | Stores raw WebM lecture recordings (`VultrAudioStore`) using streamed AWS SigV4 `UNSIGNED-PAYLOAD` uploads and HTTP `206 Partial Content` range streaming for instant `[mm:ss]` seeking. |
| **Vultr Managed PostgreSQL** | Single source of truth (`EF Core Npgsql`) for users, sessions, transcript timelines, grounded citations, flashcards, and Hangfire jobs. |
| **GitHub Actions CI/CD** | Runs all 55 backend tests, builds both artifacts, SCPs to Vultr Compute, and restarts `systemd` services on every push to `main`. |

---

## 5. Engineering Rigor & Security Hardening

- **55 / 55 Backend xUnit Tests Passing:**
  - Grounding verification (`±1000ms`), Deepgram Nova-3 diarization & keyterms, Aura-2 TTS, ASR circuit breaker, LLM fallback, and meeting URL validation.
- **13 / 13 Playwright E2E + WCAG 2.2 AA Scans Passing:**
  - Tested across 7 routes and 7 viewports (`360px` – `1920px`) with zero axe accessibility violations.
- **Audit-Hardened Security (`C1`–`C3`, `H1`–`H7`, `M1`–`M7`):**
  - Single-use 30s WebSocket host tickets, fail-closed Bearer auth, PBKDF2 rate limiting, and zero-buffer S3 audio streaming.

---

## 6. Live Demo & Links

- **Live App:** [https://levvy.tech](https://levvy.tech)
- **Repository:** [https://github.com/Oriolowo-Mustapha/Levvy](https://github.com/Oriolowo-Mustapha/Levvy)
- **Documentation Suite:** [`/docs`](../README.md) (`architecture`, `adr`, `api`, `data-model`, `user-flows`, `runbook`, `security`, `testing`, `demo-script`)

### **Levvy — Every lecture, in your language, backed by playable receipts.**
