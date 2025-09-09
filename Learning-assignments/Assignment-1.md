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
mkdir -p backend/src
cd backend
```

 - **Explanation:** Create a `backend/` folder with a `src/` subfolder and switch into `backend`.

```bash
dotnet new sln -n SmartGreenhouse
```

 - **Explanation:** Create a new **solution** file (`SmartGreenhouse.sln`) to contain multiple .NET projects.

```bash
cd src
```

 - **Explanation:** Move into the source folder where we’ll place project directories.

```bash
dotnet new classlib -n SmartGreenhouse.Domain -f net8.0
mkdir -p SmartGreenhouse.Domain/Entities
```

 - **Explanation:** Create a **class library** for **domain models** (pure C#). Then make an `Entities/` folder for entity classes.

```bash
dotnet new classlib -n SmartGreenhouse.Infrastructure -f net8.0
mkdir -p SmartGreenhouse.Infrastructure/Data
```

 - **Explanation:** Make a library for **infrastructure** concerns (EF Core DbContext, repositories). Create `Data/` for DB files.

```bash
dotnet new classlib -n SmartGreenhouse.Application -f net8.0
mkdir -p SmartGreenhouse.Application/Services
```

 - **Explanation:** Create the **application layer** where business/use-case services live. Add `Services/` folder.

```bash
dotnet new classlib -n SmartGreenhouse.Shared -f net8.0
mkdir -p SmartGreenhouse.Shared/Contracts
```

 - **Explanation:** Shared types/contracts that may be used by multiple projects.

```bash
dotnet new webapi -n SmartGreenhouse.Api -f net8.0
mkdir -p SmartGreenhouse.Api/Controllers
```

 - **Explanation:** Scaffold the **ASP.NET Core Web API** project and a `Controllers/` folder for HTTP endpoints.

---

### A2) Add packages

```bash
cd SmartGreenhouse.Infrastructure
 dotnet add package Microsoft.EntityFrameworkCore --version 8.0.8
 dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.8
cd ..
```

 - **Explanation:** Install EF Core + the **PostgreSQL provider** into **Infrastructure**.

```bash
cd SmartGreenhouse.Api
 dotnet add package Swashbuckle.AspNetCore --version 6.6.2
 dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.8
cd ..
```

 - **Explanation:** Add **Swagger** (OpenAPI UI) and EF **design-time** tools for migrations to the **API** project.

---

### A3) Wire references & add to solution

```bash
# From backend/src

dotnet add SmartGreenhouse.Infrastructure/SmartGreenhouse.Infrastructure.csproj reference SmartGreenhouse.Domain/SmartGreenhouse.Domain.csproj
```

 - **Explanation:** Infrastructure depends on Domain entities.

```bash
dotnet add SmartGreenhouse.Application/SmartGreenhouse.Application.csproj reference SmartGreenhouse.Domain/SmartGreenhouse.Domain.csproj SmartGreenhouse.Infrastructure/SmartGreenhouse.Infrastructure.csproj
```

 - **Explanation:** Application uses Domain models and Infrastructure persistence.

```bash
dotnet add SmartGreenhouse.Api/SmartGreenhouse.Api.csproj reference SmartGreenhouse.Domain/SmartGreenhouse.Domain.csproj SmartGreenhouse.Application/SmartGreenhouse.Application.csproj SmartGreenhouse.Infrastructure/SmartGreenhouse.Infrastructure.csproj SmartGreenhouse.Shared/SmartGreenhouse.Shared.csproj
```

 - **Explanation:** API calls Application services, returns Domain data, persists via Infrastructure, and may use Shared contracts.

```bash
cd ..
dotnet sln SmartGreenhouse.sln add src/SmartGreenhouse.Domain/SmartGreenhouse.Domain.csproj src/SmartGreenhouse.Infrastructure/SmartGreenhouse.Infrastructure.csproj src/SmartGreenhouse.Application/SmartGreenhouse.Application.csproj src/SmartGreenhouse.Shared/SmartGreenhouse.Shared.csproj src/SmartGreenhouse.Api/SmartGreenhouse.Api.csproj
```

 - **Explanation:** Register all the projects into the **solution** so `dotnet build` builds everything together.

---

### A4) Add the code files

## Domain

**`backend/src/SmartGreenhouse.Domain/Entities/Device.cs`**

```csharp
namespace SmartGreenhouse.Domain.Entities;

public class Device
{
    public int Id { get; set; } // PK
    public string DeviceName { get; set; } = string.Empty; // custom human-readable name
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ICollection<SensorReading> Readings { get; set; } = new List<SensorReading>();
}
```

**`backend/src/SmartGreenhouse.Domain/Entities/SensorReading.cs`**

```csharp
namespace SmartGreenhouse.Domain.Entities;

public class SensorReading
{
    public int Id { get; set; } // PK
    public int DeviceId { get; set; } // FK -> Device.Id
    public Device? Device { get; set; } // navigation property

    public string SensorType { get; set; } = string.Empty; // temp|humidity|light|soilMoisture
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;       // °C|%|lux|%
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
```

## Infrastructure

**`backend/src/SmartGreenhouse.Infrastructure/Data/AppDbContext.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using SmartGreenhouse.Domain.Entities;

namespace SmartGreenhouse.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {}

    public DbSet<Device> Devices => Set<Device>();
    public DbSet<SensorReading> Readings => Set<SensorReading>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Device>()
            .HasKey(d => d.Id);

        modelBuilder.Entity<SensorReading>()
            .HasKey(r => r.Id);

        // Configure relationship: one Device → many Readings
        modelBuilder.Entity<SensorReading>()
            .HasOne(r => r.Device)
            .WithMany(d => d.Readings)
            .HasForeignKey(r => r.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for query performance
        modelBuilder.Entity<SensorReading>()
            .HasIndex(r => new { r.DeviceId, r.SensorType, r.Timestamp });
    }
}
```


## Application

**`backend/src/SmartGreenhouse.Application/Services/ReadingService.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using SmartGreenhouse.Domain.Entities;
using SmartGreenhouse.Infrastructure.Data;

namespace SmartGreenhouse.Application.Services;

public class ReadingService
{
    private readonly AppDbContext _db;
    public ReadingService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<SensorReading>> QueryAsync(int? deviceId = null, string? sensorType = null, int take = 200)
    {
        var q = _db.Readings
            .Include(r => r.Device) // optional, if you want Device info in results
            .AsNoTracking()
            .OrderByDescending(r => r.Timestamp)
            .AsQueryable();

        if (deviceId.HasValue) q = q.Where(r => r.DeviceId == deviceId.Value);
        if (!string.IsNullOrWhiteSpace(sensorType)) q = q.Where(r => r.SensorType == sensorType);

        return await q.Take(take).ToListAsync();
    }

    public async Task<SensorReading> AddAsync(SensorReading reading)
    {
        _db.Readings.Add(reading);
        await _db.SaveChangesAsync();
        return reading;
    }
}
```


## API

**`backend/src/SmartGreenhouse.Api/Program.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using SmartGreenhouse.Application.Services;
using SmartGreenhouse.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var cs = builder.Configuration.GetConnectionString("Default")
         ?? "Host=localhost;Port=5432;Database=greenhouse;Username=greenhouse;Password=greenhouse";
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(cs));

builder.Services.AddScoped<ReadingService>();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
await app.RunAsync();
```

**`backend/src/SmartGreenhouse.Api/appsettings.json`**

>[!NOTE]
>Update the ConnectionStrings "Default" Username and Password to use your PostgreSQL credentials

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=greenhouse;Username=greenhouse;Password=greenhouse"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

**Controllers**

`backend/src/SmartGreenhouse.Api/Controllers/HealthController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;

namespace SmartGreenhouse.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok", utc = DateTime.UtcNow });
}
```

`backend/src/SmartGreenhouse.Api/Controllers/ReadingsController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using SmartGreenhouse.Application.Services;
using SmartGreenhouse.Domain.Entities;

namespace SmartGreenhouse.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReadingsController : ControllerBase
{
    private readonly ReadingService _service;
    public ReadingsController(ReadingService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int? deviceId, [FromQuery] string? sensorType)
        => Ok(await _service.QueryAsync(deviceId, sensorType));

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] SensorReading reading)
        => Ok(await _service.AddAsync(reading));
}
```

`backend/src/SmartGreenhouse.Api/Controllers/DevicesController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartGreenhouse.Domain.Entities;
using SmartGreenhouse.Infrastructure.Data;

namespace SmartGreenhouse.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly AppDbContext _db;
    public DevicesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _db.Devices.AsNoTracking().ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create(Device device)
    {
        _db.Devices.Add(device);
        await _db.SaveChangesAsync();
        return Ok(device);
    }
}
```


---

### A5) Start PostgreSQL

Create a new database called `greenhouse`, either with PgAdmin tool or using `psql` terminal commands

---

### A6) Create DB schema (migrations)

```bash
dotnet tool install --global dotnet-ef
```

 - **Explanation:** Install EF **CLI** tooling (one-time per machine).

```bash
cd backend/src
```

 - **Explanation:** Ensure the **project directories** are the working dir (the `-p` and `-s` paths below are resolved from here).

```bash
dotnet ef migrations add InitialCreate -p SmartGreenhouse.Infrastructure -s SmartGreenhouse.Api -o Data/Migrations
```

 - **Explanation:** Generate a migration in the **Infrastructure** project (`-p`), using the **API** project as the **startup** (`-s`) so it can read `appsettings.json` and DI. Output files into `Data/Migrations`.

```bash
dotnet ef database update -p SmartGreenhouse.Infrastructure -s SmartGreenhouse.Api
```

 - **Explanation:** Apply the migrations to the Postgres database defined by the API's connection string.

---

### A7) Build & run API on port 5080

```bash
cd ../..   # back to backend/
dotnet build
```

 - **Explanation:** Compile all projects in the solution to ensure everything links.

```bash
cd src/SmartGreenhouse.Api
dotnet run
```

 - **Explanation:** Start Kestrel and bind the API to **[http://localhost:5080](http://localhost:5080/)** so the frontend dev proxy can reach it.

### A8) Add some data (for UI test)

With your backend running:

Create a device
```bash
curl -X POST http://localhost:5080/api/devices 
  -H "Content-Type: application/json" 
  -d '{ "deviceName": "Greenhouse Pi" }'
```

Add a reading for device #1
```bash
curl -X POST http://localhost:5080/api/readings 
  -H "Content-Type: application/json" 
  -d '{
    "deviceId": 1,
    "sensorType":"temp",
    "value":23.5,
    "unit":"°C",
    "timestamp":"2025-09-09T11:00:00Z"
  }'
```

or alternatively you can use Postman to perform the API call
 
 - Type: `POST`
 - Url: `http://localhost:5080/api/devices `
 - Add new key value to headers: `Content-Type: application/json`
 - Body: `{ "deviceName": "Greenhouse Pi" }`

and

 - Type: `POST`
 - Url: `http://localhost:5080/api/readings `
 - Add new key value to headers: `Content-Type: application/json`
 - Body: `{
    "deviceId": 1,
    "sensorType":"temp",
    "value":23.5,
    "unit":"°C",
    "timestamp":"2025-09-09T11:00:00Z"
  }`

or alternatively you can use the Swagger to perform the necessary calls

---

## Part B — Frontend (React + Vite + TypeScript)

### B1) Scaffold app

> [!NOTE]
> If prompted to select framework choose React and for variant select TypeScript

```bash
mkdir -p frontend
cd frontend
npm create vite@latest smart-greenhouse-web -- --template react-ts
cd smart-greenhouse-web
npm install
```

 - **Explanation:** Create a React + TypeScript app using **Vite** and install dependencies.

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

 - **Explanation:** Forward `/api` requests from the dev server to the backend so we avoid CORS in development.

### B4) Minimal API client and UI

Create **`src/api/greenhouse.ts`**:

```ts
export type Reading = {
  id: number
  deviceId: number        // FK -> Device.Id
  sensorType: string      // temp|humidity|light|soilMoisture
  value: number
  unit: string            // °C|%|lux|%
  timestamp: string       // ISO-8601
}

/**
 * Fetch readings, optionally filtered by deviceId and sensorType.
 */
export async function fetchReadings(params?: { deviceId?: number; sensorType?: string }) {
  const search = new URLSearchParams()
  if (params?.deviceId !== undefined) search.set("deviceId", String(params.deviceId))
  if (params?.sensorType) search.set("sensorType", params.sensorType)

  const url = `/api/readings${search.toString() ? `?${search.toString()}` : ""}`
  const res = await fetch(url)
  if (!res.ok) throw new Error(`Failed to load readings: ${res.status}`)
  return (await res.json()) as Reading[]
}
```

 - **What this does:** provides a typed function to call your backend’s `/api/readings`.

Create **`src/api/devices.ts`**:

```ts
export type Device = {
  id: number
  deviceName: string
  createdAt: string
}

export async function fetchDevices() {
  const res = await fetch('/api/devices')
  if (!res.ok) throw new Error(`Failed to load devices: ${res.status}`)
  return (await res.json()) as Device[]
}
```

 - **What this does:** provides a typed function to call your backend’s `/api/devices`.

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
import { fetchDevices, Device } from '@/api/devices'
import { SensorCard } from '@/components/SensorCard'

export default function Dashboard() {
  const [devices, setDevices] = useState<Device[]>([])
  const [selectedDeviceId, setSelectedDeviceId] = useState<number | undefined>(undefined)
  const [readings, setReadings] = useState<Reading[]>([])
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  // Load devices on mount
  useEffect(() => {
    fetchDevices()
      .then(d => {
        setDevices(d)
        // pick first device by default if available
        if (d.length > 0) setSelectedDeviceId(d[0].id)
      })
      .catch(e => setError(String(e)))
  }, [])

  // Load readings whenever selected device changes
  useEffect(() => {
    if (selectedDeviceId === undefined) {
      setReadings([])
      setLoading(false)
      return
    }
    setLoading(true)
    fetchReadings({ deviceId: selectedDeviceId })
      .then(r => setReadings(r))
      .catch(e => setError(String(e)))
      .finally(() => setLoading(false))
  }, [selectedDeviceId])

  const latestByType = useMemo(() => {
    const map = new Map<string, Reading>()
    for (const r of readings) {
      const prev = map.get(r.sensorType)
      if (!prev || prev.timestamp < r.timestamp) map.set(r.sensorType, r)
    }
    return map
  }, [readings])

  return (
    <div className="p-6 space-y-6">
      {/* Header + device picker */}
      <div className="flex flex-col sm:flex-row sm:items-center gap-3">
        <h1 className="text-2xl font-bold">Smart Greenhouse</h1>
        <div className="sm:ml-auto">
          <label className="mr-2 text-sm text-gray-600">Device</label>
          <select
            className="border rounded-lg px-3 py-2 bg-white"
            value={selectedDeviceId ?? ''}
            onChange={e => setSelectedDeviceId(e.target.value ? Number(e.target.value) : undefined)}
          >
            {devices.length === 0 && <option value="">No devices</option>}
            {devices.map(d => (
              <option key={d.id} value={d.id}>{d.deviceName} (#{d.id})</option>
            ))}
          </select>
        </div>
      </div>

      {error && <div className="text-red-600">{error}</div>}
      {devices.length === 0 && (
        <div className="text-gray-600">
          No devices found. Create one via Swagger (<code>/api/devices</code>) or cURL (see examples below).
        </div>
      )}
      {loading && <div className="text-gray-500">Loading readings…</div>}

      {/* Sensor cards */}
      {!loading && (
        <div className="grid gap-4 grid-cols-1 sm:grid-cols-2 lg:grid-cols-4">
          <SensorCard title="Temperature"    value={latestByType.get('temp')?.value}         unit={latestByType.get('temp')?.unit} />
          <SensorCard title="Humidity"       value={latestByType.get('humidity')?.value}     unit={latestByType.get('humidity')?.unit} />
          <SensorCard title="Light"          value={latestByType.get('light')?.value}        unit={latestByType.get('light')?.unit} />
          <SensorCard title="Soil Moisture"  value={latestByType.get('soilMoisture')?.value} unit={latestByType.get('soilMoisture')?.unit} />
        </div>
      )}
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

 - **Explanation:** Start Vite dev server at **[http://localhost:5173](http://localhost:5173/)**. It proxies `/api` to the backend.

---

### B8) Verify end-to-end

- Visit **[http://localhost:5080/swagger](http://localhost:5080/swagger)** and call `GET /api/readings` — you should see data.
    
- Open the frontend at **[http://localhost:5173](http://localhost:5173/)**; cards should show the latest values.
    
- Add another reading via Swagger or `curl`, then refresh the frontend to confirm it updates.
    


---

## Troubleshooting

- **CORS errors**: Ensure the Vite proxy is configured, or enable CORS in the API.
    
- **DB connection errors**: Verify Postgres container is running and credentials match `appsettings.json`.
    
- **404 on `/api/*`**: Confirm the API runs on `http://localhost:5080` and controller routes are `api/[controller]`.
    
- **Type mismatches**: Align the `Reading` TypeScript type with the backend JSON fields.
    

