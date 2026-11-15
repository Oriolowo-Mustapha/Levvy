"use client";

/**
 * Marketing sections for the landing page.
 * Reuses the app's monochrome system: grey canvas, white cards, near-black
 * dark cards, the red "live" accent only for the live dot, pill CTAs.
 * The interactive receipt player is the single orchestrated moment on the page.
 */

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { Icon } from "./ui";
import { formatMmSs } from "../lib/api";

/* ------------------------------------------------------------------ */
/* How it works                                                        */
/* ------------------------------------------------------------------ */

const STEPS = [
  {
    title: "Start or join",
    body: "Host live from the browser, or paste a Google Meet, Zoom, or Teams link. The Levvy note-taker joins and listens.",
  },
  {
    title: "Follow along",
    body: "Captions land on every student's screen, translated into the language they picked, seconds behind the speaker.",
  },
  {
    title: "Leave with the lecture",
    body: "When class ends the study pack is ready: a summary you can verify, and flashcards that point back into the recording.",
  },
];

export function HowItWorks() {
  return (
    <section className="mk-section" aria-labelledby="mk-how-title">
      <h2 id="mk-how-title" className="mk-title">
        How a lecture runs on Levvy
      </h2>
      <ol className="how">
        {STEPS.map((step, i) => (
          <li key={step.title} className="how-step">
            <span className="how-step-num num" aria-hidden="true">
              {i + 1}
            </span>
            <h3>{step.title}</h3>
            <p>{step.body}</p>
          </li>
        ))}
      </ol>
    </section>
  );
}

/* ------------------------------------------------------------------ */
/* The receipt: interactive player (signature section)                 */
/* ------------------------------------------------------------------ */

const PLAY_FROM = 2507; // 41:47, a real offset in the demo lecture transcript
const PLAY_TO = 2517;
const LINES = [
  { at: 2507, text: "Cross-price elasticity measures how quantity demanded of good A responds to a price change in good B." },
  { at: 2511, text: "So when the price of a substitute falls, buyers move toward the cheaper good." },
  { at: 2515, text: "Demand for the original product shifts left. That is the whole mechanism." },
];

const prefersReducedMotion = () =>
  typeof window !== "undefined" &&
  window.matchMedia("(prefers-reduced-motion: reduce)").matches;

function ReceiptPlayer() {
  const [pos, setPos] = useState<number>(PLAY_FROM);
  const [playing, setPlaying] = useState(false);
  const raf = useRef(0);

  useEffect(() => {
    if (!playing) return;
    const from = pos;
    const t0 = performance.now();
    const tick = (now: number) => {
      const p = Math.min(PLAY_TO, from + (now - t0) / 1000);
      setPos(p);
      if (p >= PLAY_TO) {
        setPlaying(false);
        return;
      }
      raf.current = requestAnimationFrame(tick);
    };
    raf.current = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(raf.current);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [playing]);

  const press = () => {
    if (playing) {
      cancelAnimationFrame(raf.current);
      setPlaying(false);
      return;
    }
    if (prefersReducedMotion()) {
      // No animation: show the highlighted moment immediately.
      setPos(PLAY_FROM + 1);
      setPlaying(false);
      return;
    }
    if (pos >= PLAY_TO) setPos(PLAY_FROM);
    setPlaying(true);
  };

  const active = (() => {
    let idx = 0;
    LINES.forEach((line, i) => {
      if (line.at <= pos) idx = i;
    });
    return idx;
  })();
  const progress = Math.min(1, Math.max(0, (pos - (PLAY_FROM - 3)) / (PLAY_TO - (PLAY_FROM - 3))));
  const atEnd = pos >= PLAY_TO;

  return (
    <div className="receipt-player">
      <div className="receipt-player-head">
        <span className="receipt-player-app">Ask Levvy</span>
        <span className="receipt-live">
          <i />
          Live transcript
        </span>
      </div>
      <p className="receipt-question">
        What happens to demand when the price of a substitute falls?
      </p>
      <div className="receipt-answer">
        <p>
          Buyers shift toward the cheaper good, so demand for the original
          product falls. The two are linked through cross-price elasticity.
        </p>
        <button
          type="button"
          className={`receipt-chip ${playing ? "is-playing" : ""}`}
          onClick={press}
          aria-pressed={playing}
          aria-label={
            playing
              ? `${formatMmSs(pos * 1000)}, pause the recording`
              : atEnd
                ? `Replay ${formatMmSs(PLAY_FROM * 1000)}`
                : `${formatMmSs(PLAY_FROM * 1000)} in the recording, play the cited moment`
          }
        >
          <Icon name={playing ? "pause" : "play"} size={12} />
          {playing
            ? formatMmSs(pos * 1000)
            : atEnd
              ? `Replay ${formatMmSs(PLAY_FROM * 1000)}`
              : `${formatMmSs(PLAY_FROM * 1000)} in the recording`}
        </button>
      </div>
      <div className="receipt-transcript" aria-hidden="true">
        {LINES.map((line, i) => (
          <p
            key={line.at}
            className={`receipt-line ${i === active ? "active" : ""}`}
          >
            <span className="num">{formatMmSs(line.at * 1000)}</span>
            {line.text}
          </p>
        ))}
      </div>
      <div className="receipt-track">
        <span style={{ inlineSize: `${Math.round(progress * 100)}%` }} />
      </div>
    </div>
  );
}

export function ReceiptSection() {
  return (
    <section className="mk-section receipt-section" aria-labelledby="mk-receipt-title">
      <div className="receipt-copy">
        <h2 id="mk-receipt-title" className="mk-title">
          Don&rsquo;t trust the summary. Play the proof.
        </h2>
        <p>
          Every answer, flashcard, and summary line carries a receipt: the exact
          position in the recording it came from. Tap it and hear the original
          words.
        </p>
        <p className="receipt-fine">
          Levvy answers from the live transcript only. If something wasn&rsquo;t
          said, it says so instead of guessing.
        </p>
      </div>
      <ReceiptPlayer />
    </section>
  );
}

/* ------------------------------------------------------------------ */
/* Features: asymmetric bento                                          */
/* ------------------------------------------------------------------ */

export function FeatureBento() {
  return (
    <section className="mk-section" aria-labelledby="mk-features-title">
      <h2 id="mk-features-title" className="mk-title">
        What students get in the room
      </h2>
      <div className="bento">
        <article className="bento-tile bento-study">
          <h3>Study pack on autopilot</h3>
          <p>
            When the lecture ends, the pack is already done: a summary you can
            verify, and flashcards that cite the moment they came from.
          </p>
          <div className="bento-flashcard" aria-hidden="true">
            <span className="bento-flash-side">Front</span>
            <p>What does a high elasticity of substitution imply?</p>
            <span className="bento-flash-side">Back</span>
            <p className="bento-flash-back">
              Consumers switch easily between goods.
              <span className="receipt-chip mini num">
                <Icon name="play" size={10} />
                43:12
              </span>
            </p>
          </div>
        </article>

        <article className="bento-tile bento-captions">
          <h3>Captions that keep up</h3>
          <p>Near-live captions from the room&rsquo;s audio or the meeting bot.</p>
          <p className="bento-caption-line" aria-hidden="true">
            <span className="num">41:47</span>
            Cross-price elasticity measures how quantity demanded responds&hellip;
          </p>
        </article>

        <article className="bento-tile bento-langs">
          <h3>Your language, their lecture</h3>
          <p>
            Everyone in the room reads the same lecture in their own language,
            picked per student.
          </p>
          <div className="bento-lang-row" aria-hidden="true">
            <span className="bento-lang"><b>FR</b>Français</span>
            <span className="bento-lang"><b>ES</b>Español</span>
            <span className="bento-lang"><b>HI</b>हिन्दी</span>
          </div>
        </article>

        <article className="bento-tile bento-ask">
          <h3>Ask mid-lecture</h3>
          <p>
            Raise a question without raising a hand. Answers come from the
            transcript, receipts attached.
          </p>
        </article>

        <article className="bento-tile bento-meet">
          <h3>Works with your meetings</h3>
          <p>Meeting links or plain tab audio. Nothing to install.</p>
          <ul className="bento-providers" aria-label="Supported meeting platforms">
            <li>Google Meet</li>
            <li>Zoom</li>
            <li>Teams</li>
          </ul>
        </article>

        <article className="bento-tile bento-a11y">
          <h3>Built for hearing accessibility</h3>
          <p>
            Live text for deaf and hard-of-hearing students: the words on screen
            as they&rsquo;re said, not after.
          </p>
        </article>
      </div>
    </section>
  );
}

/* ------------------------------------------------------------------ */
/* Who it's for                                                        */
/* ------------------------------------------------------------------ */

const PERSONAS = [
  {
    who: "The international student",
    body: "Follow a fast lecture in your second language, then re-read any claim with its receipt.",
  },
  {
    who: "The deaf and hard-of-hearing student",
    body: "The room finally speaks your way: the words on screen as they are said, not after the class.",
  },
  {
    who: "The one who zoned out",
    body: "Missed ten minutes? Jump straight back to the moment, not a transcript hunt.",
  },
];

export function Personas() {
  return (
    <section className="mk-section" aria-labelledby="mk-personas-title">
      <h2 id="mk-personas-title" className="mk-title">
        Who it&rsquo;s for
      </h2>
      <div className="personas">
        {PERSONAS.map((p) => (
          <div key={p.who} className="persona">
            <h3>{p.who}</h3>
            <p>{p.body}</p>
          </div>
        ))}
      </div>
    </section>
  );
}

/* ------------------------------------------------------------------ */
/* Honest proof band                                                   */
/* ------------------------------------------------------------------ */

const PROOF = [
  "Built at HackGlobal 2026",
  "Every deploy gated by an automated test suite",
  "Receipts verified server-side before a student sees them",
];

export function ProofBand() {
  return (
    <section className="mk-section proof-band" aria-label="Why you can rely on Levvy">
      {PROOF.map((item) => (
        <p key={item}>{item}</p>
      ))}
    </section>
  );
}

/* ------------------------------------------------------------------ */
/* FAQ (native details/summary: keyboard-accessible with zero JS)      */
/* ------------------------------------------------------------------ */

const FAQS = [
  {
    q: "Do students need an account?",
    a: "No. Joining a lecture with a room code is anonymous. Hosts can sign in to keep their history and dashboard.",
  },
  {
    q: "Who joins my Google Meet?",
    a: "Only the Levvy note-taker bot, when a host sends it. It listens, captions, and leaves when the call ends.",
  },
  {
    q: "How many languages?",
    a: "The source language is detected automatically. Translation covers more than thirty languages, from Spanish and French to Hindi and Vietnamese.",
  },
  {
    q: "Where does the recording go?",
    a: "Object storage, private to the session. Receipts point into it, so viewers can play the moments they were cited. Nothing more is exposed.",
  },
  {
    q: "What does it cost?",
    a: "Nothing during the HackGlobal pilot. Hosting a lecture and joining one are both free.",
  },
];

export function Faq() {
  return (
    <section className="mk-section" aria-labelledby="mk-faq-title">
      <h2 id="mk-faq-title" className="mk-title">
        Questions, answered
      </h2>
      <div className="faq">
        {FAQS.map((item) => (
          <details key={item.q}>
            <summary>
              {item.q}
              <Icon name="plus" size={16} />
            </summary>
            <p>{item.a}</p>
          </details>
        ))}
      </div>
    </section>
  );
}

/* ------------------------------------------------------------------ */
/* Final CTA + footer                                                  */
/* ------------------------------------------------------------------ */

export function CtaBand() {
  return (
    <section className="mk-section cta-band" aria-labelledby="mk-cta-title">
      <h2 id="mk-cta-title">Your next lecture, understood.</h2>
      <p>
        Join with a room code, or bring your own meeting link. The demo runs
        without a signup.
      </p>
      <div className="cta-actions">
        <a className="button primary" href="#join">
          Join a lecture
          <Icon name="headphones" size={15} />
        </a>
        <Link className="button cta-ghost" href="/host">
          Host a lecture
          <Icon name="mic" size={15} />
        </Link>
      </div>
    </section>
  );
}

export function LandingFooter() {
  return (
    <footer className="home-footer">
      <Link href="/" className="logo-word-footer" aria-label="Levvy home">
        levvy
      </Link>
      <nav aria-label="Footer">
        <Link href="/host">Host</Link>
        <Link href="/dashboard">History</Link>
        <a href="https://github.com/Oriolowo-Mustapha/Levvy" rel="noreferrer">
          GitHub
        </a>
      </nav>
      <p>Built for HackGlobal 2026</p>
    </footer>
  );
}
