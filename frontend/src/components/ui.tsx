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
    back: <path d="m14 6-6 6 6 6" />,
    mic: (
      <>
        <rect x="9" y="3" width="6" height="12" rx="3" />
        <path d="M5 10v2a7 7 0 0 0 14 0v-2M12 19v3m-4 0h8" />
      </>
    ),
    book: (
      <>
        <path d="M3 4h6l3 2 3-2h6v15h-6l-3 2-3-2H3zM12 6v15" />
      </>
    ),
    history: (
      <>
        <path d="M3 11a9 9 0 1 1 2 7M3 4v7h7M12 7v5l3 2" />
      </>
    ),
    copy: (
      <>
        <rect x="8" y="8" width="12" height="13" rx="3" />
        <path d="M15 8V3H3v12h5" />
      </>
    ),
    swap: <path d="M4 8h15l-4-4M20 16H5l4 4" />,
    play: <path d="m9 5 11 7-11 7z" />,
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
      <>
        <path d="M3 9h4l5-4v14l-5-4H3zM16 8a6 6 0 0 1 0 8M19 5a10 10 0 0 1 0 14" />
      </>
    ),
    upload: (
      <>
        <path d="M12 16V3m-5 5 5-5 5 5M4 15v6h16v-6" />
      </>
    ),
    search: (
      <>
        <circle cx="10" cy="10" r="7" />
        <path d="m15 15 6 6" />
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
      strokeWidth="1.5"
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
      <span className="brand-bars">
        <i />
        <i />
        <i />
        <i />
      </span>
      levvy<span className="logo-period">.</span>
    </Link>
  );
}
export function AppHeader({ active }: { active?: string }) {
  return (
    <header className="app-header">
      <LevvyLogo />
      <nav aria-label="Main navigation">
        <Link aria-current={active === "home" ? "page" : undefined} href="/">
          Assistant
        </Link>
        <Link
          aria-current={active === "dashboard" ? "page" : undefined}
          href="/dashboard"
        >
          My lectures
        </Link>
        <Link
          aria-current={active === "host" ? "page" : undefined}
          href="/host"
        >
          Host a lecture
        </Link>
      </nav>
      <Link
        className="account-button"
        href="/dashboard"
        aria-label="Your account"
      >
        <Icon name="settings" />
        <span>My workspace</span>
      </Link>
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
export function LanguageSelector({
  value,
  onChange,
  label = "Caption language",
  disabled = false,
}: {
  value: string;
  onChange: (value: string) => void;
  label?: string;
  disabled?: boolean;
}) {
  return (
    <label className="language">
      <span className="language-globe" aria-hidden="true">
        ◎
      </span>
      <span>
        <small>{label}</small>
        <select
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
      </span>
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
        value={source}
        onChange={onSource}
        disabled={sourceDisabled}
      />
      <button
        className="icon-button"
        aria-label="Swap languages"
        type="button"
        disabled={sourceDisabled}
        onClick={() => {
          onSource(target);
          onTarget(source);
        }}
      >
        <Icon name="swap" />
      </button>
      <LanguageSelector
        label="My caption language"
        value={target}
        onChange={onTarget}
      />
    </div>
  );
}
export function AudioVisualizer({
  level = 0,
  hero = false,
}: {
  level?: number;
  hero?: boolean;
}) {
  return (
    <div
      className={`audio-visual ${hero ? "hero-visual" : ""}`}
      aria-hidden="true"
      style={{ "--level": level } as React.CSSProperties}
    >
      <div className="dot-orb" />
      <div className="orb-core">
        <Icon name="mic" size={hero ? 29 : 36} />
      </div>
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
      <span className="empty-icon">
        <Icon name="headphones" size={26} />
      </span>
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
      <span>Lecture room code</span>
      <input
        required
        maxLength={16}
        autoComplete="off"
        autoCapitalize="characters"
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
      <Icon name="play" size={12} />
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
      <Icon name={copied ? "check" : "copy"} size={17} />
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
    <label className="caption-size">
      Text size
      <select
        aria-label="Caption text size"
        value={value}
        onChange={(e) => onChange(Number(e.target.value))}
      >
        <option value={20}>Comfortable</option>
        <option value={24}>Large</option>
        <option value={30}>Extra large</option>
        <option value={36}>Largest</option>
      </select>
    </label>
  );
}
