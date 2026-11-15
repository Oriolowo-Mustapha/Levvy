# Levvy — Operations & Deployment Runbook

This runbook covers deploying, configuring, and troubleshooting Levvy on **Vultr Compute**, **Vultr Object Storage**, and **Vultr Managed PostgreSQL**.

---

## 1. Production Architecture on Vultr (`levvy.tech`)

| Component | Path on Vultr VM | Runtime |
|---|---|---|
| **Backend API + WebSocket Hub** | `/opt/levvy` | .NET 10 (`Levvy.Api`), port `5000` |
| **Backend Environment File** | `/opt/levvy/.env` | Loaded at startup by `DotEnvLoader` |
| **Frontend Web App** | `/opt/levvy-web` | Next.js 16 Standalone (`node server.js`), port `3000` |
| **Reverse Proxy / TLS** | Nginx / Caddy | Terminates HTTPS/WSS for `levvy.tech` |

---

## 2. Environment Configuration (`/opt/levvy/.env`)

To edit the production environment variables on the Vultr VM:

```bash
sudo nano /opt/levvy/.env
```

### Required & Recommended Variables

```dotenv
# 1. Database (Vultr Managed PostgreSQL)
DATABASE_URL=Host=<vultr-pg-host>;Port=<port>;Database=levvy;Username=<user>;Password=<password>;Ssl Mode=Require;Trust Server Certificate=true

# 2. Groq (LLM for Translation, Ask Levvy Q&A, Study Pack + Fallback Whisper ASR)
GROQ_API_KEY=gsk_...
GROQ_BASE_URL=https://api.groq.com/openai/v1/
GROQ_ASR_MODEL=whisper-large-v3-turbo
GROQ_ANSWER_MODEL=openai/gpt-oss-120b
GROQ_FALLBACK_MODEL=llama-3.3-70b-versatile

# 3. Deepgram (Primary Nova-3 Speech-to-Text + Aura-2 Text-to-Speech)
ASR_PROVIDER=deepgram
DEEPGRAM_API_KEY=...
DEEPGRAM_BASE_URL=https://api.deepgram.com/v1/
DEEPGRAM_MODEL=nova-3
DEEPGRAM_DIARIZE=true
DEEPGRAM_TTS_MODEL=aura-2-thalia-en

# 4. Vultr Object Storage (S3-Compatible Audio Recordings)
STORAGE_ENDPOINT=ewr1.vultrobjects.com
STORAGE_REGION=us-east-1
STORAGE_BUCKET=levvy-recordings
STORAGE_ACCESS_KEY=...
STORAGE_SECRET_KEY=...

# 5. Authentication & JWT (7-day default expiry = 10080 minutes)
JWT_SECRET=<at-least-32-random-characters>
JWT_ISSUER=levvy-api
JWT_AUDIENCE=levvy-client
JWT_EXPIRY_MINUTES=10080
GOOGLE_CLIENT_ID=...
GOOGLE_CLIENT_SECRET=...

# 6. Security & CORS
CORS_ALLOWED_ORIGINS=https://levvy.tech,https://www.levvy.tech
HANGFIRE_DASHBOARD_TOKEN=<secret-admin-token>
```

After updating `/opt/levvy/.env`, restart the backend service:

```bash
sudo systemctl restart levvy-api
sudo systemctl status levvy-api --no-pager -l
```

---

## 3. Automated CI/CD Deployment (`.github/workflows/deploy-vultr.yml`)

Every push to `main`:
1. Runs all 55 backend xUnit tests (`dotnet test Levvy.slnx --configuration Release`).
2. Publishes `Levvy.Api` (`dotnet publish -c Release -o ../publish/api`).
3. Builds the Next.js 16 standalone bundle (`npm ci && npm run build`) and packages `.next/standalone`, `.next/static`, and `public` into `web.tar.gz`.
4. Uploads the backend to `/opt/levvy` and frontend to `/opt/levvy-web` via SCP.
5. Connects via SSH to unpack `/opt/levvy-web/web.tar.gz` and restart `levvy-api`/`levvy` and `levvy-web` systemd units.

---

## 4. Troubleshooting Common Issues

### Issue A: `Groq chat completion failed with status 404: model_not_found`
- **Cause:** `GROQ_ANSWER_MODEL` or `GROQ_FALLBACK_MODEL` in `/opt/levvy/.env` is set to a deprecated or unpermitted model ID on your Groq account.
- **Fix:** Set `GROQ_ANSWER_MODEL=openai/gpt-oss-120b` and `GROQ_FALLBACK_MODEL=llama-3.3-70b-versatile` in `/opt/levvy/.env`, then run `sudo systemctl restart levvy-api`.

### Issue B: Live Captions Work, but Translation Card Stays Empty
- **Cause:** Translation uses the Groq LLM (`ITranslator` → `GroqLlmClient`). If `GROQ_API_KEY` is missing or the model ID fails, translation fails while Deepgram ASR continues working.
- **Check logs:** `sudo journalctl -u levvy-api -n 100 --no-pager`

### Issue C: Speaker Button (TTS) Does Not Play Audio
- **Cause:** `POST /sessions/{code}/speak` requires `DEEPGRAM_API_KEY` with permission to call Deepgram `/v1/speak` (`aura-2-*` voices).
- **Verify:** Test TTS directly against the API:
  ```bash
  curl -i -X POST https://levvy.tech/api/sessions/K4F-8Q2/speak \
    -H "Content-Type: application/json" \
    -d '{"text":"Bonjour tout le monde","language":"fr"}'
  ```

### Issue D: Startup Fails in Production with `JWT_SECRET` Error
- **Cause:** Audit fix `C3` refuses to boot in `Production` if `JWT_SECRET` is missing, shorter than 32 characters, or left at the development placeholder.
- **Fix:** Generate a 48-byte random hex/base64 secret (`openssl rand -base64 48`) and set `JWT_SECRET` in `/opt/levvy/.env`.
