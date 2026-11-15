# Levvy — Diagrams as Code (Mermaid)

All diagrams render natively on GitHub.

---

## 1. End-to-End System Architecture

```mermaid
flowchart TB
    subgraph Clients["Browser Clients (Next.js 16 App Router)"]
        Host["Host Studio (/host)\nMediaRecorder WebM + WebAudio"]
        Viewer["Student Workspace (/session/[code])\nLive Captions + Translation + TTS + Ask Levvy"]
        Pack["Study Pack (/session/[code]/pack)\nCited Summary + Flip Flashcards"]
    end

    subgraph Vultr["Vultr Cloud Infrastructure (levvy.tech)"]
        subgraph Compute["Vultr Compute Instance"]
            Web["Next.js Standalone Server\n(/opt/levvy-web)"]
            Api["Levvy.Api (.NET 10 Minimal APIs)\n(/opt/levvy)"]
            Hub["LectureSessionHub\nWebSocket (/ws/{sessionId})"]
            MediatR["CQRS Bus\n(MediatR + FluentValidation)"]
            Hangfire["Hangfire Background Worker\n(Caption Persistence + Study Pack)"]
        end
        PG[("Vultr Managed PostgreSQL\n(EF Core Npgsql)")]
        S3[("Vultr Object Storage\n(S3 SigV4 Recordings)")]
    end

    subgraph AI["External AI & Speech Providers"]
        DG["Deepgram API\nNova-3 (/v1/listen) + Aura-2 (/v1/speak)"]
        Groq["Groq Cloud API\nopenai/gpt-oss-120b + llama-3.3-70b-versatile\n+ whisper-large-v3-turbo"]
    end

    Host -- "1. POST /sessions/{id}/ws-ticket\n2. WSS binary audio chunks" --> Hub
    Host -- "POST /sessions/{id}/audio (WebM)" --> Api
    Viewer -- "WSS /ws/{sessionId}?lang=xx" --> Hub
    Viewer -- "POST /sessions/{code}/ask | /speak" --> Api
    Pack -- "GET/POST /sessions/{code}/pack" --> Api

    Hub -- "Hot Pipe (bypasses MediatR)" --> DG
    DG -. "Circuit-breaker failover" .-> Groq
    Hub -- "Per-language subtitle translation" --> Groq
    Hub -- "Broadcast caption & translation" --> Viewer
    Hub -- "Enqueue CaptionFinalized" --> Hangfire

    Api --> MediatR
    MediatR -- "Grounded Q&A + Study Pack\n(GroundingVerifier)" --> Groq
    Api -- "Synthesize speech (/v1/speak)" --> DG
    Api -- "Stream PUT / Range GET" --> S3
    MediatR --> PG
    Hangfire --> PG
```

---

## 2. Live Audio Hot Pipe & Translation Fan-Out

```mermaid
sequenceDiagram
    autonumber
    actor Host as Lecturer (/host)
    participant Hub as LectureSessionHub (/ws/{sessionId})
    participant ASR as ResilientAsrStreamer (Deepgram Nova-3 / Groq Whisper)
    participant LLM as GroqLlmClient (openai/gpt-oss-120b)
    participant HF as Hangfire (CaptionPersistenceJob)
    actor Student as Student (/session/[code])

    Host->>Hub: Connect WSS with single-use ?role=host&ticket=...
    Student->>Hub: Connect WSS as viewer (?lang=fr)
    loop Every ~5s WebM audio chunk
        Host->>Hub: Binary WebM audio chunk
        Hub-->>Host: { "type": "asr_status", "state": "processing" }
        Hub->>ASR: TranscribeChunkAsync(chunk, chunkStartOffsetMs, sourceLang)
        ASR-->>Hub: TranscriptSegment[] (OffsetMs, Text, DetectedLanguage)
        Hub-->>Host: { "type": "asr_status", "state": "ready" }
        Hub->>Student: { "type": "caption", "ts": "00:05", "offsetMs": 5000, "text": "..." }
        Hub->>HF: Enqueue PersistSegmentAsync (append-only, idempotent)
        par Active Viewer Languages (e.g. "fr")
            Hub->>LLM: TranslateAsync(segment.Text, sourceLang, "fr")
            LLM-->>Hub: Translated subtitle
            Hub->>Student: { "type": "translation", "for": "fr", "offsetMs": 5000, "text": "..." }
        end
    end
```

---

## 3. Strict Grounding Pipeline (`cite-or-say-not-covered`)

```mermaid
flowchart LR
    Q["Student Question\nPOST /sessions/{code}/ask"] --> Load["Load Session\nTranscriptSegments"]
    Load --> CheckEmpty{"Any segments\nrecorded?"}
    CheckEmpty -- "No" --> NotCov["Return status: not_covered\n(No LLM call wasted)"]
    CheckEmpty -- "Yes" --> Prompt["GroqLlmClient\nPrimary: openai/gpt-oss-120b\nFallback: llama-3.3-70b-versatile"]
    Prompt --> Verify["GroundingVerifier.VerifyAnswer()\nMatch cited [mm:ss] within ±1000ms\nof real TranscriptSegment.OffsetMs"]
    Verify --> ValidCitations{"≥1 valid citation\nmatched?"}
    ValidCitations -- "Yes" --> Answered["Persist Answer (status: answered)\n+ AnswerCitations (OffsetMs)"]
    ValidCitations -- "No" --> Downgrade["Downgrade to status: not_covered\nStrip hallucinated citations"]
```

---

## 4. CI/CD Deployment Pipeline to Vultr

```mermaid
flowchart LR
    Push["git push origin main"] --> GHA["GitHub Actions\n(deploy-vultr.yml)"]
    GHA --> Test["dotnet test Levvy.slnx\n(55/55 unit & integration tests)"]
    Test --> BuildAPI["dotnet publish Levvy.Api\n(.NET 10 Release)"]
    Test --> BuildWeb["npm ci && npm run build\n(Next.js 16 Standalone)"]
    BuildAPI --> SCP1["SCP to Vultr Compute\n/opt/levvy"]
    BuildWeb --> SCP2["SCP to Vultr Compute\n/opt/levvy-web"]
    SCP1 --> Systemd["SSH: Restart systemd services\nlevvy-api & levvy-web"]
    SCP2 --> Systemd
```
