import { Suspense, type ReactNode } from "react";
import { LoadingState } from "../../../components/ui";

export default function SessionLayout({ children }: { children: ReactNode }) {
  return (
    <Suspense fallback={<LoadingState>Opening lecture…</LoadingState>}>
      {children}
    </Suspense>
  );
}
