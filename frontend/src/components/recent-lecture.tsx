"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import type { DashboardSessionItemDto, UserDashboardDto } from "../lib/api";
import { readAuth, request } from "../lib/client";
import { Icon } from "./ui";

export function RecentLecture() {
  const [recent, setRecent] = useState<DashboardSessionItemDto | null>(null);
  useEffect(() => {
    if (!readAuth()) return;
    const controller = new AbortController();
    void request<UserDashboardDto>("/users/me/dashboard", {
      signal: controller.signal,
    })
      .then((data) => {
        const sessions = [
          ...data.attendedSessions,
          ...data.hostedSessions,
        ].sort(
          (a, b) => Date.parse(b.startedAtUtc) - Date.parse(a.startedAtUtc),
        );
        if (!controller.signal.aborted) setRecent(sessions[0] || null);
      })
      .catch(() => {});
    return () => controller.abort();
  }, []);
  return recent ? (
    <Link href={`/session/${recent.code}`} className="recent-lecture">
      <Icon name="history" size={15} />
      <span>
        Pick up where you left off<strong>{recent.title}</strong>
      </span>
      <Icon name="arrow" size={15} />
    </Link>
  ) : null;
}
