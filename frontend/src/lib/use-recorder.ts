"use client";
import { useEffect, useRef, useState } from "react";
import { errorText } from "./client";

export function useRecorder(
  send: (payload: object | Blob) => void,
  sourceLanguage: string,
) {
  const [state, setState] = useState("Ready to record");
  const [level, setLevel] = useState(0);
  const [elapsed, setElapsed] = useState(0);
  const [error, setError] = useState("");
  const [recording, setRecording] = useState(false);
  const [paused, setPaused] = useState(false);
  const resources = useRef<{
    stream: MediaStream;
    context: AudioContext;
    full: MediaRecorder;
    chunk: MediaRecorder | null;
    timer: ReturnType<typeof setInterval>;
    clock: ReturnType<typeof setInterval>;
    chunkDone: Promise<void>;
    frame: number;
    start: number;
    offset: number;
    stop: boolean;
    paused: boolean;
    startChunkLoop: () => void;
    startMeter: () => void;
    parts: Blob[];
    done: Promise<Blob>;
  } | null>(null);
  const elapsedRef = useRef(0);
  const pending = useRef(false);
  const mounted = useRef(true);
  function cleanup() {
    const r = resources.current;
    if (!r) return;
    r.stop = true;
    clearInterval(r.timer);
    clearInterval(r.clock);
    cancelAnimationFrame(r.frame);
    r.stream.getTracks().forEach((track) => track.stop());
    void r.context.close().catch(() => {});
    setLevel(0);
    setRecording(false);
    setPaused(false);
  }
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      const r = resources.current;
      if (r) {
        r.stop = true;
        clearInterval(r.timer);
        clearInterval(r.clock);
        cancelAnimationFrame(r.frame);
        if (r.chunk?.state === "recording") r.chunk.stop();
        if (r.full.state === "recording") r.full.stop();
        r.stream.getTracks().forEach((t) => t.stop());
        void r.context.close().catch(() => {});
      }
    };
  }, []);
  async function start(source: "mic" | "tab") {
    if (pending.current || resources.current) return;
    pending.current = true;
    setError("");
    setState("Waiting for audio permission");
    let stream: MediaStream | undefined;
    let context: AudioContext | undefined;
    try {
      if (!navigator.mediaDevices || typeof MediaRecorder === "undefined")
        throw new Error(
          "Audio capture needs a supported browser and a secure HTTPS connection.",
        );
      const mime = ["audio/webm;codecs=opus", "audio/webm"].find((t) =>
        MediaRecorder.isTypeSupported(t),
      );
      if (!mime)
        throw new Error(
          "This browser cannot record WebM audio required by Levvy. Please use a current Chromium browser.",
        );
      if (source === "tab") {
        if (!navigator.mediaDevices.getDisplayMedia)
          throw new Error(
            "Tab audio capture is not supported here. Choose microphone instead.",
          );
        stream = await navigator.mediaDevices.getDisplayMedia({
          video: true,
          audio: true,
        });
        if (!stream.getAudioTracks().length) {
          stream.getTracks().forEach((t) => t.stop());
          throw new Error(
            "No shared audio. Select a browser tab and enable “Share tab audio”.",
          );
        }
      } else
        stream = await navigator.mediaDevices.getUserMedia({
          audio: { echoCancellation: true, noiseSuppression: true },
          video: false,
        });
      if (!mounted.current) {
        stream.getTracks().forEach((t) => t.stop());
        return;
      }
      const audioStream = new MediaStream(stream.getAudioTracks());
      context = new AudioContext();
      await context.resume();
      const analyser = context.createAnalyser();
      analyser.fftSize = 256;
      context.createMediaStreamSource(audioStream).connect(analyser);
      const full = new MediaRecorder(audioStream, { mimeType: mime });
      const parts: Blob[] = [];
      full.ondataavailable = (e) => {
        if (e.data.size) parts.push(e.data);
      };
      const done = new Promise<Blob>((resolve) => {
        full.onstop = () => resolve(new Blob(parts, { type: mime }));
      });
      const r = {
        stream,
        context,
        full,
        chunk: null as MediaRecorder | null,
        timer: setInterval(() => {}, 1000),
        clock: setInterval(() => {}, 1000),
        chunkDone: Promise.resolve(),
        frame: 0,
        start: performance.now(),
        offset: elapsedRef.current,
        stop: false,
        paused: false,
        startChunkLoop: () => {},
        startMeter: () => {},
        parts,
        done,
      };
      resources.current = r;
      clearInterval(r.timer);
      clearInterval(r.clock);
      full.start(1000);
      setRecording(true);
      setPaused(false);
      setState("Checking microphone");
      const samples = new Uint8Array(analyser.fftSize);
      let lastSignal = performance.now();
      function meter() {
        if (r.stop || r.paused) return;
        analyser.getByteTimeDomainData(samples);
        let sum = 0;
        for (const n of samples) sum += ((n - 128) / 128) ** 2;
        const rms = Math.sqrt(sum / samples.length);
        if (rms > 0.008) lastSignal = performance.now();
        setLevel(Math.min(1, rms * 8));
        setState(
          performance.now() - lastSignal > 5000
            ? "No audio detected"
            : rms > 0.008
              ? "Recording"
              : "Checking microphone",
        );
        r.frame = requestAnimationFrame(meter);
      }
      r.startMeter = () => {
        lastSignal = performance.now();
        cancelAnimationFrame(r.frame);
        meter();
      };
      meter();
      // Each ASR frame is an independently decodable WebM, rather than a headerless timeslice.
      function nextChunk() {
        if (r.stop || r.paused) return;
        const chunk = new MediaRecorder(audioStream, { mimeType: mime });
        r.chunk = chunk;
        const offset = r.offset + performance.now() - r.start;
        chunk.ondataavailable = (e) => {
          if (!e.data.size) return;
          try {
            send({
              type: "chunk_meta",
              offsetMs: Math.round(offset),
              lang: sourceLanguage,
            });
            send(e.data);
          } catch (e) {
            setError(errorText(e));
            setState("Connection lost — recording stopped");
            if (full.state === "recording" || full.state === "paused")
              full.stop();
            cleanup();
          }
        };
        r.chunkDone = new Promise<void>((resolve) => {
          chunk.onstop = () => {
            resolve();
            if (!r.stop && !r.paused) nextChunk();
          };
        });
        chunk.start();
      }
      r.startChunkLoop = () => {
        clearInterval(r.timer);
        clearInterval(r.clock);
        nextChunk();
        r.timer = setInterval(() => {
          if (!r.paused && r.chunk?.state === "recording") r.chunk.stop();
        }, 5000);
        r.clock = setInterval(() => {
          if (r.paused) return;
          elapsedRef.current = Math.round(
            r.offset + performance.now() - r.start,
          );
          setElapsed(elapsedRef.current);
        }, 250);
      };
      r.startChunkLoop();
      for (const track of stream.getTracks())
        track.onended = () => {
          if (!r.stop) {
            r.stop = true;
            if (r.chunk?.state === "recording") r.chunk.stop();
            if (full.state === "recording") full.stop();
            cleanup();
            setState("Audio sharing stopped — save your recording");
          }
        };
    } catch (e) {
      stream?.getTracks().forEach((t) => t.stop());
      if (context) void context.close().catch(() => {});
      setError(errorText(e));
      setState("Audio unavailable");
    } finally {
      pending.current = false;
    }
  }
  function pause() {
    const r = resources.current;
    if (!r || r.stop || r.paused) return;
    r.paused = true;
    r.offset = Math.round(r.offset + performance.now() - r.start);
    elapsedRef.current = r.offset;
    setElapsed(r.offset);
    clearInterval(r.timer);
    clearInterval(r.clock);
    cancelAnimationFrame(r.frame);
    if (r.chunk?.state === "recording") r.chunk.stop();
    if (r.full.state === "recording") r.full.pause();
    setLevel(0);
    setPaused(true);
    setState("Paused");
  }
  function resume() {
    const r = resources.current;
    if (!r || r.stop || !r.paused) return;
    r.paused = false;
    r.start = performance.now();
    if (r.full.state === "paused") r.full.resume();
    setPaused(false);
    setState("Recording");
    r.startMeter();
    r.startChunkLoop();
  }
  async function stop(): Promise<Blob | null> {
    const r = resources.current;
    if (!r) return null;
    r.stop = true;
    if (!r.paused) {
      elapsedRef.current = Math.round(r.offset + performance.now() - r.start);
      setElapsed(elapsedRef.current);
    }
    if (r.chunk?.state === "recording") r.chunk.stop();
    if (r.full.state === "recording" || r.full.state === "paused")
      r.full.stop();
    cleanup();
    await r.chunkDone;
    const blob = await r.done;
    resources.current = null;
    setState("Recording stopped");
    return blob;
  }
  return {
    state,
    level,
    elapsed,
    error,
    start,
    pause,
    resume,
    stop,
    recording,
    paused,
  };
}
