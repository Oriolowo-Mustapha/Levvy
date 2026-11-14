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
  return (
    <>
      <AppHeader active="home" />
      <main id="main" className="home-main">
        <div className="home-topline">
          <span className="eyebrow">
            A LITTLE CLARITY. A WORLD OF POSSIBILITY.
          </span>
          <Link className="subtle-link" href="/dashboard">
            <Icon name="history" size={16} />
            Lecture history
          </Link>
        </div>
        <div className="home-composition">
          <section className="assistant-intro">
            <div className="intro-kicker">
              <span />
              Made for every mind
            </div>
            <AudioVisualizer hero />
            <h1>
              Your smart
              <br />
              lecture <span>assistant.</span>
            </h1>
            <p>
              Follow every thought. Understand every word.
              <br className="desktop-break" /> Live captions, translation and a
              little help from AI.
            </p>
            <div className="feature-pills">
              <span>
                <Icon name="headphones" size={14} />
                Real-time captions
              </span>
              <span>
                <Icon name="spark" size={14} />
                Grounded in your lecture
              </span>
            </div>
          </section>
          <section className="entry-card" aria-labelledby="entry-title">
            <div className="card-top">
              <span className="small-icon">
                <Icon name="book" />
              </span>
              <span className="eyebrow">YOUR NEXT MOMENT OF CLARITY</span>
              <span className="card-number">01 /</span>
            </div>
            <h2 id="entry-title">A seat in the conversation.</h2>
            <p>
              Choose your languages, enter your room code,
              <br className="desktop-break" /> and make the lecture your own.
            </p>
            <LanguagePairSelector
              source={source}
              target={target}
              onSource={changeSource}
              onTarget={changeTarget}
            />
            <form onSubmit={join}>
              <RoomCodeInput value={code} onChange={setCode} />
              <div className="entry-hint">
                <Icon name="headphones" size={15} />
                <span>No account needed to follow along.</span>
              </div>
              {error && <ErrorState message={error} />}
              <PrimaryActionButton type="submit" disabled={busy}>
                {busy ? "Joining lecture…" : "Join a lecture"}
                <Icon name="arrow" size={18} />
              </PrimaryActionButton>
            </form>
            <div className="entry-divider">
              <span>Leading the conversation?</span>
            </div>
            <Link className="button secondary host-link" href="/host">
              <Icon name="mic" size={17} />
              Host a lecture
              <Icon name="plus" size={17} />
            </Link>
            <div className="privacy-note">
              <span>◈</span>Your learning. Your language. Your pace.
            </div>
            <RecentLecture />
          </section>
        </div>
        <div className="home-bottom">
          <span>UNDERSTAND EVERY LECTURE.</span>
          <span>
            Never miss what matters.
            <span className="bottom-line" />
          </span>
          <span>BUILT FOR BELONGING ↗</span>
        </div>
      </main>
      <footer className="site-footer">
        <span>Levvy · A clearer way to learn</span>
        <Link href="/dashboard">
          Your accessibility preferences <Icon name="arrow" size={14} />
        </Link>
      </footer>
    </>
  );
}
