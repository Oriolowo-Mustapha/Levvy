import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Levvy — The accessibility layer for live lectures",
  description:
    "Live captions, multilingual subtitles, mid-lecture grounded Q&A, and playable audio receipts for every student in the room.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html
      lang="en"
      data-scroll-behavior="smooth"
      className="h-full antialiased"
    >
      <body className="min-h-full flex flex-col bg-[#F4F5F7] text-[#1E2A38]">
        <a href="#main" className="skip-link">
          Skip to content
        </a>
        {children}
      </body>
    </html>
  );
}
