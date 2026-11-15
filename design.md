# Levvy — Design & Frontend Architecture Specification

> **Visual direction update (Nov 2026):** the app UI now follows a monochrome
> "AI translator & live captions" reference: soft grey canvas (`#f1f1f1`),
> white cards, one near-black card (`#0b0b0b`) for translated captions with a
> notched swap button, round icon buttons, pill controls, black pill CTAs,
> Inter, and halftone dot visuals (glass orb on `/`, `/host`, auth; dot sphere
> + pager-dot level meter on the live host screen). Tokens live in
> `frontend/src/app/globals.css`. Where §1–§3 below (yellow accent, 4px
> corners, editorial homepage sections, eyebrow-free rules aside) conflict
> with that, the code and this note win; routes, components, API mapping and
> the accessibility/honesty rules still apply.

> **Art Direction:** `sentence-as-layout` (page, seed `698354275`) + `Editorial greeting` / `Open plane` (app screens, seed `878022159`), distilled via `design-taste-frontend` and verified against `web-design-guidelines` and `vercel-react-best-practices`.  
> **Idea Statement:** *On this page, a live lecture’s timestamped caption strip (`▶ 43:12`) becomes the missing word inside the poster headline.*  
> **Primary Live Interaction:** Instant room-code entry + live paired-language caption preview directly on `/`, and clickable audio-receipt playback (`offsetMs / 1000`) across every transcript, Q&A answer, and flashcard.

---

## 1. Non-Negotiable House Rules

Every route (`/` and all app screens) must obey these rules—both for craft and for automated `lint.mjs` / `inspect.mjs` gates:

1. **Zero decorative clutter:**
   - **No eyebrow labels** above headings (`A LITTLE CLARITY…`, `NEW ·`, tracked uppercase kickers). Put the context in the heading or first sentence.
   - **No index numbering** (`01 /`, `02 /`, `Step 1`). Order is shown by layout position, scale, or real `<ol>` semantics.
   - **No thin decorative rules or 1px card grids** on the homepage. Separate sections with space (`120–240px`) and flat tonal steps (`--bg` vs `--surface` vs `--field-1`). (Real `<table>` and `<input>`/`<button>` controls inside `data-ui` fragments may keep 1px borders.)
   - **No gradients, glows, or glassmorphism** (`backdrop-filter`, floating pill navs, gradient buttons, gradient text).
   - **No shadows at rest.** Shadows appear *only* on truly floating layers (dropdown menus, modal dialogs, mobile nav sheets).
   - **No styled accent words inside headlines.** Never italicize, recolor, or underline a single word in an `<h1>`–`<h6>`. One face, one ink.
   - **No icon-topped feature cards or emoji.**
   - **No monospace outside `<code>`/`<pre>`.** All timestamps (`43:12`) use **Hanken Grotesk** with `font-variant-numeric: tabular-nums`.
2. **Typography & Frame Discipline:**
   - **Two lines max** for every `h1`–`h6` at every viewport (`1440px`, `768px`, `390px`).
   - **Equal outer margins** (`clamp(16px, 3vw, 32px)`) across the entire page. Never huddle content on one side with an empty void opposite.
   - **Running copy** is always full `--ink` (`#0d2a1c`) at `≥ 16px`; `--muted` (`#5f6b64`) is strictly for timestamps, metadata, and secondary captions.
3. **Accessibility & AI Honesty:**
   - **WCAG AA contrast** (`≥ 4.5:1` body, `≥ 3:1` large display) on every surface.
   - **Keyboard first:** Every receipt chip (`▶ 41:47`), subtitle radio, flashcard flip, and room-code input works via `Tab`, `Enter`, and `Space` with visible `:focus-visible` rings.
   - **Honest live state:** Never show `LIVE` if WebSocket/ASR lags `> 5s`; display a clear staleness notice instead.
   - **No uncited AI text:** Frontend must refuse to render an Ask Levvy `ok` answer as grounded if `receipts.length === 0`. Honest `not_covered` answers render distinctly without fabricated timestamps.

---

## 2. Design Tokens & Type System (`frontend/src/app/globals.css`)

Both the public homepage (`/`) and all app screens share **one typeface** and **one palette**:

### 2.1 Typography — Hanken Grotesk

Load via `next/font/google` (`Hanken_Grotesk`, weights `["400", "500", "600"]`, `display: "swap"`).

| Role | Token | Size / Formula | Weight | Leading | Tracking | Usage |
|---|---|---|---|---|---|---|
| Hero Display | `--step-hero` | `clamp(3.25rem, 9.5vw, 9.5rem)` | `500` | `0.92` | `-0.05em` | Homepage `<h1>` only |
| Section Head | `--step-head` | `clamp(2rem, 1.3rem + 3.2vw, 4rem)` | `500` | `0.98` | `-0.04em` | Homepage section `<h2>`s, CTA |
| Lead / Statement | `--step-lead` | `clamp(1.25rem, 1.1rem + 0.7vw, 1.625rem)` | `400` / `500` | `1.35` | `-0.015em` | Subheads, FAQ questions |
| Body | `--step-body` | `clamp(1rem, 0.96rem + 0.2vw, 1.125rem)` | `400` | `1.5` | `-0.005em` | Paragraphs, answers, captions (`md`) |
| Small / Meta | `--step-small` | `clamp(0.8125rem, 0.79rem + 0.1vw, 0.875rem)` | `400` / `500` | `1.4` | `0` | Timestamps (`tabular-nums`), nav links, labels |

Caption font-size overrides (user preference `UserProfileDto.captionFontSize`):
- `sm`: `0.9375rem` (`15px`)
- `md`: `1.0625rem` (`17px`) — default
- `lg`: `1.25rem` (`20px`)
- `xl`: `1.5rem` (`24px`)

### 2.2 Colour Tokens (`Bottle Ink & Signal Yellow`)

```css
:root {
  /* Core Surfaces & Ink */
  --bg: #f7f7f7;          /* Off-white paper canvas */
  --surface: #ebebeb;     /* Tonal step surface (panels, inputs, bars) */
  --sheet: #ffffff;       /* Elevated app panel inside --bg */
  --ink: #0d2a1c;         /* Bottle-green near-black primary text */
  --muted: #5f6b64;       /* Secondary metadata, timestamps, hints */
  --line: #dcdedf;        /* App-screen structural hairline (not homepage ornament) */

  /* Brand Accent & Saturated Field */
  --accent: #f6e016;      /* Signal yellow — primary CTA & active highlight */
  --accent-ink: #0d2a1c;  /* Text on --accent */
  --field-1: #f6e016;     /* Full-bleed yellow footer chapter */
  --field-1-ink: #0d2a1c;
  --field-dark: #0d2a1c;  /* Full-bleed dark closing CTA & translated caption dark card */
  --field-dark-ink: #f7f7f7;

  /* App-Screen Semantic Tokens (<= 2% screen budget) */
  --tint: #eef3ec;        /* Active transcript line / selected row */
  --good: #1f7a3f;        /* Grounded receipt / verified state */
  --live: #c0362c;        /* LIVE recording dot & active recording status ONLY */
  --warn: #9a5b13;        /* Mic silence >15s / WS lag >5s warning */
}
```

### 2.3 Geometry & Control Rules

- **Button shape:** Square-cornered (`border-radius: 4px`) or pill (`9999px`)—pick **one** across the whole app. **Decision: `4px` crisp architectural corners** on buttons/inputs, **`14px` radius** reserved exclusively for the inline hero media strip and `data-ui` preview frames.
- **Outer container:** `max-width: 1360px` on homepage (`1152px` on app settings/dashboard), `padding-inline: clamp(16px, 3vw, 32px)`.

---

## 3. Homepage Specification (`/`) — `sentence-as-layout`

The homepage is both a **complete product showcase** and a **zero-friction lecture entry point**. Every section uses a distinct layout blueprint (never repeating two adjacent layouts).

### 3.1 Navigation Bar (`AppHeader`)
- Plain single-row bar (`height: 64px`, no background blur, no floating pill).
- **Left:** `Levvy` wordmark + minimal 3-bar waveform SVG mark in `--ink`.
- **Right:** 3–4 plain text links (`Join`, `Host`, `Dashboard` or `Sign in`) + 1 solid `--accent` (`#f6e016`) CTA button (`Host a Lecture` or `Open Workspace`).
- **Mobile (`< 768px`):** Wordmark left, single action + menu drawer toggle right.

### 3.2 Hero — `Sentence with a photo strip` + Live Room Entry
- **Headline (`<h1>`):** 5 words at `--step-hero`, single line on desktop (2 lines on mobile), `display: flex; align-items: baseline; gap: 0.22em`:
  - Copy: `Every` `[inline live strip]` `word` `understood.`
  - **Inline strip (`flex: 1`, `height: 0.85em`, `min-width: 140px`, `border-radius: 14px`, `overflow: hidden`):** Displays a wide lecture-hall photograph with a crisp, bottom-aligned live caption overlay bar (`▶ 43:12 · "…gradient descent converges when…"`) that gently updates between `EN → FR` / `ES` / `AR` without obscuring the photo.
- **Below the headline (`margin-top: 32px`):**
  - **Left (cols 1–6):** One supporting sentence (`22px`, `max-width: 44ch`): *"Live captions, instant translation, and audio-verified answers for every lecture hall and online class."*
  - **Right (cols 7–12):** **The Live Join Strip** (`RoomJoinBar`, `data-ui`):
    - 6-character Room Code input (`K4F-8Q2`, auto-uppercase, `aria-label="Lecture room code"`)
    - Source → Target Language pair selector (`EN → FR` / `ES` / `HI` / `TA` / `AR` / `DE`)
    - Solid `--accent` submit button: `Join Lecture`
    - Secondary text link beneath: `Or start hosting a lecture →` (`/host`) and `RecentLecture` shortcut if `localStorage` has a prior session.

### 3.3 Section 1 (`intro`) — `Headline crossing onto a photograph`
- **Grid:** 12-column split.
- **Right (`cols 7–12`, flush right):** Tall portrait/4:5 photograph of students in a lecture hall (or archival university auditorium photo from `assets.mjs`), entering with the signature **Shutter Image** reveal (`clip-path` wipe + `1.12 → 1.0` scale over `1.1s`).
- **Left (`cols 1–8`, overlapping top-left corner of photo):** Two-line `<h2>` at `--step-head`:
  - *"Built for loud halls* / *and fast speakers."*
- **Left (`cols 1–5`, below `<h2>`):** Concise 22-word paragraph explaining zero-install mobile joining via QR/code and per-viewer subtitle language independence.

### 3.4 Section 2 (`showcase`) — `One frame, three distances` (Interactive `data-ui` Product Showcase)
- **Layout:** Tall scroll-pinned or tabbed 3-stage showcase (`100svh` sticky stage on desktop, stacked on mobile) framing a **real `data-ui` interactive mockup** of `/session/[code]`.
- **Headline split:** First half above the frame (`"Answers backed by"`), second half below (`"audio receipts."`).
- **Three Stages (scroll-triggered or clickable step tabs):**
  1. **Overview (`100%`):** Full 3-zone viewer workspace (Left room rail, Center live transcript, Right Ask Levvy + Subtitles).
  2. **Paired Translation (`180%` zoom):** Focuses on live light Original (`EN`) + deep `--field-dark` Translated (`FR` / `AR`) caption stream with `tabular-nums` timestamps.
  3. **Verified Q&A (`320%` zoom):** Focuses on an Ask Levvy response showing clickable `▶ 41:47` receipt chip (plays a real 5-second Web Audio / sample clip right on the homepage!) alongside the honest `not_covered` state (*"The lecturer is at 43:12. This hasn't come up yet."*).

### 3.5 Section 3 (`how-it-works`) — `Stages as one bar`
- **Heading (`<h2>`):** `"From mic to study pack."` (flush left above bar).
- **Diagram (`data-ui`, cols 1–12, `height: 22vh` desktop / vertical stack mobile):** 4 proportional flat-colour segments separated by `6px` gaps (no borders):
  1. `Join in 5 sec` (`12%` width, `--surface`) — *"Scan the hall QR or enter the 6-character room code."*
  2. `Captions < 1s` (`30%` width, `--tint`) — *"Stream mic, tab audio, or the Google Meet bot live."*
  3. `Ask with proof` (`33%` width, `--accent`) — *"Every AI answer links to playable audio timestamps."*
  4. `Pack in 30s` (`25%` width, `--field-dark` with light ink) — *"End class to generate cited summaries and flashcards."*

### 3.6 Section 4 (`features`) — `Rows with margin photographs`
- **Axis:** All text hangs from **column 5** (`~35%` left offset; full-width on phone). Columns 1–4 remain open breathing space.
- **Section `<h2>`:** `"One workspace for every learner."`
- **4 Benefit Rows** separated by `96–120px` vertical air (no lines/cards), each with title (`--step-lead`), `34ch` paragraph, and an unequal-aspect right-margin photograph (`240px` 4:5, `320px` 3:2, `180px` 1:1, `280px` 4:3):
  1. **Independent language per seat** — Each student picks `EN`, `ES`, `FR`, `HI`, `TA`, `AR`, or `DE` without affecting anyone else in the room.
  2. **Google Meet & online classes** — Paste a Meet URL in `/host` to dispatch Levvy’s automated bot and stream captions into the same room code.
  3. **Deaf & hard-of-hearing ergonomics** — Adjustable caption sizes (`sm`–`xl`), zero-jitter `tabular-nums` timestamps, and calm contrast.
  4. **Flashcards with audio provenance** — Every generated card carries a `▶ Play receipt` button straight to the moment the lecturer explained it.

### 3.7 Section 5 (`faq`) — `Large questions, answers beside`
- **Rule:** **Every answer is open; nothing collapses** (no accordion hiding text).
- **Grid:** 12 columns. Row 1 left cell (`cols 1–6`) holds `"Common questions"` in `--muted`. Subsequent 6 rows (`72–88px` vertical gap) place the question in `cols 1–6` (`--step-lead`, max 2 lines) and the open answer in `cols 8–12` (`17px`, `40ch`, baseline-aligned):
  1. *Do students need to download an app or create an account?*
  2. *What happens if I ask Levvy something the lecturer hasn’t covered?*
  3. *How does Levvy work for Google Meet or Zoom recordings?*
  4. *Which caption and translation languages are supported?*
  5. *Can I adjust caption text size and high-contrast modes?*
  6. *How are Study Packs and flashcards generated after class?*

### 3.8 Section 6 (`cta`) — `Question at both edges`
- Full-bleed `--field-dark` (`#0d2a1c`) `100svh` band.
- Centered 2-line `<h2>` split across top and bottom edges (`"Ready for your"` / `"next lecture?"`), framing two solid `200×56px` buttons at optical centre:
  - `Join a Room` (`--accent` fill, focuses/scrolls to join bar or opens `/session`)
  - `or` (`--muted`)
  - `Host a Lecture` (`--sheet` fill, navigates to `/host`)
- One-line caption below: *"No installation required. Works on any phone or laptop browser."*

### 3.9 Section 7 (`footer`) — `Contact as the headline`
- Full-bleed `--field-1` (`#f6e016`) `≥ 85svh` closing field (`--field-1-ink: #0d2a1c`).
- **Top row:** `Levvy` wordmark + short mission note left; `Back to top ↑` link right.
- **Lower display link (`8–9vw`):** `hello@levvy.tech` (breaks cleanly before `@` on mobile).
- **Bottom meta row (`14px`):** Links left (`Host`, `Dashboard`, `Sign in`, `Privacy`) · Photo credits center · `HackGlobal 2026` right.

---

## 4. Required App Pages & Routes

All app screens use the **`Editorial greeting` / `Open plane`** shell (`240px` left sidebar on desktop, collapsing to a `48px` top bar + slide-over sheet on mobile) with `--bg` (`#f7f7f7`), `--sheet` (`#ffffff`), `--ink` (`#0d2a1c`), and `< 2%` `--accent` budget.

| Route | Purpose | Primary User | Key Layout & Elements |
|---|---|---|---|
| `/` | Public Homepage & Room Entry | Student / Guest / Host | `sentence-as-layout` full homepage (§3) |
| `/login` | Sign In | Returning Host / Student | Split clean auth card: Email + Password form, Google Sign-In (`GOOGLE_CLIENT_ID`), link to `/signup` |
| `/signup` | Create Account & Accessibility Setup | New Host / Student | Name, Email, Password, Role (`student` / `instructor`), Preferred Language, Accessibility Mode (`standard` / `deaf_hoh` / `international`), Caption Size (`sm`–`xl`), plus Google OAuth |
| `/dashboard` | User Home, History & Preferences | Authenticated User | `Editorial greeting` (`"Morning, <FirstName>. <N> lectures saved"`), 4 tabular count tiles (`Sessions`, `Live Now`, `Study Packs`, `Recordings`), `9 + 3` / `6 + 6` grid of `DashboardSessionItemDto[]` and `DashboardStudyPackItemDto[]`, inline two-column Accessibility Settings form (`UpdateUserProfileRequest`) |
| `/host` | Lecture Capture & Broadcast Console | Instructor / Host | Session title + create room → prominent `code` + Copy URL + `JoinQr` canvas; 4-source picker (**Microphone**, **Share Tab / System Audio** `getDisplayMedia`, **Upload Audio**, **Google Meet Bot** URL input + live status/leave); real-time audio VU meter + 15s silence warning; **End Session** button triggering Study Pack generation |
| `/session/[code]` | Live Lecture Viewer Workspace | Student (zero-install) | 3-zone desktop / stacked mobile: **(1) Left `240px` rail** (title, room code, nav: `Live transcript` / `Ask Levvy` / `Study pack`, live viewer count); **(2) Center Transcript** (`LIVE` `--live` dot or `> 5s` stale warning, scrollable `tabular-nums` lines, active `--tint` line + playback strip `<audio>` seeking to `offsetMs / 1000`); **(3) Right Rail** (`Ask Levvy` input + receipt-cited answer cards + `Subtitles` language radio & `captionFontSize` control) |
| `/session/[code]/pack` | After-Class Study Pack | Student / Host | Header with export/print + audio player; **Key-Concept Summary** with inline `[mm:ss]` clickable receipt chips; **Interactive Flashcards** (`Enter`/`Space` flip front/back + `▶ Play receipt` button); **Full Clickable Transcript** |

---

## 5. Component Inventory (`frontend/src/components/`)

| Component | File | Props / Responsibilities | Required States & A11y |
|---|---|---|---|
| `AppHeader` | `ui.tsx` | `active?: "home" \| "host" \| "dashboard" \| "login"` — Plain top bar on `/` and auth pages; collapses to mobile drawer | Visible focus ring, `aria-current="page"` |
| `AppSidebar` | `app-shell.tsx` | `sessionTitle?`, `code?`, `activeNav`, `participantCount?` — `240px` Open-plane sidebar for `/dashboard`, `/host`, `/session/[code]` | `48px` mobile top bar + accessible dialog drawer |
| `RoomJoinBar` | `room-join-bar.tsx` | `initialSource`, `initialTarget` — 6-char room code input + language pair selector + submit | Validation error inline (`aria-describedby`), loading state |
| `AudioVisualizer` | `ui.tsx` | `level?: number`, `live?: boolean`, `variant?: "hero-strip" \| "vu-meter"` — Canvas/SVG waveform bars | Respects `prefers-reduced-motion` (static bars) |
| `LiveTranscriptList` | `lecture.tsx` | `lines: TranscriptLineDto[]`, `activeOffsetMs`, `fontSize: "sm"\|"md"\|"lg"\|"xl"`, `onSeek(offsetMs)` | `tabular-nums` 48px timestamp column, `aria-live="polite"`, keyboard seek |
| `AudioReceiptChip` | `receipt-chip.tsx` | `offsetMs: number`, `active?: boolean`, `onPlay(offsetMs)` — Renders `▶ mm:ss` | `<button type="button">`, `aria-label="Play audio at mm:ss"` |
| `AskLevvyPanel` | `ask-levvy.tsx` | `sessionCode`, `onSeek(offsetMs)` — Question form + grounded answer card | Enforces `receipts.length > 0` for `status === "ok"`; distinct `not_covered` card; loading & error states |
| `SubtitleControls` | `subtitle-controls.tsx` | `language`, `fontSize`, `onLanguageChange`, `onFontSizeChange` | `radiogroup` keyboard navigation |
| `HostSourcePicker` | `host-source-picker.tsx` | `onSelectMic`, `onSelectTab`, `onUploadFile`, `onDispatchMeetBot` | Clear permission-denied error + 15s mic-silence warning (`--warn`) |
| `JoinQr` | `join-qr.tsx` | `url: string`, `code: string` — Dynamically imported QR canvas + copy link button | `aria-live="polite"` `"Copied"` confirmation |
| `FlashcardDeck` | `flashcard-deck.tsx` | `cards: FlashcardDto[]`, `onPlayReceipt(offsetMs)` — Flippable cards | `Enter`/`Space` flip, `aria-pressed`, arrow-key navigation |
| `AuthForm` | `auth-form.tsx` | `mode: "login" \| "signup"` — Local form + Google Sign-In button | Inline field validation via `aria-describedby`, error banner |

---

## 6. Motion & Performance Guidelines

- **Signature Move (Homepage only):** `Shutter Image` on the Section 1 lecture-hall photograph (`clip-path: inset(0 0 100% 0)` → `inset(0 0 0% 0)` + `scale(1.12)` → `scale(1)` over `1100ms cubic-bezier(0.16, 1, 0.3, 1)`, triggered once via `IntersectionObserver`).
- **Section Entrances:** Heading → body → media staggered by `70ms`, `transform: translateY(20px)` + `opacity: 0 → 1`, triggered once.
- **Reduced Motion:** `@media (prefers-reduced-motion: reduce)` zeroes all transforms/transitions and keeps content visible immediately.
- **React / Next.js Performance (`vercel-react-best-practices`):**
  - Keep `/` shell and static copy server-rendered where possible; isolate interactive `RoomJoinBar`, `Showcase`, and `AudioVisualizer` into lean `"use client"` leaves.
  - Dynamically import `qrcode` (`JoinQr`) and heavy audio/chart utilities so `/` initial JS stays minimal.
  - Use `loading="lazy"` on below-the-fold photographs; never lazy-load the hero strip image.

---

## 7. API & WebSocket Contract Mapping (`frontend/src/lib/api.ts`)

- **Auth & Profile:** `POST /auth/register`, `POST /auth/login`, `POST /auth/google`, `GET /auth/me`, `PUT /users/me/profile`, `GET /users/me/dashboard`
- **Sessions & Hosting:** `POST /sessions`, `POST /sessions/{code}/join`, `GET /sessions/{code}`, `POST /sessions/{code}/end`, `POST /sessions/{code}/audio`
- **Online Class / Meet Bot:** `POST /sessions/{code}/meet-bot`, `GET /sessions/{code}/meet-bot`, `POST /sessions/{code}/meet-bot/leave`
- **Live Captions, Q&A & Study Pack:** `GET /sessions/{code}/transcript`, `POST /sessions/{code}/ask`, `GET /sessions/{code}/pack`, `GET /sessions/{code}/audio`
- **WebSocket (`/ws/sessions/{code}`):** Streams `transcript`, `translation`, `status` (staleness check `> 5s`), and `ended` events.

---

## 8. Verification Gate (Before Marking Frontend Complete)

1. `node <taste-skill>/scripts/lint.mjs frontend/src` → **0 banned patterns** (no eyebrows, no `01/`, no gradients/glass, no styled headline spans).
2. `node <taste-skill>/scripts/inspect.mjs http://localhost:3000` (production `next build && next start`) → **0 FAIL** across `1440px`, `768px`, and `390px`.
3. `npm run build` in `frontend/` → **0 TypeScript / Next.js build errors**.
4. `npx playwright test` (`frontend/tests/lecture.spec.ts`) → all E2E flows pass.
