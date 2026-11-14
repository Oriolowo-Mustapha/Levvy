# Levvy frontend

A responsive Next.js academic accessibility workspace based on the supplied AI Translator & Live Captions mobile reference.

## Run locally

1. Install Node.js 22 or newer and run `npm ci` in this directory.
2. Copy `.env.example` to `.env.local`; set the public API and WebSocket addresses. These values are compiled into the client at build time. Never add server API keys or `WS_HOST_KEY`.
3. Configure and run the .NET API and PostgreSQL as documented in the repository. A working API is required for sessions, authentication, captions, answers and study packs.
4. Run `npm run dev`, then open `http://localhost:3000`.

Students can join by room code without an account. Hosts sign in before creating a room. Source language is supplied to the ASR pipe by the host; caption language is per viewer.

## Production

Run `npm run build`, then `npm start`. The postbuild script copies public and static assets alongside Next's standalone server. Set `PORT` and `HOSTNAME` as needed. Deploy the matching backend changes before using browser hosting: signed-in hosts obtain a single-use, session-scoped 30-second WebSocket ticket. A normal user JWT is never placed in the WebSocket URL.

Google sign-in is optional: set `NEXT_PUBLIC_GOOGLE_CLIENT_ID` and the matching backend `GOOGLE_CLIENT_ID`. The button appears only when configured. Google Identity and provider credentials must be tested in the deployment environment.

## Routes

- `/`: language preferences, room-code join, host entry.
- `/login`, `/signup`: email authentication and optional Google Identity.
- `/dashboard`: actual user lectures, search, filters, study packs and profile preferences.
- `/host`: authenticated room creation, joining link, local QR code, microphone/tab audio capture, recording save and session end confirmation.
- `/session/[code]`: original and translated caption cards, transcript, language and text-size settings, Ask Levvy and audio playback.
- `/session/[code]/pack`: summary citations, reversible flashcards and full transcript.

## Recording and service limits

The host records WebM/Opus only after browser capability checks. Microphone visualization reads actual Web Audio input levels. Every ASR frame is a complete WebM recording; a second continuous MediaRecorder produces the final playback file. Audio tracks, audio contexts, intervals and animation frames are released on stop and unmount. The recording timer excludes idle time.

Browser-tab sharing requires a browser that supports `getDisplayMedia` audio; users must select a tab and enable sharing its audio. Actual OS/browser support varies. The upload endpoint stores existing WebM audio for playback; it does not transcribe uploaded files. The interface says this explicitly. Pause/resume and server-side file transcription are not offered. Stopped recordings can be saved, then the lecture ended.

Original and translated captions match by `offsetMs`. Changes of subtitle language affect incoming captions; historical translations are not synthesized. Unknown source language is labeled “Original speech.” Speakers and translated speech synthesis are not fabricated when absent from server contracts.

ASR emits additive `asr_status` frames (`processing`, `ready`, `error`) to hosts. The host waits for outstanding ASR frames before ending; service failure is visible. Storage and PostgreSQL/Hangfire/provider behavior still require deployment validation. Study-pack generation remains asynchronous; the pack screen offers retry when unavailable.

## Verification

```sh
npm ci
npm run lint
npx tsc --noEmit
npm run build
npm test
```

Playwright uses installed Chrome on Windows, and bundled Chromium elsewhere (`npx playwright install --with-deps chromium`). Override `PLAYWRIGHT_CHANNEL` if needed. To test the standalone production server, build first and set `PLAYWRIGHT_USE_PRODUCTION=true` and `PLAYWRIGHT_BASE_URL=http://127.0.0.1:3100` before `npm test`.

Tests use clearly identified REST/WebSocket fixtures, browser-generated synthetic microphone input, and automated axe checks. They exercise frontend contracts and browser APIs, not external AI/storage providers. Run real microphone, tab sharing, OAuth and multi-device service tests separately against a configured backend. The public API returned 502 during this implementation session.
