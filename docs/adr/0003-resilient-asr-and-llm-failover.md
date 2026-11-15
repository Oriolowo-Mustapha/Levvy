# ADR 0003: Resilient Multi-Provider ASR & LLM Failover

- **Status:** Accepted
- **Date:** 2026-11-15

## Context
Live classroom demos cannot afford single-provider outages, rate limits, or model deprecations during a lecture. Both Speech-to-Text (ASR) and LLM inference are on the critical path for live captions, translations, Q&A, and study packs.

## Decision
1. **ASR Circuit Breaker & Bidirectional Failover (`ResilientAsrStreamer`):**
   - Supports configurable primary provider (`ASR_PROVIDER=deepgram` using Deepgram `nova-3` `/v1/listen` with smart formatting, speaker diarization, language detection, and keyterms; or `ASR_PROVIDER=groq` using `whisper-large-v3-turbo`).
   - If the primary ASR provider fails on a chunk, `ResilientAsrStreamer` automatically retries the same audio stream against the secondary provider.
   - After **3 consecutive primary failures**, a circuit breaker trips for **60 seconds**, routing chunks directly to the healthy backup provider to avoid timeout cascades.
2. **LLM Primary + Fallback Model Chain (`GroqLlmClient`):**
   - Uses `openai/gpt-oss-120b` as the primary model (`Groq:AnswerModel` / `GROQ_ANSWER_MODEL`) and `llama-3.3-70b-versatile` as fallback (`Groq:FallbackModel` / `GROQ_FALLBACK_MODEL`).
   - Automatically fails over on both HTTP error responses (`4xx`/`5xx`) and client-side request timeouts (`TaskCanceledException`).
3. **Multilingual TTS (`DeepgramTextToSpeech`):**
   - Uses Deepgram Aura-2 (`/v1/speak`) with automatic language-to-voice resolution (`en`, `es`, `fr`, `de`, `it`, `ja`, `nl`) so students can listen to both original and translated captions.

## Consequences
- **Positive:** Transient provider errors or model availability changes self-heal per request without dropping a live lecture session.
- **Positive:** Changing models or primary providers requires only `.env` configuration updates.
