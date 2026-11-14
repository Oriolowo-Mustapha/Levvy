# Levvy — Design Specification

> **Source of truth:** `Levvy-PRD.md` and `levvy-app-hero.html` mockup.  
> **Design ethos:** Calm, high-contrast, zero-clutter accessibility workspace. One ground, one ink, one accent; red is reserved strictly for `LIVE`.

---

## 1. Visual Tokens (`frontend/src/app/globals.css`)

| Token | Hex | Usage |
|---|---|---|
| `--ground` | `#F4F5F7` | App canvas background |
| `--sheet` | `#FFFFFF` | Cards, panels, sidebar surface |
| `--ink` | `#1E2A38` | Primary text & headings |
| `--ink-2` | `#5A6675` | Secondary labels, inactive nav, metadata |
| `--ink-3` | `#8B96A3` | Timestamps, muted hints, placeholders |
| `--line` | `#E4E7EC` | Primary 1px borders & dividers |
| `--line-2` | `#D6DAE1` | Input borders & interactive chip outlines |
| `--tint` | `#EDF3FE` | Active transcript line highlight |
| `--tint-edge` | `#C7D9F8` | Highlighted border |
| `--accent` | `#2563EB` | Primary brand blue, playhead, send button, active radio |
| `--good` | `#1F7A3F` | Grounded/verified status |
| `--live` | `#C0362C` | **LIVE** recording dot & label only |
| `--amber-tint` | `#FDF3E3` | Avatar tint (Amara) |
| `--green-tint` | `#EBF5EE` | Avatar tint |
| `--violet-tint` | `#F3F0FB` | Avatar tint (Sara) |

### Typography
- **Font stack:** `'Segoe UI', -apple-system, BlinkMacSystemFont, sans-serif` (or `Inter` with `tabular-nums` for all timestamps).
- **Timestamps:** Always `font-variant-numeric: tabular-nums` (`43:12`), fixed-width column (`46px` right-aligned) so caption lines never jitter as numbers tick.

---

## 2. Core Screens & Components

### A. Viewer Workspace (`/session/[code]`)
Three-zone desktop layout (collapses cleanly to stacked mobile layout for phone viewers):
1. **Left Sidebar (`208px` on desktop, drawer/top-bar on mobile):**
   - Brand mark (3-bar waveform icon in `--accent`) + **Levvy**
   - Active session title & room code (`K4F-8Q2`)
   - Navigation: `Live transcript`, `Ask Levvy`, `Study pack`
   - Viewer count & language summary at bottom
2. **Primary Canvas — Live Transcript (`flex-1`):**
   - Header with title, `LIVE` dot (`--live`), and connection/staleness indicator (never fake live if WS/ASR lags >5s)
   - Scrollable timestamped lines (`ts` + `text`); active/replaying line gets `--tint` background + `2px` `--accent` left bar
   - **Playback Strip:** Seekable progress bar + **Playable Receipt Chips** (`▶ 41:47`, `▶ 43:12`) — tapping any line or receipt chip seeks the `<audio>` element to `offsetMs / 1000` and plays immediately
3. **Right Rail (`Ask Levvy` + `Subtitles`):**
   - **Ask Levvy Panel:** Single question input + private answer card. Every answer renders **clickable receipt chips** (`▶ 41:47`) or an honest `not_covered` state (*"The lecturer is at 43:12... This hasn't come up yet."*).
   - **Subtitles Panel:** Radio selector for per-viewer language (`Original (EN)`, `Español`, `हिन्दी`, `தமிழ்`, `العربية`, `Français`, `Deutsch`).

### B. Host Console (`/host`)
- Big, unmistakable source picker: **Microphone**, **Share Tab / System Audio** (`getDisplayMedia({ audio: true })`), or **Upload Recording** (demo safety net).
- Prominent room code + copyable link + QR code for phones in the lecture hall.
- Audio level meter + "Checking mic..." warning if no audio detected within ~15s.
- **End Session** button → triggers Study Pack generation.

### C. After-Class Study Pack (`/session/[code]/pack`)
- Clean full transcript (every line clickable to play audio)
- Key-concept summary (each bullet cites `[mm:ss]` receipt)
- Interactive flashcards (flip front/back + `▶ Play receipt` button on every card)

---

## 3. Accessibility & UX Guardrails
- **WCAG AA contrast** on all text/background pairs.
- **Keyboard navigable** receipt chips, subtitle radios, and flashcard flips (`Enter`/`Space`).
- **Zero-install mobile first:** Viewers never sign up or install anything; joining via QR/link lands straight into `/session/[code]`.
- **No uncited AI text:** Frontend refuses to render an `answer` card as grounded if `status === "ok"` has an empty `receipts` array.
