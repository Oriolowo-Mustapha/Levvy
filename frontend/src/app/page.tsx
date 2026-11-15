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

export default function Home() {
  return (
    <BrowserReady>
      <HomeContent />
    </BrowserReady>
  );
}
function HomeContent() {
  const router = useRouter();
  const [code, setCode] = useState("");
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
      setCode(
        text
          .trim()
          .toUpperCase()
          .replace(/[^A-Z0-9-]/g, "")
          .slice(0, 16),
      );
    } catch {
      setError("Clipboard access was blocked. Type the room code instead.");
    }
  }
  return (
    <>
      <AppHeader active="home" />
      <main id="main" className="home-main">
        <section className="home-hero">
          <AudioVisualizer hero />
          <h1>
            Smart Lecture
            <span>Assistant</span>
          </h1>
          <p>
            Follow lectures with instant captions, translation, and answers
            grounded in what was said.
          </p>
        </section>
        <form className="composer-card" onSubmit={join}>
          <LanguagePairSelector
            source={source}
            target={target}
            onSource={changeSource}
            onTarget={changeTarget}
          />
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
          {error && <ErrorState message={error} />}
          <div className="pill-switch">
            <Link className="button secondary" href="/host">
              Host a lecture
              <Icon name="mic" size={15} />
            </Link>
            <PrimaryActionButton type="submit" disabled={busy}>
              {busy ? "Joining…" : "Join a lecture"}
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
      </main>
    </>
  );
}
