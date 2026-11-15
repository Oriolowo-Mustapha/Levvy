"use client";
import { useCallback, useEffect, useRef, useState } from "react";
import {
  WS_BASE,
  type TranscriptLineDto,
  type SessionTranscriptResponse,
} from "./api";
import { errorText, request, roomPath } from "./client";

export function useLecture(
  code: string,
  language: string,
  role: "host" | "viewer" = "viewer",
) {
  const [session, setSession] = useState<SessionTranscriptResponse | null>(
    null,
  );
  const [lines, setLines] = useState<TranscriptLineDto[]>([]);
  const [translations, setTranslations] = useState<Record<string, string>>({});
  const [connection, setConnection] = useState("Connecting");
  const [error, setError] = useState("");
  const [lastCaption, setLastCaption] = useState(0);
  const [sourceLanguage, setSourceLanguage] = useState("auto");
  const [asrStatus, setAsrStatus] = useState("Waiting for audio");
  const pendingAudio = useRef(0);
  const socket = useRef<WebSocket | null>(null);
  const languageRef = useRef(language);
  useEffect(() => {
    languageRef.current = language;
    if (socket.current?.readyState === WebSocket.OPEN)
      socket.current.send(JSON.stringify({ type: "set_lang", lang: language }));
  }, [language]);
  useEffect(() => {
    if (!code) return;
    let disposed = false;
    let ended = false;
    let reconnect: ReturnType<typeof setTimeout>;
    let poll: ReturnType<typeof setInterval>;
    let attempt = 0;
    const controller = new AbortController();
    async function refresh() {
      const data = await request<SessionTranscriptResponse>(
        `${roomPath(code)}/transcript`,
        { signal: controller.signal },
      );
      if (disposed) return null;
      setSession(data);
      ended = !data.isLive;
      if (ended) {
        setConnection("Session ended");
        socket.current?.close();
      }
      setLines((previous) => {
        const merged = new Map(previous.map((line) => [line.offsetMs, line]));
        data.lines.forEach((line) => merged.set(line.offsetMs, line));
        return [...merged.values()].sort((a, b) => a.offsetMs - b.offsetMs);
      });
      return data;
    }
    async function connect(id: string) {
      if (disposed || ended) return;
      setConnection(attempt ? "Reconnecting" : "Connecting");
      try {
        const params = new URLSearchParams({
          role,
          lang: languageRef.current,
          code,
        });
        if (role === "host") {
          const ticket = await request<{ ticket: string }>(
            `/sessions/${id}/ws-ticket`,
            { method: "POST" },
          );
          if (disposed) return;
          params.set("ticket", ticket.ticket);
        }
        const ws = new WebSocket(`${WS_BASE}/ws/${id}?${params}`);
        socket.current = ws;
        ws.onopen = () => {
          if (disposed) {
            ws.close();
            return;
          }
          attempt = 0;
          setError("");
          setConnection("Connected");
        };
        ws.onmessage = (event) => {
          if (disposed || typeof event.data !== "string") return;
          try {
            const msg = JSON.parse(event.data);
            if (msg?.type === "session_ended") {
              ended = true;
              setConnection("Session ended");
              socket.current?.close();
              void refresh().catch(() => {});
              return;
            }
            if (
              msg?.type === "asr_status" &&
              ["processing", "ready", "error"].includes(msg.state)
            ) {
              setAsrStatus(
                msg.state === "processing"
                  ? "Processing audio"
                  : msg.state === "error"
                    ? "Transcription unavailable — check the server configuration."
                    : "Audio processed",
              );
              if (msg.state !== "processing")
                pendingAudio.current = Math.max(0, pendingAudio.current - 1);
              return;
            }
            if (
              !msg ||
              !Number.isFinite(msg.offsetMs) ||
              msg.offsetMs < 0 ||
              typeof msg.text !== "string"
            )
              return;
            if (msg.type === "caption") {
              if (typeof msg.sourceLanguage === "string")
                setSourceLanguage(msg.sourceLanguage);
              setLastCaption(Date.now());
              setLines((previous) =>
                [
                  ...previous.filter((line) => line.offsetMs !== msg.offsetMs),
                  {
                    offsetMs: msg.offsetMs,
                    text: msg.text,
                    isCurrent: !msg.final,
                  },
                ].sort((a, b) => a.offsetMs - b.offsetMs),
              );
            }
            if (msg.type === "translation" && typeof msg.for === "string")
              setTranslations((previous) => ({
                ...previous,
                [`${msg.for}:${msg.offsetMs}`]: msg.text,
              }));
          } catch {
            /* Invalid frames never enter caption state. */
          }
        };
        ws.onerror = () => setConnection("Connection lost");
        ws.onclose = () => {
          if (!disposed && !ended) {
            setConnection("Reconnecting");
            reconnect = setTimeout(
              () => void connect(id),
              Math.min(30000, 1000 * 2 ** Math.min(attempt++, 5)) +
                Math.random() * 300,
            );
          }
        };
      } catch (e) {
        if (!disposed) {
          setError(errorText(e));
          setConnection("Connection lost");
          reconnect = setTimeout(
            () => void connect(id),
            Math.min(30000, 1000 * 2 ** Math.min(attempt++, 5)),
          );
        }
      }
    }
    void refresh()
      .then((data) => {
        if (!data) return;
        void connect(data.id);
        poll = setInterval(() => void refresh().catch(() => {}), 15000);
      })
      .catch((e) => {
        if (!disposed) {
          setError(errorText(e));
          setConnection("Unavailable");
        }
      });
    return () => {
      disposed = true;
      controller.abort();
      clearTimeout(reconnect);
      clearInterval(poll);
      socket.current?.close();
      socket.current = null;
    };
  }, [code, role]);
  const send = useCallback((payload: object | Blob) => {
    const ws = socket.current;
    if (!ws || ws.readyState !== WebSocket.OPEN)
      throw new Error(
        "Audio connection lost. Recording has stopped; reconnect before continuing.",
      );
    if (ws.bufferedAmount > 4 * 1024 * 1024)
      throw new Error(
        "Audio upload is falling behind. Please check your connection.",
      );
    ws.send(payload instanceof Blob ? payload : JSON.stringify(payload));
    if (payload instanceof Blob) pendingAudio.current++;
  }, []);
  const drainAudio = useCallback(async () => {
    const deadline = Date.now() + 30000;
    while (pendingAudio.current > 0) {
      if (Date.now() > deadline)
        throw new Error(
          "The recording is saved, but transcription is still processing. Wait a moment and try ending the lecture again.",
        );
      await new Promise((resolve) => setTimeout(resolve, 100));
    }
  }, []);
  return {
    session,
    lines,
    translations,
    connection,
    error,
    lastCaption,
    send,
    sourceLanguage,
    asrStatus,
    drainAudio,
  };
}
