## Assignment 2: Extending the Smart Greenhouse with Enums and Abstract Factories


#### **Introduction**

In Assignment 1, you built a minimal backend and frontend for the Smart Greenhouse system: a simple ASP.NET Core Web API with PostgreSQL for persistence and React + Tailwind CSS frontend. Devices and sensor readings could be created, stored, and queried, forming the foundation of an IoT-style monitoring application.

In this second assignment, we take the project a step further by improving its **architecture, type safety, and extensibility**. You will:

- Replace fragile string values with **enumerations (`enum`)** for `SensorType` and `DeviceType`.
    
- Introduce the **Abstract Factory pattern** to handle different integration families of devices 
	- simulated devices for now
	- MQTT devices will be added at later point
    
- Implement a new **Capture Reading use case**, where the system actively queries a device for a fresh measurement, normalizes it, and stores it in the database.
	- For now this will be used to simulate the real scenarios where the data is captured from a real device
    
- Update the **API layer** to expose clear and safe **DTOs (Data Transfer Objects)** instead of raw entities, preventing serialization issues and stabilizing the public contract.
    
- Manage schema changes via **Entity Framework Core migrations**, or alternatively reset the database if migrations become problematic.
    

By completing this assignment, you will learn how to evolve a real-world backend system through:

- **Domain modeling with enums** for clarity and validation.
    
- **Design patterns** (Abstract Factory, DTO) for clean separation of concerns.
    
- **Service layer use cases** for orchestrating device interactions.
    
- **Database evolution techniques** for keeping code and schema in sync.
    

The end result will be a more **robust and extensible Smart Greenhouse backend** that is more ready for integration with real IoT devices.

---

### 📁 Repository layout comparison

### A) Before (end of Assignment 1 – backend only)

```
backend/
└─ src/
   ├─ SmartGreenhouse.Domain/
   │  └─ Entities/
   │     ├─ Device.cs
   │     └─ SensorReading.cs
   ├─ SmartGreenhouse.Infrastructure/
   │  └─ Data/
   │     └─ AppDbContext.cs
   ├─ SmartGreenhouse.Application/
   │  └─ Services/
   │     └─ ReadingService.cs
   └─ SmartGreenhouse.Api/
      ├─ Controllers/
      │  ├─ DevicesController.cs
      │  ├─ ReadingsController.cs
      │  └─ HealthController.cs
      ├─ Properties/
      │  └─ launchSettings.json
      ├─ Program.cs
      └─ appsettings.json
```

> Notes: `SensorReading.SensorType` is a **string**; no factories; `Device` has no device type.

---

### B) After (target for Assignment 2 - backend only)

```
backend/
└─ src/
   ├─ SmartGreenhouse.Domain/
   │  ├─ Entities/
   │  │  ├─ Device.cs                          (UPDATED: adds DeviceType enum)
   │  │  └─ SensorReading.cs                   (UPDATED: SensorType -> enum)
   │  └─ Enums/
   │     ├─ SensorTypeEnum.cs                  (NEW)
   │     └─ DeviceTypeEnum.cs                  (NEW)
   ├─ SmartGreenhouse.Infrastructure/
   │  └─ Data/
   │     ├─ AppDbContext.cs                    (UPDATED: indexes/relations)
   │     ├─ DesignTimeDbContextFactory.cs      (NEW: EF design-time factory)
   │     └─ Migrations/
   │        ├─ <ts>_EnumSensorType.cs          (NEW: custom migration SensorType)
   │        └─ <ts>_AddDeviceTypeToDevice.cs   (NEW: migration to add DeviceType)
   ├─ SmartGreenhouse.Application/
   │  ├─ Abstractions/
   │  │  ├─ ISensorReader.cs                   (NEW)
   │  │  ├─ IActuatorController.cs             (NEW)
   │  │  ├─ IDeviceIntegrationFactory.cs       (NEW)
   │  │  └─ ISensorNormalizer.cs               (NEW)
   │  ├─ DeviceIntegration/
   │  │  ├─ SimulatedDeviceFactory.cs          (NEW)
   │  │  └─ DeviceFactoryResolver.cs           (NEW)
   │  ├─ Factories/
   │  │  └─ SensorNormalizerFactory.cs         (NEW)
   │  └─ Services/
   │     ├─ CaptureReadingService.cs           (NEW)
   │     └─ ReadingService.cs                  (UPDATED: enum filter)
   └─ SmartGreenhouse.Api/
      ├─ Contracts/
      │  └─ CaptureReadingRequest.cs           (NEW)
      │  ├─ ReadingDto.cs                      (NEW)
      │  └─ DeviceDto.cs                       (NEW)
      ├─ Controllers/
      │  ├─ ReadingsController.cs              (UPDATED: enum + capture, ReadingDto)
      │  └─ DevicesController.cs               (UPDATED: accept DeviceType, DeviceDto)
      ├─ Properties/
      │  └─ launchSettings.json                (unchanged)
      ├─ Program.cs                            (UPDATED: enum JSON + DI)
      └─ appsettings.json                      (unchanged)
```

---

# 🧭 Step-by-step implementation

> Build after each step to catch errors early.

---

## Step 0 — Baseline check (no code changes)

**Goal:** Verify that the Assignment 1 API runs correctly before making changes. This ensures you don’t confuse new bugs with old setup issues.

- `cd backend/src/SmartGreenhouse.Api`
    
- `dotnet run` → open `http://localhost:5080/swagger`
    

---

## Step 1 — Strongly-typed **sensor types** (Domain)

**Goal:** Replace the string-based `SensorType` with an enum to improve type safety, prevent typos, and make API contracts clearer. This also prepares the system for a safe schema migration.

### 1.1 `Domain/Enums/SensorTypeEnum.cs` **(NEW)**

**Implement:**

- Enum members: `Temperature`, `Humidity`, `Light`, `SoilMoisture`.

### 1.2 `Domain/Entities/SensorReading.cs` **(UPDATED)**

**Implement:**

- Replace `public string SensorType { get; set; }` with  
    `public SensorTypeEnum SensorType { get; set; }`.
    
- Keep `Id`, `DeviceId`, `Value`, `Unit`, `Timestamp`, and `Device` navigation unchanged.
    

> EF maps enums to **int** by default. We’ll migrate existing text data in Step 8.

---

## Step 2 — Add **device type** enum & apply to Device (Domain)

**Goal:** Add a `DeviceType` enum so every device declares how it integrates with the system (e.g., simulated or MQTT). This allows the resolver to pick the right factory later.

### 2.1 `Domain/Enums/DeviceTypeEnum.cs` **(NEW)**

**Implement:**

- Enum with members and docs:
    
    - `Simulated = 0` — in-process simulated values (no hardware).
        
    - `MqttEdge = 1` — values via MQTT edge/broker.
        
    - (Leave comment for future types, e.g., `UsbProbe = 2`.)
        

### 2.2 `Domain/Entities/Device.cs` **(UPDATED)**

**Implement:**

- Add `DeviceType` property using the DeviceTypeEnum, with default `Simulated`.
    
- Keep `Id`, `DeviceName`, `CreatedAt`, `Readings` as they were.
    

> Default keeps older rows valid if clients omit `deviceType`.

---

## Step 3 — EF model (Infrastructure)

**Goal:** Ensure EF Core can manage the updated model and support migrations smoothly, while keeping queries efficient.

### 3.1 `Infrastructure/Data/AppDbContext.cs` **(UPDATED)**

**Implement:**

- Confirm relationships: `Reading.DeviceId` → `Device.Id` (cascade delete).
    
- Ensure composite index includes the enum column:  
    `HasIndex(r => new { r.DeviceId, r.SensorType, r.Timestamp });`
    
- Add an index on `Device.DeviceType`:  
    `modelBuilder.Entity<Device>().HasIndex(d => d.DeviceType);`
    
- Example:
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Device>().HasKey(d => d.Id);
    modelBuilder.Entity<Device>().Property(d => d.DeviceName).HasMaxLength(120);
    modelBuilder.Entity<Device>().HasIndex(d => d.DeviceName);
    modelBuilder.Entity<Device>().HasIndex(d => d.DeviceType); // DeviceType index

    modelBuilder.Entity<SensorReading>().HasKey(r => r.Id);
    modelBuilder.Entity<SensorReading>()
        .HasOne(r => r.Device)
        .WithMany(d => d.Readings)
        .HasForeignKey(r => r.DeviceId)
        .OnDelete(DeleteBehavior.Cascade);

    // Index includes enum column (stored as int)
    modelBuilder.Entity<SensorReading>()
        .HasIndex(r => new { r.DeviceId, r.SensorType, r.Timestamp });
}
```
    
---

## Step 4 — Contracts for factories (Application)

**Goal:** Define abstractions for reading sensor data, controlling actuators, normalizing values, and creating device families. This enforces clean separation between business logic and device integration.

### 4.1 `Application/Abstractions/ISensorReader.cs` **(NEW)**

- `Task<double> ReadAsync(int deviceId, SensorTypeEnum sensorType, CancellationToken ct = default);`
    
- `string UnitFor(SensorTypeEnum sensorType);`
    

### 4.2 `Application/Abstractions/IActuatorController.cs` **(NEW)**

- `Task SetStateAsync(int deviceId, string actuatorName, bool on, CancellationToken ct = default);`
    

### 4.3 `Application/Abstractions/IDeviceIntegrationFactory.cs` **(NEW)**

- `ISensorReader CreateSensorReader();`
    
- `IActuatorController CreateActuatorController();`
    

### 4.4 `Application/Abstractions/ISensorNormalizer.cs` **(NEW)**

- `string CanonicalUnit { get; }`
    
- `double Normalize(double raw);`
    

---

## Step 5 — Simple Factory for normalization (Application)

**Goal:** Centralize the logic of normalizing raw sensor values into canonical units (°C, %, lux). This avoids duplication and ensures consistent handling across the system.

### 5.1 `Application/Factories/SensorNormalizerFactory.cs` **(NEW)**

**Implement:**

- Static `Create(SensorTypeEnum)` returning an `ISensorNormalizer`:
    
    - Temperature → Celsius (pass-through ok)
        
    - Humidity → Percent (clamp 0–100)
        
    - Light → Lux (non-negative)
        
    - SoilMoisture → Percent (clamp 0–100)
        
- Implement small internal classes for each normalizer with `CanonicalUnit` and `Normalize`.
	
    - `CelsiusNormalizer`
	    - Unit -> `°C`
        
    - `PercentageNormalizer`
	    - Unit -> `%`
        
    - `LuxNormalizer`
	    - Unit -> `lux`

---

## Step 6 — Abstract Factory & Resolver (Application)

**Goal:** Provide concrete factories for device families and a resolver that picks the correct factory based on `DeviceType`. This makes the system extensible for future integrations (e.g., MQTT, USB).

### 6.1 `Application/DeviceIntegration/SimulatedDeviceFactory.cs` **(NEW)**

**Implement:**

- Class `SimulatedDeviceFactory : IDeviceIntegrationFactory`
    
    - `CreateSensorReader()` → reader returns plausible random values per `SensorTypeEnum`; implement `UnitFor(...)`.
        
    - `CreateActuatorController()` → no-op (or simple log) controller.

Example solution:
```csharp
public sealed class SimulatedDeviceFactory : IDeviceIntegrationFactory
{
    public ISensorReader CreateSensorReader() => new SimulatedSensorReader();

    public IActuatorController CreateActuatorController() => new SimulatedActuatorController();
}

// Example simulated sensor reader -> in future could create new ones to test/mock different devices/sensors
sealed class SimulatedSensorReader : ISensorReader
{
    private static readonly Random _rng = new();

    public Task<double> ReadAsync(
        int deviceId,
        SensorTypeEnum sensorType,
        CancellationToken ct = default
    ) =>
        Task.FromResult(
            sensorType switch
            {
                SensorTypeEnum.Temperature => 20 + _rng.NextDouble() * 6,
                SensorTypeEnum.Humidity => 40 + _rng.NextDouble() * 30,
                SensorTypeEnum.Light => 300 + _rng.NextDouble() * 600,
                SensorTypeEnum.SoilMoisture => 20 + _rng.NextDouble() * 40,
                _ => 0,
            }
        );

    public string UnitFor(SensorTypeEnum sensorType) =>
        sensorType switch
        {
            SensorTypeEnum.Temperature => "°C",
            SensorTypeEnum.Humidity => "%",
            SensorTypeEnum.Light => "lux",
            SensorTypeEnum.SoilMoisture => "%",
            _ => "",
        };
}

// Simple example actuator controller -> actuators will be added later
sealed class SimulatedActuatorController : IActuatorController
{
    public Task SetStateAsync(
        int deviceId,
        string actuatorName,
        bool on,
        CancellationToken ct = default
    )
    {
        Console.WriteLine($"[SIM] Device {deviceId}: {actuatorName} => {(on ? "ON" : "OFF")}");
        return Task.CompletedTask;
    }
}
```

### 6.2 `Application/DeviceIntegration/DeviceFactoryResolver.cs` **(NEW)**

**Implement:**

- Interface `IDeviceFactoryResolver { IDeviceIntegrationFactory Resolve(Device device); }`
    
- Class `DeviceFactoryResolver` using `IServiceProvider` to resolve based on **Device.DeviceType**:
    
    - `Simulated` → `SimulatedDeviceFactory`
        
    - `MqttEdge` → `MqttDeviceFactory` (commented out for now)
        
    - Default: throw `NotSupportedException` for unknown types
        

---

## Step 7 — Use cases & queries (Application)

**Goal:** Encapsulate business workflows (capturing readings and querying) in services. This keeps controllers lean and the application logic reusable and testable.

### 7.1 `Application/Services/CaptureReadingService.cs` **(NEW)**

**Implement:**

- Ctor: `AppDbContext`, `IDeviceFactoryResolver`.
    
- `CaptureAsync(int deviceId, SensorTypeEnum sensorType, CancellationToken ct = default)`:
    
    1. Load device (fetch device from db) or throw exception.
        
    2. Resolve factory → `ISensorReader`.
        
    3. Read raw value via `ReadAsync`.
        
    4. Normalize via `SensorNormalizerFactory.Create`.
        
    5. Create & save `SensorReading` (DeviceId, SensorType enum, Value, Unit, Timestamp=UTC now).
        
    6. Return saved reading.
        

### 7.2 `Application/Services/ReadingService.cs` **(UPDATED)**

**Implement:**

- Change signature to:  
    `QueryAsync(int? deviceId = null, SensorTypeEnum? sensorType = null, int take = 200)`
    
- Apply optional filters, order by `Timestamp DESC`, `Take(take)`.
    

---

## Step 8 — API DTOs, controllers, and JSON enum config (Api)

**Goal:** Create Data Transfer Objects (DTOs) to shape API responses, avoid JSON serializer cycles, and provide stable, clean contracts for clients.. This improves API usability and clarity.


### 8.1 `Api/Contracts/ReadingDto.cs` **(NEW)**

```csharp
using SmartGreenhouse.Domain.Enums;

namespace SmartGreenhouse.Api.Contracts;

public record ReadingDto(
    int Id,
    int DeviceId,
    SensorTypeEnum SensorType,
    double Value,
    string Unit,
    DateTime Timestamp
);
```

### 8.2 `Api/Contracts/DeviceDto.cs` **(NEW)**

```csharp
using SmartGreenhouse.Domain.Enums;

namespace SmartGreenhouse.Api.Contracts;

public record DeviceDto(
    int Id,
    string DeviceName,
    DeviceTypeEnum DeviceType,
    DateTime CreatedAt
);
```

### 8.3 `Api/Contracts/CaptureReadingRequest.cs` **(NEW)**

**Implement:**

- Record/class: `int DeviceId`, `SensorTypeEnum SensorType`.
    

### 8.4 `Api/Controllers/ReadingsController.cs` **(UPDATED)**

**Implement:**

- Inject `CaptureReadingService` & `ReadingService`. 
    
- Map entities → `ReadingDto` before returning.
	
- `GET /api/readings?deviceId=&sensorType=` — bind `SensorTypeEnum?` and pass to service.
    
- `POST /api/readings/capture` — accept `CaptureReadingRequest`, call service, return saved reading.
    
- Example:
```csharp
private readonly AppDbContext _db;
private readonly CaptureReadingService _capture;
private readonly ReadingService _reading;

public ReadingsController(AppDbContext db, CaptureReadingService capture, ReadingService reading)
{
    _db = db;
    _capture = capture;
    _reading = reading;
}

// GET /api/readings?deviceId=1&sensorType=Temperature
[HttpGet]
public async Task<IActionResult> Get([FromQuery] int? deviceId, [FromQuery] SensorTypeEnum? sensorType)
{
    var readings = await _readingService.QueryAsync(deviceId, sensorType);
    var dtos = readings.Select(r => new ReadingDto(r.Id, r.DeviceId, r.SensorType, r.Value, r.Unit, r.Timestamp));
    return Ok(dtos);
}

// POST /api/readings/capture
[HttpPost("capture")]
public async Task<IActionResult> Capture([FromBody] CaptureReadingRequest req)
{
    var r = await _captureService.CaptureAsync(req.DeviceId, req.SensorType);
    return Ok(new ReadingDto(r.Id, r.DeviceId, r.SensorType, r.Value, r.Unit, r.Timestamp));
}
```

### 8.5 `Api/Controllers/DevicesController.cs` **(UPDATED)**

**Implement:**

- Ensure POST accepts payload with optional `deviceType` (enum name). If missing, backend default (`Simulated`) is used.
	
-  Map `Device` → `DeviceDto`.
    
- Example payload: `{ "deviceName": "Greenhouse Pi", "deviceType": "Simulated" }`
    
- Example:
```csharp
private readonly AppDbContext _db;
public DevicesController(AppDbContext db) => _db = db;

[HttpGet]
public async Task<IActionResult> Get() 
{
    return Ok(await _db.Devices.AsNoTracking().Select(d => new DeviceDto(d.Id, d.DeviceName, d.DeviceType, d.CreatedAt)).ToListAsync());
}

[HttpPost]
public async Task<IActionResult> Create(Device device)
{
    _db.Devices.Add(device);
    await _db.SaveChangesAsync();
    return Ok(device);
}
```

### 8.4 `Api/Program.cs` **(UPDATED)**

**Implement:**

- Add JSON enum names:  
    `builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));`
    
- Register DI:
    
    - `AddDbContext<AppDbContext>(UseNpgsql(...))`
        
    - `AddSingleton<SimulatedDeviceFactory>()`
        
    - `AddSingleton<IDeviceFactoryResolver, DeviceFactoryResolver>()`
        
    - `AddScoped<CaptureReadingService>()`
        
    - `AddScoped<ReadingService>()`
        

---

## Step 9 — **Migrations** (Infrastructure)

**Goal:** Safely evolve the database schema to support enums for both `SensorType` and `DeviceType`, preserving existing data.

### 9A - Custom migration Custom migration for `SensorReading.SensorType` (text → enum-backed int)

**Goal:** Safely evolve the schema from Assignment 1 to the new model using migrations.

- Generate migration `EnumSensorType`, then replace its contents with the custom migration you’ve been given:
    
    - Drop old index on `(DeviceId, SensorType, Timestamp)` (text).
        
    - Add `SensorTypeInt` (int), map old string values/aliases → int enum.
        
    - Drop old `SensorType` (text), rename `SensorTypeInt` → `SensorType`.
        
    - Recreate composite index `(DeviceId, SensorType, Timestamp)`.
        

1. Generate migration `EnumSensorType`:
```bash
cd backend/src dotnet ef migrations add EnumSensorType -p SmartGreenhouse.Infrastructure -s SmartGreenhouse.Api  -o Data/Migrations
```

2. Example content for the custom migration
```csharp
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartGreenhouse.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceTypeAndFactories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Readings_DeviceId",
                table: "Readings");

            // 1) Add a temporary int column to hold enum values
            migrationBuilder.AddColumn<int>(
                name: "SensorTypeInt",
                table: "Readings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // 2) Copy/normalize data from old text column -> int column
            //    Be lenient with casing/aliases.
            migrationBuilder.Sql(@"
                UPDATE ""Readings""
                SET ""SensorTypeInt"" = CASE
                    WHEN LOWER(""SensorType"") IN ('temperature', 'temp', 't') THEN 0
                    WHEN LOWER(""SensorType"") IN ('humidity', 'hum', 'h') THEN 1
                    WHEN LOWER(""SensorType"") IN ('light', 'lux', 'l') THEN 2
                    WHEN LOWER(""SensorType"") IN ('soilmoisture', 'soil_moisture', 'soil-moisture', 'moisture', 'sm') THEN 3
                    ELSE 0 -- default/fallback to Temperature if unknown
                END;
            ");

            // 3) Drop the old text column
            migrationBuilder.DropColumn(
                name: "SensorType",
                table: "Readings");

            // 4) Rename the int column to SensorType (the enum property name)
            migrationBuilder.RenameColumn(
                name: "SensorTypeInt",
                table: "Readings",
                newName: "SensorType");

            migrationBuilder.AddColumn<int>(
                name: "DeviceType",
                table: "Devices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Readings_DeviceId_SensorType_Timestamp",
                table: "Readings",
                columns: new[] { "DeviceId", "SensorType", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Readings_DeviceId_SensorType_Timestamp",
                table: "Readings");

            migrationBuilder.DropColumn(
                name: "DeviceType",
                table: "Devices");

            // 1) Add back the old text column (default to 'Temperature' for non-null constraint)
            migrationBuilder.AddColumn<string>(
                name: "SensorType",
                table: "Readings",
                type: "text",
                nullable: false,
                defaultValue: "Temperature");

            // 2) Copy data from int enum column -> text (using enum names)
            migrationBuilder.Sql(@"
                UPDATE ""Readings""
                SET ""SensorType"" = CASE ""SensorType""
                    WHEN 0 THEN 'Temperature'
                    WHEN 1 THEN 'Humidity'
                    WHEN 2 THEN 'Light'
                    WHEN 3 THEN 'SoilMoisture'
                    ELSE 'Temperature'
                END;
            ");

            // 3) Drop the int column (currently named SensorType) by renaming then dropping
            //    We need a temporary rename to avoid name conflict with the text column.
            migrationBuilder.RenameColumn(
                name: "SensorType",
                table: "Readings",
                newName: "SensorTypeInt_Backup");

            migrationBuilder.DropColumn(
                name: "SensorTypeInt_Backup",
                table: "Readings");

            migrationBuilder.CreateIndex(
                name: "IX_Readings_DeviceId",
                table: "Readings",
                column: "DeviceId");
        }
    }
}

```


3. Apply migrations

```bash
# Replace file with your custom contents (mapping strings -> ints), then:
dotnet ef database update -p SmartGreenhouse.Infrastructure -s SmartGreenhouse.Api
```

---

### 9B - Drop & recreate DB using EF Core

**Goal:** Start fresh by deleting the current DB and letting EF rebuild it with the latest model.


1. Drop the existing database:
    
    ```bash
    dotnet ef database drop -p SmartGreenhouse.Infrastructure -s SmartGreenhouse.Api -f
    ```
    
1. Delete old migration files (the command is run in `backend/src`):
    
    ```
    rm -r SmartGreenhouse.Infrastructure/Data/Migrations/* 
    ```
    
    - Or remove the files manually by deleting them
    
2. Create a fresh migration from the current model:
    
    ```bash
    dotnet ef migrations add InitialClean -p SmartGreenhouse.Infrastructure  -s SmartGreenhouse.Api -o Data/Migrations
    ```
    
1. Rebuild the DB (will recreate the deleted database also):
    
    ```bash
    dotnet ef database update -p SmartGreenhouse.Infrastructure -s SmartGreenhouse.Api
    ```
    

---

## Step 10 — Run & verify

1. Run both frontend and backend
2. Use the swagger UI to create a device and capture a reading: `[Swagger UI](http://localhost:5080/swagger/index.html)`

**Create a device (Simulated for now):**

```bash
curl -X POST http://localhost:5080/api/devices 
  -H "Content-Type: application/json" 
  -d '{ "deviceName": "Greenhouse Pi", "deviceType": "Simulated" }'
```

**Capture a reading:**

```bash
curl -X POST http://localhost:5080/api/readings/capture 
  -H "Content-Type: application/json" 
  -d '{ "deviceId": 1, "sensorType": "Temperature" }'
```

**Query by enum name:**

```bash
curl "http://localhost:5080/api/readings?deviceId=1&sensorType=Temperature"
```

**Expected:** JSON shows `"sensorType": "Temperature"`; DB column is int.

---
