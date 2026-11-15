"use client";

import Link from "next/link";
import {
  useEffect,
  useRef,
  useState,
  useSyncExternalStore,
  type ButtonHTMLAttributes,
  type ReactNode,
} from "react";
import { formatMmSs } from "../lib/api";

export const languages = [
  ["en", "English"],
  ["fr", "Français"],
  ["es", "Español"],
  ["ar", "العربية"],
  ["hi", "हिन्दी"],
  ["ta", "தமிழ்"],
  ["de", "Deutsch"],
] as const;
export const languageName = (code: string) =>
  languages.find(([id]) => id === code)?.[1] ||
  (code === "auto" ? "Original speech" : code);
const subscribeReady = () => () => {};
export function BrowserReady({ children }: { children: ReactNode }) {
  const ready = useSyncExternalStore(
    subscribeReady,
    () => true,
    () => false,
  );
  return ready ? children : <LoadingState />;
}
export function Icon({ name, size = 20 }: { name: string; size?: number }) {
  const paths: Record<string, ReactNode> = {
    arrow: <path d="m8 5 7 7-7 7M4 12h11" />,
    arrowUp: <path d="M12 19V5m-6 6 6-6 6 6" />,
    back: <path d="m14 6-6 6 6 6" />,
    chevron: <path d="m6 9 6 6 6-6" />,
    menu: <path d="M5 8h14M5 12h14M5 16h9" />,
    mic: (
      <>
        <rect x="9" y="3" width="6" height="12" rx="3" />
        <path d="M5 10v2a7 7 0 0 0 14 0v-2M12 19v3" />
      </>
    ),
    book: <path d="M3 4h6l3 2 3-2h6v15h-6l-3 2-3-2H3zM12 6v15" />,
    history: <path d="M3 11a9 9 0 1 1 2 7M3 4v7h7M12 7v5l3 2" />,
    copy: (
      <>
        <rect x="8" y="8" width="12" height="13" rx="3" />
        <path d="M15 8V3H3v12h5" />
      </>
    ),
    swap: <path d="M4 8h15l-4-4M20 16H5l4 4" />,
    play: <path d="m9 5 11 7-11 7z" />,
    stop: <rect x="6" y="6" width="12" height="12" rx="2" />,
    headphones: (
      <>
        <path d="M4 14v-3a8 8 0 0 1 16 0v3" />
        <rect x="3" y="12" width="4" height="8" rx="2" />
        <rect x="17" y="12" width="4" height="8" rx="2" />
      </>
    ),
    spark: <path d="m12 2 3 7 7 3-7 3-3 7-3-7-7-3 7-3z" />,
    monitor: (
      <>
        <rect x="2" y="3" width="20" height="14" rx="3" />
        <path d="M12 17v4m-5 0h10" />
      </>
    ),
    check: <path d="m5 12 4 4L20 5" />,
    plus: <path d="M12 5v14M5 12h14" />,
    volume: (
      <path d="M3 9h4l5-4v14l-5-4H3zM16 8a6 6 0 0 1 0 8M19 5a10 10 0 0 1 0 14" />
    ),
    upload: <path d="M12 16V3m-5 5 5-5 5 5M4 15v6h16v-6" />,
    search: (
      <>
        <circle cx="10" cy="10" r="7" />
        <path d="m15 15 6 6" />
      </>
    ),
    expand: <path d="M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5" />,
    list: <path d="M8 6h12M8 12h12M8 18h8M4 6h.01M4 12h.01M4 18h.01" />,
    user: (
      <>
        <circle cx="12" cy="8" r="4" />
        <path d="M4 21a8 8 0 0 1 16 0" />
      </>
    ),
    paste: (
      <>
        <rect x="6" y="4" width="12" height="17" rx="3" />
        <path d="M9 4V3h6v1M9 11h6M9 15h4" />
      </>
    ),
    settings: (
      <>
        <circle cx="12" cy="12" r="4" />
        <path d="M12 2v3m0 14v3M2 12h3m14 0h3M5 5l2 2m10 10 2 2M5 19l2-2M17 7l2-2" />
      </>
    ),
  };
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.6"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {paths[name] || paths.spark}
    </svg>
  );
}
export function LevvyLogo() {
  return (
    <Link href="/" className="logo" aria-label="Levvy home">
      <span className="logo-mark">
        <span className="brand-bars">
          <i />
          <i />
          <i />
          <i />
        </span>
      </span>
      <span className="logo-word">levvy</span>
    </Link>
  );
}
/** Top bar from the reference: round icon buttons + a "History" pill. */
export function AppHeader({
  active,
  back,
}: {
  active?: string;
  back?: { href: string; label: string };
}) {
  return (
    <header className="app-header">
      {back ? (
        <Link className="icon-button" href={back.href} aria-label={back.label}>
          <Icon name="back" size={18} />
        </Link>
      ) : (
        <LevvyLogo />
      )}
      <nav aria-label="Main navigation" className="header-actions">
        <Link
          className="icon-button"
          aria-current={active === "home" ? "page" : undefined}
          href="/"
          aria-label="Join a lecture"
          title="Join a lecture"
        >
          <Icon name="headphones" size={17} />
        </Link>
        <Link
          className="icon-button"
          aria-current={active === "host" ? "page" : undefined}
          href="/host"
          aria-label="Host a lecture"
          title="Host a lecture"
        >
          <Icon name="mic" size={17} />
        </Link>
        <Link
          className="pill-link"
          aria-current={active === "dashboard" ? "page" : undefined}
          href="/dashboard"
        >
          <Icon name="history" size={15} />
          History
        </Link>
      </nav>
    </header>
  );
}
export function PrimaryActionButton({
  children,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button {...props} className={`button primary ${props.className || ""}`}>
      {children}
    </button>
  );
}
export function SecondaryActionButton({
  children,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button {...props} className={`button secondary ${props.className || ""}`}>
      {children}
    </button>
  );
}
/** The small ringed dot at the end of black pill buttons in the reference. */
export const PillDot = () => <span className="pill-dot" aria-hidden="true" />;
function LanguageMark({ code }: { code: string }) {
  return (
    <span className="lang-mark" aria-hidden="true">
      {code === "auto" ? "··" : code.slice(0, 2).toUpperCase()}
    </span>
  );
}
/** Static "From · English" pill, used where the language is not editable. */
export function LanguagePill({
  value,
  caption,
}: {
  value: string;
  caption: string;
}) {
  return (
    <span className="language static">
      <LanguageMark code={value} />
      <span className="lang-text">
        <small>{caption}</small>
        <strong>{languageName(value)}</strong>
      </span>
    </span>
  );
}
export function LanguageSelector({
  value,
  onChange,
  label = "Caption language",
  caption,
  disabled = false,
}: {
  value: string;
  onChange: (value: string) => void;
  label?: string;
  /** Short visible hint ("From", "To"); defaults to the accessible label. */
  caption?: string;
  disabled?: boolean;
}) {
  return (
    <label className={`language ${disabled ? "disabled" : ""}`}>
      <LanguageMark code={value} />
      <span className="lang-text">
        <small>{caption || label}</small>
        <strong>{languageName(value)}</strong>
      </span>
      <Icon name="chevron" size={14} />
      <select
        className="lang-select"
        disabled={disabled}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        aria-label={label}
      >
        {languages.map(([id, name]) => (
          <option key={id} value={id}>
            {name}
          </option>
        ))}
      </select>
    </label>
  );
}
export function LanguagePairSelector({
  source,
  target,
  onSource,
  onTarget,
  sourceDisabled = false,
}: {
  source: string;
  target: string;
  onSource: (v: string) => void;
  onTarget: (v: string) => void;
  sourceDisabled?: boolean;
}) {
  return (
    <div className="language-pair">
      <LanguageSelector
        label="Lecture language"
        caption="From"
        value={source}
        onChange={onSource}
        disabled={sourceDisabled}
      />
      <button
        className="swap-button"
        aria-label="Swap languages"
        type="button"
        disabled={sourceDisabled}
        onClick={() => {
          onSource(target);
          onTarget(source);
        }}
      >
        <Icon name="swap" size={16} />
      </button>
      <LanguageSelector
        label="My caption language"
        caption="To"
        value={target}
        onChange={onTarget}
      />
    </div>
  );
}
/**
 * Halftone dot sphere from the reference's listening screen. Dots are largest
 * at the centre and fade to nothing at the rim; `level` (0–1) swells them.
 */
export function DotSphere({
  level = 0,
  size = 240,
  spacing = 6,
}: {
  level?: number;
  size?: number;
  spacing?: number;
}) {
  const canvas = useRef<HTMLCanvasElement>(null);
  const levelRef = useRef(level);
  useEffect(() => {
    levelRef.current = level;
  }, [level]);
  useEffect(() => {
    const el = canvas.current;
    const ctx = el?.getContext("2d");
    if (!el || !ctx) return;
    const dpr = Math.min(2, window.devicePixelRatio || 1);
    el.width = size * dpr;
    el.height = size * dpr;
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    const still = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    const color = getComputedStyle(el).color;
    let smooth = 0;
    let frame = 0;
    const draw = (t: number) => {
      smooth += (levelRef.current - smooth) * 0.18;
      const energy = Math.min(1, smooth * 5);
      const breathe = still ? 0 : Math.sin(t / 1100) * 0.05;
      const c = size / 2;
      ctx.clearRect(0, 0, size, size);
      ctx.fillStyle = color;
      for (let y = spacing / 2; y < size; y += spacing)
        for (let x = spacing / 2; x < size; x += spacing) {
          const d = Math.hypot(x - c, y - c) / c;
          if (d >= 1) continue;
          const falloff = Math.cos((d * Math.PI) / 2) ** 1.4;
          const r =
            (spacing / 2) *
            Math.min(0.92, falloff * (0.5 + breathe + energy * 0.5));
          if (r < 0.3) continue;
          ctx.beginPath();
          ctx.arc(x, y, r, 0, Math.PI * 2);
          ctx.fill();
        }
      frame = requestAnimationFrame(draw);
    };
    frame = requestAnimationFrame(draw);
    return () => cancelAnimationFrame(frame);
  }, [size, spacing]);
  return (
    <canvas
      ref={canvas}
      className="dot-sphere"
      style={{ width: size, height: size }}
      aria-hidden="true"
    />
  );
}
/** Glass orb with a halftone core — the hero mark from the reference. */
export function AudioVisualizer({
  level = 0,
  hero = false,
}: {
  level?: number;
  hero?: boolean;
}) {
  return hero ? (
    <div className="orb" aria-hidden="true">
      <DotSphere level={level} size={96} spacing={4} />
    </div>
  ) : (
    <div className="listening-sphere" aria-hidden="true">
      <DotSphere level={level} size={260} spacing={6.5} />
    </div>
  );
}
/** Pager-style dots from the reference, lit in proportion to input level. */
export function LevelDots({ level }: { level: number }) {
  const lit = Math.round(Math.min(1, level * 6) * 10);
  return (
    <div className="level-dots" aria-hidden="true">
      {Array.from({ length: 10 }, (_, i) => (
        <i key={i} className={i < Math.max(1, lit) ? "on" : ""} />
      ))}
    </div>
  );
}
export function RecordingTimer({ ms }: { ms: number }) {
  return <time className="num">{formatMmSs(ms)}</time>;
}
export function RecordingStatus({
  state,
  recording = false,
}: {
  state: string;
  recording?: boolean;
}) {
  return (
    <span
      className={`recording-status ${recording ? "is-recording" : ""}`}
      role="status"
    >
      <span />
      {state}
    </span>
  );
}
export function ConnectionIndicator({ state }: { state: string }) {
  return (
    <span className="connection" role="status">
      <span className={state === "Connected" ? "connected" : ""} />
      {state}
    </span>
  );
}
export function EmptyState({
  title,
  children,
}: {
  title: string;
  children?: ReactNode;
}) {
  return (
    <div className="empty-state">
      <h3>{title}</h3>
      <p>{children}</p>
    </div>
  );
}
export function LoadingState({
  children = "Loading your workspace…",
}: {
  children?: ReactNode;
}) {
  return (
    <div className="loading-state" role="status">
      <span className="spinner" />
      {children}
    </div>
  );
}
export function ErrorState({
  message,
  retry,
}: {
  message: string;
  retry?: () => void;
}) {
  return (
    <div className="error-state" role="alert">
      <p>{message}</p>
      {retry && (
        <SecondaryActionButton onClick={retry}>Try again</SecondaryActionButton>
      )}
    </div>
  );
}
export function RoomCodeInput({
  value,
  onChange,
}: {
  value: string;
  onChange: (v: string) => void;
}) {
  return (
    <label className="room-input">
      <span className="sr-only">Lecture room code</span>
      <input
        required
        maxLength={16}
        autoComplete="off"
        autoCapitalize="characters"
        spellCheck={false}
        placeholder="e.g. K4F-8Q2"
        value={value}
        onChange={(e) =>
          onChange(e.target.value.toUpperCase().replace(/[^A-Z0-9-]/g, ""))
        }
      />
    </label>
  );
}
export function AudioReceiptChip({
  offsetMs,
  onPlay,
  disabled = false,
}: {
  offsetMs: number;
  onPlay: (ms: number) => void;
  disabled?: boolean;
}) {
  return (
    <button
      type="button"
      disabled={disabled}
      className="receipt num"
      onClick={() => onPlay(offsetMs)}
      aria-label={`Play recording at ${formatMmSs(offsetMs)}`}
    >
      <Icon name="play" size={10} />
      {formatMmSs(offsetMs)}
    </button>
  );
}
export function CopyButton({ text }: { text: string }) {
  const [copied, setCopied] = useState(false);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  useEffect(
    () => () => {
      if (timer.current) clearTimeout(timer.current);
    },
    [],
  );
  return (
    <button
      type="button"
      className="icon-button"
      aria-label={copied ? "Copied" : "Copy text"}
      onClick={async () => {
        try {
          await navigator.clipboard.writeText(text);
          setCopied(true);
          timer.current = setTimeout(() => setCopied(false), 2000);
        } catch {
          setCopied(false);
        }
      }}
    >
      <Icon name={copied ? "check" : "copy"} size={16} />
    </button>
  );
}
export const captionSizes = [
  [20, "Comfortable"],
  [24, "Large"],
  [30, "Extra large"],
  [36, "Largest"],
] as const;
/** The reference's "Tт" button: cycles caption text size. */
export function TextSizeButton({
  value,
  onChange,
}: {
  value: number;
  onChange: (n: number) => void;
}) {
  const index = Math.max(
    0,
    captionSizes.findIndex(([n]) => n === value),
  );
  const [, name] = captionSizes[index];
  return (
    <button
      type="button"
      className="icon-button text-size"
      aria-label={`Caption text size: ${name}. Change size`}
      title={`Text size: ${name}`}
      onClick={() =>
        onChange(captionSizes[(index + 1) % captionSizes.length][0])
      }
    >
      <span aria-hidden="true">
        T<small>т</small>
      </span>
    </button>
  );
}
export function CaptionSizeControl({
  value,
  onChange,
}: {
  value: number;
  onChange: (n: number) => void;
}) {
  return (
    <label className="field">
      Text size
      <select
        aria-label="Caption text size"
        value={value}
        onChange={(e) => onChange(Number(e.target.value))}
      >
        {captionSizes.map(([n, name]) => (
          <option key={n} value={n}>
            {name}
          </option>
        ))}
      </select>
    </label>
  );
}
