export default function Home() {
  return (
    <main className="min-h-screen flex items-center justify-center p-8">
      <div className="text-center space-y-2">
        <h1 className="text-2xl font-semibold">Levvy</h1>
        <p className="text-sm text-[#5A6675]">
          Frontend scaffold ready. Import typed API &amp; WebSocket helpers from{" "}
          <code className="font-mono text-[#2563EB]">@/lib/api</code>.
        </p>
      </div>
    </main>
  );
}
