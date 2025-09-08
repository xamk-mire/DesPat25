# Assignment: Setup Smart Greenhouse Backend & Frontend (Step‑by‑Step)

## Objective

Set up a minimal IoT-style web app consisting of a **backend API (ASP.NET Core + EF Core + PostgreSQL)** and a **frontend dashboard (React + Vite + TypeScript)**. By the end you will:

- Run a .NET backend connected to PostgreSQL
    
- Expose API endpoints for sensor readings and devices
    
- Launch a React frontend that fetches from the backend and displays values

## Prerequisites

- .NET SDK **8.0**
    
- Node.js **18+**, npm **9+**
    
- Local PostgreSQL 14+
    

---

## Part A — Backend (ASP.NET Core + PostgreSQL)

### A1) Create solution and projects

```bash
mkdir -p backend/src && cd backend
```

**Explanation:** Create a `backend/` folder with a `src/` subfolder and switch into `backend`.

```bash
dotnet new sln -n SmartGreenhouse
```

**Explanation:** Create a new **solution** file (`SmartGreenhouse.sln`) to contain multiple .NET projects.

```bash
cd src
```

**Explanation:** Move into the source folder where we’ll place project directories.

```bash
dotnet new classlib -n SmartGreenhouse.Domain -f net8.0 && mkdir -p SmartGreenhouse.Domain/Entities
```

**Explanation:** Create a **class library** for **domain models** (pure C#). Then make an `Entities/` folder for entity classes.

```bash
dotnet new classlib -n SmartGreenhouse.Infrastructure -f net8.0 && mkdir -p SmartGreenhouse.Infrastructure/Data
```

**Explanation:** Make a library for **infrastructure** concerns (EF Core DbContext, repositories). Create `Data/` for DB files.

```bash
dotnet new classlib -n SmartGreenhouse.Application -f net8.0 && mkdir -p SmartGreenhouse.Application/Services
```

**Explanation:** Create the **application layer** where business/use-case services live. Add `Services/` folder.

```bash
dotnet new classlib -n SmartGreenhouse.Shared -f net8.0 && mkdir -p SmartGreenhouse.Shared/Contracts
```

**Explanation:** Shared types/contracts that may be used by multiple projects.

```bash
dotnet new webapi -n SmartGreenhouse.Api -f net8.0 && mkdir -p SmartGreenhouse.Api/Controllers
```

**Explanation:** Scaffold the **ASP.NET Core Web API** project and a `Controllers/` folder for HTTP endpoints.


### A2) Add packages

```bash
cd SmartGreenhouse.Infrastructure
 dotnet add package Microsoft.EntityFrameworkCore --version 8.0.8
 dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.8
cd ..
```

**Explanation:** Install EF Core + the **PostgreSQL provider** into **Infrastructure**.

```bash
cd SmartGreenhouse.Api
 dotnet add package Swashbuckle.AspNetCore --version 6.6.2
 dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.8
cd ..
```

**Explanation:** Add **Swagger** (OpenAPI UI) and EF **design-time** tools for migrations to the **API** project.

### A3) Wire references & add to solution

```bash
# From backend/src

dotnet add SmartGreenhouse.Infrastructure/SmartGreenhouse.Infrastructure.csproj reference SmartGreenhouse.Domain/SmartGreenhouse.Domain.csproj
```

**Explanation:** Infrastructure depends on Domain entities.

```bash
dotnet add SmartGreenhouse.Application/SmartGreenhouse.Application.csproj reference SmartGreenhouse.Domain/SmartGreenhouse.Domain.csproj SmartGreenhouse.Infrastructure/SmartGreenhouse.Infrastructure.csproj
```

**Explanation:** Application uses Domain models and Infrastructure persistence.

```bash
dotnet add SmartGreenhouse.Api/SmartGreenhouse.Api.csproj reference SmartGreenhouse.Domain/SmartGreenhouse.Domain.csproj SmartGreenhouse.Application/SmartGreenhouse.Application.csproj SmartGreenhouse.Infrastructure/SmartGreenhouse.Infrastructure.csproj SmartGreenhouse.Shared/SmartGreenhouse.Shared.csproj
```

**Explanation:** API calls Application services, returns Domain data, persists via Infrastructure, and may use Shared contracts.

```bash
cd ..
dotnet sln SmartGreenhouse.sln add src/SmartGreenhouse.Domain/SmartGreenhouse.Domain.csproj src/SmartGreenhouse.Infrastructure/SmartGreenhouse.Infrastructure.csproj src/SmartGreenhouse.Application/SmartGreenhouse.Application.csproj src/SmartGreenhouse.Shared/SmartGreenhouse.Shared.csproj src/SmartGreenhouse.Api/SmartGreenhouse.Api.csproj
```

**Explanation:** Register all the projects into the **solution** so `dotnet build` builds everything together.

A4) Paste backend code


### A5) Start PostgreSQL

Create a new database called `greenhouse`, either with PgAdmin tool or using `psql` terminal commands


### A6) Create DB schema (migrations)

```bash
dotnet tool install --global dotnet-ef
```

**Explanation:** Install EF **CLI** tooling (one-time per machine).

```bash
cd backend/src
```

**Explanation:** Ensure the **project directories** are the working dir (the `-p` and `-s` paths below are resolved from here).

```bash
dotnet ef migrations add InitialCreate -p SmartGreenhouse.Infrastructure -s SmartGreenhouse.Api -o Data/Migrations
```

**Explanation:** Generate a migration in the **Infrastructure** project (`-p`), using the **API** project as the **startup** (`-s`) so it can read `appsettings.json` and DI. Output files into `Data/Migrations`.

```bash
dotnet ef database update -p SmartGreenhouse.Infrastructure -s SmartGreenhouse.Api
```

**Explanation:** Apply the migrations to the Postgres database defined by the API's connection string.


### A7) Build & run API on port 5080

```bash
cd ../..   # back to backend/
dotnet build
```

**Explanation:** Compile all projects in the solution to ensure everything links.

```bash
cd src/SmartGreenhouse.Api
ASPNETCORE_URLS=http://localhost:5080 dotnet run
```

**Explanation:** Start Kestrel and bind the API to **[http://localhost:5080](http://localhost:5080/)** so the frontend dev proxy can reach it.

### A8) Seed a reading (for UI test)

```bash
curl -X POST http://localhost:5080/api/readings \
  -H "Content-Type: application/json" \
  -d '{
    "deviceId":"demo-01",
    "sensorType":"temp",
    "value":24.2,
    "unit":"°C",
    "timestamp":"2025-09-08T09:00:00Z"
  }'
```

**Explanation:** Create one sensor reading that the dashboard can display.

---

## Part B — Frontend (React + Vite + TypeScript)

### B1) Scaffold app

```bash
mkdir -p frontend && cd frontend
npm create vite@latest smart-greenhouse-web -- --template react-ts
cd smart-greenhouse-web
npm install
```

**Explanation:** Create a React + TypeScript app using **Vite** and install dependencies.

### B2) Add Tailwind CSS

```bash
npm install tailwindcss @tailwindcss/vite
```

Add the `@tailwindcss/vite` plugin to your Vite configuration inside `vite.config.ts`.

```ts
import { defineConfig } from 'vite';
import react from "@vitejs/plugin-react";
import tailwindcss from '@tailwindcss/vite';

export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
  ],
})
```

Add an `@import` to your CSS file that imports Tailwind CSS, e.g. `index.css`

```css
@import "tailwindcss";
```

### B3) Configure dev proxy to backend

**vite.config.ts**

```ts
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
  ],
  server: {
    port: 5173,
    proxy: {
      "/api": { target: "http://localhost:5080", changeOrigin: true }
    }
  }
});
```

**Explanation:** Forward `/api` requests from the dev server to the backend so we avoid CORS in development.

### B4) Minimal API client and UI

Create **`src/api/greenhouse.ts`**:

```ts
export type Reading = {
  id: string
  deviceId: string
  sensorType: string
  value: number
  unit: string
  timestamp: string
}

export async function fetchReadings(params?: { deviceId?: string; sensorType?: string }) {
  const q = new URLSearchParams(params as Record<string, string>).toString()
  const res = await fetch(`/api/readings${q ? `?${q}` : ''}`)
  if (!res.ok) throw new Error(`Failed to load readings: ${res.status}`)
  return res.json() as Promise<Reading[]>
}
```

**What this does:** provides a typed function to call your backend’s `/api/readings`.


---


### B6) Minimal UI components


Create **`src/components/SensorCard.tsx`**:

```tsx
type Props = { title: string; value?: number; unit?: string }

export function SensorCard({ title, value, unit }: Props) {
  return (
    <div className="rounded-2xl p-4 shadow-md bg-white">
      <div className="font-semibold text-gray-700">{title}</div>
      <div className="mt-2 text-3xl font-bold text-gray-900">
        {value ?? '—'} <span className="text-sm text-gray-500">{unit}</span>
      </div>
    </div>
  )
}
```

Create **`src/pages/Dashboard.tsx`**:

```tsx
import { useEffect, useMemo, useState } from 'react'
import { fetchReadings, Reading } from '@/api/greenhouse'
import { SensorCard } from '@/components/SensorCard'

export default function Dashboard() {
  const [readings, setReadings] = useState<Reading[]>([])
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    fetchReadings().then(setReadings).catch(e => setError(String(e)))
  }, [])

  const latestByType = useMemo(() => {
    const map = new Map<string, Reading>()
    for (const r of readings) {
      const prev = map.get(r.sensorType)
      if (!prev || prev.timestamp < r.timestamp) map.set(r.sensorType, r)
    }
    return map
  }, [readings])

  return (
    <div className="p-6 grid gap-4 grid-cols-1 sm:grid-cols-2 lg:grid-cols-4">
      {error && <div className="text-red-600">{error}</div>}
      <SensorCard title="Temperature"    value={latestByType.get('temp')?.value}         unit={latestByType.get('temp')?.unit} />
      <SensorCard title="Humidity"       value={latestByType.get('humidity')?.value}     unit={latestByType.get('humidity')?.unit} />
      <SensorCard title="Light"          value={latestByType.get('light')?.value}        unit={latestByType.get('light')?.unit} />
      <SensorCard title="Soil Moisture"  value={latestByType.get('soilMoisture')?.value} unit={latestByType.get('soilMoisture')?.unit} />
    </div>
  )
}
```

Update **`src/App.tsx`**:

```tsx
import Dashboard from '@/pages/Dashboard'
export default function App() { return <Dashboard /> }
```

Update **`src/main.tsx`**:

```tsx
import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App'
import './styles/globals.css'   // <-- import Tailwind layers, replace with correct css file

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
)
```

Make sure **`index.html`** has a root div (Vite creates this by default):

```html
<div id="root"></div>
```

---

### B7) Run the frontend

```bash
npm run dev
```

**Explanation:** Start Vite dev server at **[http://localhost:5173](http://localhost:5173/)**. It proxies `/api` to the backend.

---

### B8) Verify end-to-end

- Visit **[http://localhost:5080/swagger](http://localhost:5080/swagger)** and call `GET /api/readings` — you should see data.
    
- Open the frontend at **[http://localhost:5173](http://localhost:5173/)**; cards should show the latest values.
    
- Add another reading via Swagger or `curl`, then refresh the frontend to confirm it updates.
    
---

### B9) Quick end-to-end test

With your backend running:

```bash
curl -X POST http://localhost:5080/api/readings 
  -H "Content-Type: application/json" 
  -d '{
    "deviceId":"demo-01",
    "sensorType":"temp",
    "value":24.2,
    "unit":"°C",
    "timestamp":"2025-09-08T09:00:00Z"
  }'
```

or alternatively you can use Postman to perform the API call

 - Type: `POST`
 - Url: `http://localhost:5080/api/readings `
 - Add new key value to headers: `Content-Type: application/json`
 - Body: `{
    "deviceId":"demo-01",
    "sensorType":"temp",
    "value":24.2,
    "unit":"°C",
    "timestamp":"2025-09-08T09:00:00Z"
  }`


Refresh the frontend — the **Temperature** card should show the new value.

---

## Troubleshooting

- **CORS errors**: Ensure the Vite proxy is configured, or enable CORS in the API.
    
- **DB connection errors**: Verify Postgres container is running and credentials match `appsettings.json`.
    
- **404 on `/api/*`**: Confirm the API runs on `http://localhost:5080` and controller routes are `api/[controller]`.
    
- **Type mismatches**: Align the `Reading` TypeScript type with the backend JSON fields.
    

