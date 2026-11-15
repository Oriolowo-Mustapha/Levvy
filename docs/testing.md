# Levvy — Test Strategy & Verification Results

Levvy enforces automated verification across three layers: **Backend Unit & Integration Tests (`xUnit`)**, **Frontend Production E2E Browser Tests (`Playwright`)**, and **Automated Accessibility & Multi-Viewport Scans (`@axe-core/playwright`)**.

---

## 1. Summary of Latest Test Results

| Suite | Target | Result | Command |
|---|---|---|---|
| **Backend Build** | `Levvy.slnx` (.NET 10 Release) | **0 Warnings, 0 Errors** | `dotnet build backend/Levvy.slnx -c Release` |
| **Backend Unit & Integration (`xUnit`)** | `Levvy.Tests.dll` | **55 / 55 Passed** | `dotnet test backend/Levvy.slnx -c Release` |
| **Frontend Static Checks** | ESLint + TypeScript (`tsc --noEmit`) + Standalone Build | **Passed** | `npm run lint && npx tsc --noEmit && npm run build` |
| **Frontend E2E + Accessibility (`Playwright`)** | 7 Routes + WebSocket/REST Contracts + WCAG 2.2 AA | **13 / 13 Passed** | `npx playwright test` |

---

## 2. Backend Test Suite (`backend/tests/Levvy.Tests` — 55/55 Passing)

| Test File | Coverage Area |
|---|---|
| `GroundingVerifierTests.cs` | Enforces `cite-or-say-not-covered` (`±1000ms` receipt matching, stripping hallucinated timestamps, downgrading ungrounded answers to `not_covered`, grounded study-pack summaries and flashcards). |
| `AsrAndLlmTests.cs` | Tests `DeepgramAsrStreamer` (`nova-3`, diarization, keyterms, offset accumulation, language detection), `GroqAsrStreamer`, `ResilientAsrStreamer` (3-strike circuit breaker + bidirectional failover), `DeepgramTextToSpeech` (`/v1/speak` MP3 synthesis), and `GroqLlmClient` (`openai/gpt-oss-120b` → `llama-3.3-70b-versatile` fallback). |
| `WebSocketTicketTests.cs` | Verifies 30s single-use host WebSocket tickets (`WebSocketTicketStore`): valid redemption, replay rejection, cross-session rejection, and expiry. |
| `AuthAndDashboardTests.cs` | Tests email registration, PBKDF2 password hashing, login, JWT generation/validation, Google Sign-In verification, and user dashboard aggregation. |
| `SessionLifecycleTests.cs` | Tests `CreateSessionCommand`, `JoinSessionCommand`, `AskQuestionCommand`, `EndSessionCommand`, and `GenerateStudyPackCommand` against in-memory SQLite/EF Core. |
| `OnlineClassTests.cs` | Tests strict host-aware URL parsing (`MeetingUrlParser` audit fix `M7`), Meeting BaaS / Recall.ai payload construction, and webhook lifecycle handling. |

---

## 3. Frontend Browser & Accessibility Suite (`frontend/tests/lecture.spec.ts`)

The Playwright suite boots the production standalone Next.js build and tests real browser media APIs (`MediaRecorder`, `AudioContext`), WebSocket frame synchronization, and accessibility:

1. **Room Code & Language Entry (`/`):** Validates paired source/target language selection and room code navigation.
2. **Live Caption Synchronization & Deduplication (`/session/[code]`):** Verifies WebSocket `caption` and `translation` merging by `offsetMs`, live language switching (`set_lang`), and exponential-backoff reconnection.
3. **Grounded vs. Ungrounded Q&A:** Confirms `Ask Levvy` renders clickable `[mm:ss]` receipt chips only for verified offsets and displays `not_covered` when unsupported.
4. **Audio Receipt Seeking:** Clicking a `[mm:ss]` receipt chip seeks the `<audio>` element to `offsetMs / 1000` and highlights the active transcript segment.
5. **Host Studio & Scoped Ticket (`/host`):** Exercises synthetic microphone capture, 5-second WebM header validation, `POST /sessions/{id}/ws-ticket`, `POST /sessions/{id}/audio` upload, `POST /sessions/{id}/end`, and the **Lecture wrapped up** studio summary (`Back to dashboard`).
6. **Study Pack & Keyboard Flashcards (`/session/[code]/pack`):** Tests on-demand Study Pack generation (`POST /sessions/{code}/pack/generate`), summary citation playback, and keyboard-operated flip cards (`Enter` / `Space`).
7. **Dashboard & 7-Day Auth (`/dashboard`, `/login`, `/signup`):** Verifies Hosted vs. Joined lecture filtering, profile updates, 7-day persistent storage, and `clearAuth()` sign-out.
8. **Multi-Viewport Overflow Matrix:** Asserts zero horizontal scroll (`scrollWidth <= innerWidth`) across all 7 routes at `360px`, `390px`, `430px`, `768px`, `1024px`, `1440px`, and `1920px`.
9. **Automated Accessibility (`@axe-core/playwright`):** Runs WCAG 2.0 A/AA, 2.1 AA, and 2.2 AA scans across all 7 routes at mobile (`390px`) and desktop (`1440px`) viewports with **0 violations**.
