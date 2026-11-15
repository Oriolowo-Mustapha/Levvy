"use client";
import Link from "next/link";
import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import {
  AppHeader,
  BrowserReady,
  AudioVisualizer,
  Icon,
  LanguagePairSelector,
  PillDot,
  PrimaryActionButton,
  RoomCodeInput,
  ErrorState,
} from "../components/ui";
import {
  errorText,
  preference,
  readAuth,
  request,
  roomPath,
  storePreference,
} from "../lib/client";
import type { JoinSessionResponse } from "../lib/api";
import { RecentLecture } from "../components/recent-lecture";
import {
  CtaBand,
  Faq,
  FeatureBento,
  HowItWorks,
  LandingFooter,
  Personas,
  ProofBand,
  ReceiptSection,
} from "../components/landing";

export default function Home() {
  return (
    <BrowserReady>
      <HomeContent />
    </BrowserReady>
  );
}
function HomeContent() {
  const router = useRouter();
  const [mode, setMode] = useState<"levvy" | "meeting">("levvy");
  const [code, setCode] = useState("");
  const [meetingUrl, setMeetingUrl] = useState("");
  const [meetingTitle, setMeetingTitle] = useState("");
  const [source, setSource] = useState(() => preference("source", "en"));
  const [target, setTarget] = useState(
    () => readAuth()?.user.preferredLanguage || preference("language", "fr"),
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const changeSource = (v: string) => {
    setSource(v);
    storePreference("source", v);
  };
  const changeTarget = (v: string) => {
    setTarget(v);
    storePreference("language", v);
  };
  async function join(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    try {
      const auth = readAuth();
      if (mode === "meeting") {
        const trimmedUrl = meetingUrl.trim();
        if (!trimmedUrl) throw new Error("Paste a Google Meet, Zoom, or Teams link.");
        const created = await request<{ sessionId: string; code: string }>(
          "/sessions/online-class",
          {
            method: "POST",
            body: JSON.stringify({
              title: meetingTitle.trim() || "Online meeting",
              meetingUrl: trimmedUrl,
              hostId: auth?.user.id,
            }),
          },
        );
        const joined = await request<JoinSessionResponse>(
          `${roomPath(created.code)}/join`,
          {
            method: "POST",
            body: JSON.stringify({
              displayName: auth?.user.firstName || "Student",
              preferredLanguage: target,
            }),
          },
        );
        sessionStorage.setItem(
          `levvy.participant.${created.code}`,
          joined.participantId,
        );
        router.push(`/session/${encodeURIComponent(created.code)}`);
        return;
      }
      const joined = await request<JoinSessionResponse>(
        `${roomPath(code)}/join`,
        {
          method: "POST",
          body: JSON.stringify({
            displayName: auth?.user.firstName || "Student",
            preferredLanguage: target,
          }),
        },
      );
      sessionStorage.setItem(`levvy.participant.${code}`, joined.participantId);
      router.push(`/session/${encodeURIComponent(code)}`);
    } catch (e) {
      setError(errorText(e));
      setBusy(false);
    }
  }
  async function tryDemo() {
    setBusy(true);
    setError("");
    try {
      const demo = await request<{ sessionId: string; code: string }>(
        "/sessions/demo",
        { method: "POST" },
      );
      const auth = readAuth();
      const joined = await request<JoinSessionResponse>(
        `${roomPath(demo.code)}/join`,
        {
          method: "POST",
          body: JSON.stringify({
            displayName: auth?.user.firstName || "Student",
            preferredLanguage: target,
          }),
        },
      );
      sessionStorage.setItem(
        `levvy.participant.${demo.code}`,
        joined.participantId,
      );
      router.push(`/session/${encodeURIComponent(demo.code)}`);
    } catch (e) {
      setError(errorText(e));
      setBusy(false);
    }
  }
  async function paste() {
    try {
      const text = await navigator.clipboard.readText();
      if (mode === "meeting") {
        setMeetingUrl(text.trim());
        return;
      }
      setCode(
        text
          .trim()
          .toUpperCase()
          .replace(/[^A-Z0-9-]/g, "")
          .slice(0, 16),
      );
    } catch {
      setError(
        mode === "meeting"
          ? "Clipboard access was blocked. Paste the meeting link instead."
          : "Clipboard access was blocked. Type the room code instead.",
      );
    }
  }
  return (
    <>
      <AppHeader active="home" />
      <main id="main" className="home-page">
        <div className="home-top">
          <section className="home-hero">
            <AudioVisualizer hero />
            <h1>
              Every lecture,
              <span>in your language.</span>
            </h1>
            <p>
              Live captions, instant translation, and answers that cite the
              exact second they came from. No account needed.
            </p>
          </section>
          <form className="composer-card" id="join" onSubmit={join}>
          <div className="join-mode-tabs" role="tablist" aria-label="Join method">
            <button
              type="button"
              role="tab"
              aria-selected={mode === "levvy"}
              className={mode === "levvy" ? "active" : ""}
              onClick={() => {
                setMode("levvy");
                setError("");
              }}
            >
              Join on Levvy
            </button>
            <button
              type="button"
              role="tab"
              aria-selected={mode === "meeting"}
              className={mode === "meeting" ? "active" : ""}
              onClick={() => {
                setMode("meeting");
                setError("");
              }}
            >
              Join Google Meet / Zoom with Levvy
            </button>
          </div>
          <LanguagePairSelector
            source={source}
            target={target}
            onSource={changeSource}
            onTarget={changeTarget}
          />
          {mode === "levvy" ? (
            <div className="entry-box">
              <RoomCodeInput value={code} onChange={setCode} />
              <div className="entry-box-tools">
                <button
                  type="button"
                  className="icon-button"
                  aria-label="Paste room code"
                  title="Paste room code"
                  onClick={() => void paste()}
                >
                  <Icon name="paste" size={16} />
                </button>
                <span className="entry-hint">No account needed</span>
              </div>
            </div>
          ) : (
            <>
              <div className="entry-box">
                <span className="entry-label">Meeting link</span>
                <input
                  type="url"
                  required
                  value={meetingUrl}
                  onChange={(e) => setMeetingUrl(e.target.value)}
                  placeholder="https://meet.google.com/abc-defg-hij"
                />
                <div className="entry-box-tools">
                  <button
                    type="button"
                    className="icon-button"
                    aria-label="Paste meeting link"
                    title="Paste meeting link"
                    onClick={() => void paste()}
                  >
                    <Icon name="paste" size={16} />
                  </button>
                  <span className="entry-hint">
                    Levvy joins to transcribe &amp; translate
                  </span>
                </div>
              </div>
              <label className="entry-box">
                <span className="entry-label">Class title (optional)</span>
                <input
                  maxLength={200}
                  value={meetingTitle}
                  onChange={(e) => setMeetingTitle(e.target.value)}
                  placeholder="e.g. CSC 301 Algorithms"
                />
              </label>
            </>
          )}
          {error && <ErrorState message={error} />}
          <div className="pill-switch">
            <Link className="button secondary" href="/host">
              Host a lecture
              <Icon name="mic" size={15} />
            </Link>
            <PrimaryActionButton type="submit" disabled={busy}>
              {busy
                ? mode === "meeting"
                  ? "Sending Levvy…"
                  : "Joining…"
                : mode === "meeting"
                  ? "Join meeting with Levvy"
                  : "Join a lecture"}
              <PillDot />
            </PrimaryActionButton>
          </div>
        </form>
        <div className="pill-switch">
          <button
            type="button"
            className="button secondary"
            disabled={busy}
            onClick={() => void tryDemo()}
          >
            Try demo lecture (K4F-8Q2)
            <Icon name="spark" size={14} />
          </button>
        </div>
        <RecentLecture />
        </div>
        <HowItWorks />
        <ReceiptSection />
        <FeatureBento />
        <Personas />
        <ProofBand />
        <Faq />
        <CtaBand />
        <LandingFooter />
      </main>
    </>
  );
}
