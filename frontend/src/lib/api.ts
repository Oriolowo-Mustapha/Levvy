/**
 * Levvy — Typed REST & WebSocket Client for the Frontend Team
 * Matches the frozen contract in README.md and architecture.md.
 */

export const API_BASE =
  process.env.NEXT_PUBLIC_API_URL?.replace(/\/$/, "") || "http://localhost:5000";

export const WS_BASE =
  process.env.NEXT_PUBLIC_WS_URL?.replace(/\/$/, "") ||
  API_BASE.replace(/^http/, "ws");

// --- DTOs ---

export interface CreateSessionResponse {
  sessionId: string;
  code: string;
}

export interface JoinSessionResponse {
  participantId: string;
  sessionId: string;
}

export interface SessionSummaryResponse {
  id: string;
  code: string;
  title: string;
  isLive: boolean;
  endedAtUtc?: string | null;
  participantCount: number;
}

export interface TranscriptLineDto {
  offsetMs: number;
  text: string;
  isCurrent: boolean;
}

export interface SessionTranscriptResponse {
  id: string;
  code: string;
  title: string;
  isLive: boolean;
  participantCount: number;
  lines: TranscriptLineDto[];
}

export interface AskAnswerResponse {
  type: "answer";
  status: "ok" | "not_covered";
  text: string;
  /** Playable receipt offsets in seconds (e.g. [2507, 2636]) */
  receipts: number[];
  /** Playable receipt offsets in milliseconds (e.g. [2507000, 2636000]) */
  receiptOffsetsMs: number[];
}

export interface FlashcardDto {
  id: string;
  front: string;
  back: string;
  sourceOffsetMs: number;
}

export interface StudyPackResponse {
  sessionId: string;
  code: string;
  title: string;
  summary: string;
  flashcards: FlashcardDto[];
  transcript: TranscriptLineDto[];
}

// --- WebSocket Event Payloads ---

export interface WsCaptionMessage {
  type: "caption";
  ts: string;
  offsetMs: number;
  text: string;
  final: boolean;
}

export interface WsTranslationMessage {
  type: "translation";
  for: string;
  ts: string;
  offsetMs: number;
  text: string;
}

export type WsInboundMessage =
  | WsCaptionMessage
  | WsTranslationMessage
  | AskAnswerResponse;

// --- Utility ---

export function formatMmSs(offsetMs: number): string {
  const totalSeconds = Math.max(0, Math.floor(offsetMs / 1000));
  const mins = Math.floor(totalSeconds / 60);
  const secs = totalSeconds % 60;
  return `${String(mins).padStart(2, "0")}:${String(secs).padStart(2, "0")}`;
}

export function getSessionAudioUrl(code: string): string {
  return `${API_BASE}/sessions/${encodeURIComponent(code.trim().toUpperCase())}/audio`;
}

// --- REST API Calls ---

export async function createSession(
  title: string,
  hostId?: string
): Promise<CreateSessionResponse> {
  const res = await fetch(`${API_BASE}/sessions`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ title, hostId }),
  });
  if (!res.ok) throw new Error(`POST /sessions failed (${res.status})`);
  return res.json();
}

export async function joinSession(
  code: string,
  displayName: string,
  preferredLanguage: string = "en"
): Promise<JoinSessionResponse> {
  const res = await fetch(
    `${API_BASE}/sessions/${encodeURIComponent(code.trim().toUpperCase())}/join`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ displayName, preferredLanguage }),
    }
  );
  if (!res.ok) throw new Error(`POST /sessions/${code}/join failed (${res.status})`);
  return res.json();
}

export async function getSession(code: string): Promise<SessionSummaryResponse> {
  const res = await fetch(
    `${API_BASE}/sessions/${encodeURIComponent(code.trim().toUpperCase())}`,
    { cache: "no-store" }
  );
  if (!res.ok) throw new Error(`GET /sessions/${code} failed (${res.status})`);
  return res.json();
}

export async function getTranscript(code: string): Promise<SessionTranscriptResponse> {
  const res = await fetch(
    `${API_BASE}/sessions/${encodeURIComponent(code.trim().toUpperCase())}/transcript`,
    { cache: "no-store" }
  );
  if (!res.ok) throw new Error(`GET /sessions/${code}/transcript failed (${res.status})`);
  return res.json();
}

export async function askQuestion(
  code: string,
  text: string,
  askerId?: string
): Promise<AskAnswerResponse> {
  const res = await fetch(
    `${API_BASE}/sessions/${encodeURIComponent(code.trim().toUpperCase())}/ask`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ text, askerId }),
    }
  );
  if (!res.ok) throw new Error(`POST /sessions/${code}/ask failed (${res.status})`);
  return res.json();
}

export async function endSession(sessionId: string): Promise<void> {
  const res = await fetch(`${API_BASE}/sessions/${encodeURIComponent(sessionId)}/end`, {
    method: "POST",
  });
  if (!res.ok) throw new Error(`POST /sessions/${sessionId}/end failed (${res.status})`);
}

export async function getStudyPack(code: string): Promise<StudyPackResponse> {
  const res = await fetch(
    `${API_BASE}/sessions/${encodeURIComponent(code.trim().toUpperCase())}/pack`,
    { cache: "no-store" }
  );
  if (!res.ok) throw new Error(`GET /sessions/${code}/pack failed (${res.status})`);
  return res.json();
}

// --- WebSocket Client Helper ---

export interface SessionSocketOptions {
  sessionId: string;
  code?: string;
  lang?: string;
  role?: "host" | "viewer";
  participantId?: string;
  onCaption?: (msg: WsCaptionMessage) => void;
  onTranslation?: (msg: WsTranslationMessage) => void;
  onAnswer?: (msg: AskAnswerResponse) => void;
  onOpen?: () => void;
  onClose?: () => void;
  onError?: (err: Event) => void;
}

export function connectSessionSocket(opts: SessionSocketOptions) {
  const params = new URLSearchParams({
    lang: opts.lang || "en",
    role: opts.role || "viewer",
  });
  if (opts.code) params.set("code", opts.code);
  if (opts.participantId) params.set("participantId", opts.participantId);

  const ws = new WebSocket(`${WS_BASE}/ws/${opts.sessionId}?${params.toString()}`);

  ws.onopen = () => opts.onOpen?.();
  ws.onclose = () => opts.onClose?.();
  ws.onerror = (e) => opts.onError?.(e);

  ws.onmessage = (event) => {
    try {
      const msg = JSON.parse(event.data) as WsInboundMessage;
      if (msg.type === "caption") opts.onCaption?.(msg);
      else if (msg.type === "translation") opts.onTranslation?.(msg);
      else if (msg.type === "answer") opts.onAnswer?.(msg);
    } catch {
      // Ignore non-JSON frames
    }
  };

  return {
    socket: ws,
    sendAudioChunk: (chunk: ArrayBuffer | Blob) => {
      if (ws.readyState === WebSocket.OPEN) ws.send(chunk);
    },
    ask: (text: string) => {
      if (ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify({ type: "ask", text }));
      }
    },
    setLanguage: (lang: string) => {
      if (ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify({ type: "set_lang", lang }));
      }
    },
    close: () => ws.close(),
  };
}
