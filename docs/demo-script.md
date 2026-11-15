# Levvy — 5-Minute Hackathon Pitch & Demo Run Sheet

**Live URL:** `https://levvy.tech`  
**Total Time:** 5:00 (3:30 Live Product Demo + 1:00 Architecture/Vultr + 0:30 Hook/Close)

---

## Pre-Demo Checklist (2 Minutes Before Stage)

- [ ] Tab 1 (Lecturer Window): Open `https://levvy.tech/host` signed in as Host.
- [ ] Tab 2 (Student Window — Side-by-Side or Incognito): Open `https://levvy.tech` ready to paste the 6-character room code.
- [ ] Unmute system volume so the audience can hear **Deepgram Aura-2 TTS** and **Audio Receipt playback**.

---

## Run Sheet

### 0:00 – 0:35 | The Problem & Hook (Landing Page `/`)
- **Visual:** Show `https://levvy.tech` with the interactive audio orb and language pair selector.
- **Talk Track:**
  > "Every day, millions of university students sit in lectures delivered in their second language, or scramble to take notes while missing what the professor is actually saying. And when students turn to generic AI tools, those models hallucinate concepts the professor never taught.
  >
  > Meet **Levvy** — the real-time multilingual lecture companion where every live caption is translated in real time, spoken aloud in your language, and every AI answer carries a playable `[mm:ss]` audio receipt."

---

### 0:35 – 1:45 | Act 1: The Host Studio (`/host`)
- **Action:**
  1. In Tab 1 (`/host`), enter title: **`CSC 319 — Distributed Systems`** and keep source language on **`Auto-detect (Deepgram)`**.
  2. Click **Start lecture** and speak 2–3 sentences clearly into the microphone (e.g., *"In distributed systems, the Raft consensus algorithm elects a single leader to manage replicated log entries across nodes. If a follower receives no heartbeat within the election timeout, it starts a new term."*).
  3. Copy the 6-character room code displayed in the top pill.
- **Talk Track:**
  > "As a lecturer, I open the Host Studio and start streaming from my microphone or browser tab. Behind the scenes, my browser acquires a 30-second single-use WebSocket ticket and streams 5-second WebM audio chunks directly through our .NET 10 hot pipe to **Deepgram Nova-3**, while recording continuous audio for Vultr Object Storage."

---

### 1:45 – 3:00 | Act 2: Student Live Captions, Translation, TTS & Full Screen (`/session/[code]`)
- **Action:**
  1. In Tab 2, join the room code and select **Français** (or **Español**) as the target language.
  2. Show the **Top Light Card** streaming live English captions and the **Bottom Dark Card** streaming live translated subtitles via **Groq (`openai/gpt-oss-120b`)**.
  3. Click the **Speaker button** on the dark translated card so the room hears **Deepgram Aura-2** speak the French translation aloud.
  4. Click the **Full-screen button** on the caption card, then press `Esc` to exit.
- **Talk Track:**
  > "Students join with a 6-character code on their phone or laptop. On top, they see live captions; on bottom, real-time translations powered by **Groq's `openai/gpt-oss-120b`**. With one click on the speaker icon, **Deepgram Aura-2 TTS** reads the translated lecture aloud in natural French, and students can expand either card to full screen."

---

### 3:00 – 3:50 | Act 3: Grounded "Ask Levvy" Q&A (Zero Hallucinations)
- **Action:**
  1. In the bottom **Ask Levvy** bar, ask a covered question: *"What happens when a follower receives no heartbeat?"* → Show the answer with its clickable `[00:05]` citation chip.
  2. Next, ask an ungrounded question: *"When is the final exam?"* → Show the **`not_covered`** response.
- **Talk Track:**
  > "What if a student misses a concept mid-lecture? They ask Levvy. Unlike standard chatbots, our deterministic `GroundingVerifier` enforces a strict **cite-or-say-not-covered** rule within ±1 second of real transcript segments. If it was taught, you get a clickable `[mm:ss]` receipt. If it wasn't mentioned in class, Levvy honestly tells you it wasn't covered."

---

### 3:50 – 4:30 | Act 4: Host Wrap-Up & Student Study Pack (`/session/[code]/pack` & `/dashboard`)
- **Action:**
  1. Switch to Tab 1 (Host) and click **End lecture** → Confirm. Point out the clean **Lecture wrapped up** studio state (no student clutter) and click **Back to dashboard** to show **Hosted by you** vs **Joined lectures**.
  2. Switch to Tab 2 (Student), click **Open study pack & flashcards** (or **Generate study pack & flashcards**), flip a flashcard with the keyboard, and click its `[mm:ss]` receipt chip to play the recorded lecture audio from **Vultr Object Storage**.
- **Talk Track:**
  > "When the lecturer ends class, the recording is streamed into **Vultr Object Storage** and the host gets a clean studio wrap-up. Meanwhile, students get a full **Study Pack & Flashcards** where every bullet and flashcard links directly back to the exact second of the professor's voice."

---

### 4:30 – 5:00 | Architecture & Vultr Cloud Close
- **Talk Track:**
  > "Everything you just saw runs in production on **Vultr**:
  > - **Vultr Compute** hosts both our .NET 10 API + WebSocket Hub and our Next.js 16 frontend, deployed automatically via GitHub Actions.
  > - **Vultr Object Storage** streams WebM lecture recordings with S3 SigV4 and HTTP Range seeking.
  > - **Vultr Managed PostgreSQL** persists transcripts, citations, and study packs.
  > - And our `ResilientAsrStreamer` and `GroqLlmClient` provide automatic failover between **Deepgram Nova-3** and **Groq**.
  >
  > Levvy makes every classroom accessible, multilingual, and verifiable. Thank you!"
