# Slate — Real-time Collaborative Whiteboard

Draw, brainstorm, and build together — live. Create a board, share the link, and collaborate with anyone in real time. Every stroke, sticky note, and text block appears instantly for all participants.

**Live Demo:** https://slate-frontend-kappa.vercel.app

---

## What It Does

- Create a board and get a shareable link
- Invite anyone — they join instantly with no account required to view
- Draw, write, and add sticky notes together in real time
- See every collaborator's live cursor with their name
- Pan and zoom across an infinite canvas
- Your work is saved automatically

---

## Tech Stack

**Frontend:** React 18 · TypeScript · TanStack Router · @microsoft/signalr · Lucide React · Vite

**Backend:** ASP.NET Core 10 · C# · Entity Framework Core · PostgreSQL · SignalR · JWT Auth · Google OAuth

**Hosting:** Vercel (Frontend) · Railway (Backend + PostgreSQL)

---

## Key Features

- **Real-time sync** — SignalR WebSocket connection broadcasts every element to all board members instantly
- **Live cursors** — Each collaborator's cursor is visible with their display name, colored uniquely per user
- **Drawing tools** — Freehand pen (thin/medium/thick), text, sticky notes, image upload
- **Infinite canvas** — Pan by dragging, zoom with scroll wheel or pinch gesture
- **Auto-save** — Canvas state is persisted to the database (debounced on the client)
- **Board management** — Dashboard lists your boards; create, rename, or delete
- **Member roles** — Owner / Editor / Viewer with server-enforced permissions
- **Auth** — Email/password + Google OAuth, JWT-based sessions

---

## How Real-time Works

```
User draws stroke
  → Canvas captures points locally
  → On mouse-up: stroke is added to local state immediately
  → SignalR sends the finished stroke to the server Hub
  → Hub broadcasts to all other members in the same board room
  → Other clients receive the stroke and render it on their canvas
  → Separately, a debounced REST call persists the full board state to PostgreSQL
```

The Hub also broadcasts cursor positions (throttled to ~50ms) and deletion events. The REST API handles durable persistence — the Hub is purely for live sync.

---

## SignalR Hub Methods

| Client → Server | Description |
|---|---|
| `JoinBoard(boardId)` | Join the board room, validates membership |
| `LeaveBoard(boardId)` | Leave the room |
| `SendElement(boardId, elementJson)` | Broadcast a new element to others |
| `DeleteElement(boardId, elementId)` | Broadcast a deletion to others |
| `SendCursor(boardId, {x, y})` | Broadcast cursor position |

| Server → Client | Description |
|---|---|
| `UserJoined` | New collaborator joined |
| `UserLeft` | Collaborator disconnected |
| `ReceiveElement` | New element from another user |
| `ElementDeleted` | Element removed by another user |
| `ReceiveCursor` | Cursor position update |

---

## Canvas Element Types

```typescript
type BoardEl =
  | { kind: 'stroke'; points: [number, number][]; color: string; width: number }
  | { kind: 'text';   x: number; y: number; text: string; color: string }
  | { kind: 'sticky'; x: number; y: number; text: string; color: string }
  | { kind: 'image';  x: number; y: number; width: number; height: number; src: string }
```

All elements are stored as JSON in a single PostgreSQL column — no separate table per element type.

---

## Local Setup

### Backend
```bash
git clone https://github.com/Mohamed-Elsayed1xx/Slate-API
cd Slate.Api
```

Create `appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=slate;Username=postgres;Password=yourpassword"
  },
  "Jwt": {
    "Secret": "your-secret-key-min-32-chars",
    "Issuer": "Slate",
    "Audience": "SlateClient"
  },
  "Google": {
    "ClientId": "your-google-client-id"
  }
}
```

```bash
dotnet ef database update
dotnet run
```

### Frontend
```bash
git clone https://github.com/Mohamed-Elsayed1xx/Slate-Frontend
cd Slate.Frontend
npm install
```

Create `.env.local`:
```
VITE_API_BASE_URL=http://localhost:5000/api
```

```bash
npm run dev
```

---

## Environment Variables (Railway)

```
ConnectionStrings__DefaultConnection=
Jwt__Secret=
Jwt__Issuer=Slate
Jwt__Audience=SlateClient
Google__ClientId=
FRONTEND_URL=https://slate-frontend-kappa.vercel.app
```
