#  Assignment 4 — State & Adapter Patterns in the Smart Greenhouse

### **Introduction**

In the previous assignments, you enhanced the Smart Greenhouse by introducing alerting logic (Observer pattern) and flexible control algorithms (Strategy pattern). These improvements allowed the system to react to sensor events and choose appropriate control behaviors dynamically.

In this assignment, you will build on those foundations and take the system one step further toward functioning as a real autonomous greenhouse controller. This time, you will introduce two new software design patterns: the **State pattern** and the **Adapter pattern**.


---

### **Why These Patterns Matter**

#### ✅ State Pattern — Device Operating Modes

Greenhouses in the real world operate in **different modes depending on environmental conditions** — cooling when it’s hot, irrigating when soil is dry, or staying idle when everything is stable. The State pattern lets each device in the system:

- Maintain memory of its current operating mode
    
- Transition between modes based on sensor inputs
    
- Execute behavior specific to the active mode
    

By implementing this, your greenhouse will evolve from reacting to individual readings into **managing ongoing behavior over time** — much like real automation systems.

#### ✅ Adapter Pattern — Hardware Flexibility

IoT systems often need to support different hardware devices and communication methods. Instead of tying the code to a single mechanism (like console output or HTTP calls), you’ll implement an Adapter layer to support interchangeable actuation options, such as:

- Simulated outputs (development/testing)
    
- Console outputs (debugging)
    
- HTTP-based device control (cloud services or API-driven IoT devices)
    
- (later) MQTT-based device control (real microcontrollers like ESP32, Raspberry Pi)
    

The Adapter pattern ensures your core logic stays stable while your system becomes **hardware-agnostic and easily extendable**.

---

## 🎯 Assignment Overview

- Introduce a **State machine** that models how the greenhouse behaves over time (e.g., Idle → Cooling → Irrigating → Alarm).
    
- Add **Adapters** to abstract external integrations (e.g., actuators over HTTP/Simulated and notifications via webhook/email), so core code is independent of vendor APIs.
    
- Expose small APIs to **drive the state machine** and **execute adapted actions**.
    
- Persist **state snapshots/history** for traceability and debugging.
    

After finishing this assignment, your system will behave like a **fully autonomous greenhouse controller**, capable of:

- Monitoring conditions
    
- Tracking state over time
    
- Selecting control actions intelligently
    
- Communicating with diverse actuator systems
    

This assignment gives you deeper insight into how **industrial IoT systems**, smart homes, and building automation controllers maintain continuous awareness and react intelligently to changing environments.

---

## 🧱 Repository Layout (after Assignment 4)

🆕 = new or modified in A4

```
backend/
├─ SmartGreenhouse.Domain/
│  └─ Entities/
│     ├─ Device.cs
│     ├─ SensorReading.cs
│     ├─ AlertRule.cs
│     ├─ AlertNotification.cs
│     ├─ ControlProfile.cs
│     └─ 🆕 DeviceStateSnapshot.cs
│
├─ SmartGreenhouse.Infrastructure/
│  └─ Data/
│     ├─ AppDbContext.cs               (🆕 add DbSet + model config)
│     ├─ DesignTimeDbContextFactory.cs
│     └─ Migrations/                   (new migration after A4)
│
├─ SmartGreenhouse.Application/
│  ├─ Abstractions/
│  │  ├─ ObserverContracts.cs
│  │  └─ 🆕 IActuatorAdapter.cs
│  │  └─ 🆕 INotificationAdapter.cs
│  ├─ DeviceIntegration/
│  │  ├─ (from A2) factories & resolver
│  ├─ Events/
│  │  ├─ ReadingPublisher.cs
│  │  └─ Observers/ (AlertRuleObserver, LogObserver)
│  ├─ Control/
│  │  ├─ ControlContracts.cs
│  │  ├─ HysteresisCoolingStrategy.cs
│  │  ├─ MoistureTopUpStrategy.cs
│  │  ├─ ControlStrategySelector.cs
│  │  └─ ControlService.cs
│  ├─ 🆕 State/
│  │  ├─ States/
│  │  │  ├─ 🆕 IdleState.cs
│  │  │  ├─ 🆕 CoolingState.cs
│  │  │  ├─ 🆕 IrrigatingState.cs
│  │  │  └─ 🆕 AlarmState.cs
│  │  ├─ 🆕 GreenhouseStateContext.cs
│  │  ├─ 🆕 IGreenhouseState.cs
│  │  └─ 🆕 GreenhouseStateEngine.cs
│  ├─ 🆕 Adapters/
│  │  ├─ Actuators/
│  │  │  ├─ 🆕 SimulatedActuatorAdapter.cs
│  │  │  ├─ 🆕 HttpActuatorAdapter.cs
│  │  └─ Notifications/
│  │  │  ├─ 🆕 ConsoleNotificationAdapter.cs
│  │  │  └─ 🆕 WebhookNotificationAdapter.cs
│  │  └─ 🆕 AdapterRegistry
│  └─ Services/
│     ├─ CaptureReadingService.cs (publishes events)
│     └─ 🆕 StateService.cs        (orchestrates state engine + persistence)
│
├─ SmartGreenhouse.Api/
│  ├─ Contracts/
│  │  ├─ CaptureReadingRequest.cs
│  │  ├─ UpsertAlertRuleRequest.cs
│  │  ├─ SetControlProfileRequest.cs
│  │  ├─ EvaluateControlRequest.cs
│  │  ├─ 🆕 RunStateTickRequest.cs
│  │  └─ 🆕 AdapterSettingsRequest.cs
│  ├─ Controllers/
│  │  ├─ DevicesController.cs
│  │  ├─ ReadingsController.cs
│  │  ├─ AlertRulesController.cs
│  │  ├─ AlertsController.cs
│  │  ├─ ControlController.cs
│  │  └─ 🆕 StateController.cs        (tick / current / history)
│  └─ Program.cs (🆕 register adapters, state engine/services)
```

---

## ⚙️ Step-by-Step (with mini-goals + detailed tasks)

### Step 1 — New persistence for state

**Goal (why):** Keep a record of what state each device is in and when it changes (useful for audits and debugging).

**Tasks:**

- Add **DeviceStateSnapshot** to Domain:
    
    - `Id (int)`, `DeviceId (int)`, `StateName (string)`, `EnteredAt (DateTime)`, `Notes (string?)`.
        
- Update `AppDbContext`:
    
    - `DbSet<DeviceStateSnapshot> DeviceStates`
        
    - Index on `(DeviceId, EnteredAt DESC)`.
        
- Create & apply migration.
    

---

### Step 2 — State Pattern (Greenhouse lifecycle)

**Goal (why):** Model device behavior as **explicit states** with transitions (Idle → Cooling / Irrigating → Idle; any → Alarm). It keeps transition rules out of controllers/services.

**Concept:**

- `IGreenhouseState` = interface with `Task<TransitionResult> TickAsync(Context)`
    
- Each state decides: **what actions to take now** (e.g., apply actuator commands) and **which state to go next**.
    

**Minimal types:**

- `GreenhouseStateContext` holds:
    
    - `DeviceId`
        
    - Latest readings (you can reuse ControlService logic)
        
    - Access to adapters (actuator + notifications)
        
    - Thresholds (you may reuse Strategy parameters or put simple constants).
        
- `TransitionResult`:
    
    - `NextStateName`
        
    - `IReadOnlyList<ActuatorCommand> Commands`
        
    - `string? Note`
        

**States to implement (suggested):**

- `IdleState` — do nothing unless a need is detected (e.g., temp high or moisture low).
    
- `CoolingState` — request fan **On** until temp ≤ target; otherwise stay.
    
- `IrrigatingState` — request pump **On** until soil moisture ≥ target; otherwise stay.
    
- `AlarmState` — notify (via notification adapter), request safe actions (fan off, pump off), then return to Idle when safe.
    

**Engine & service:**

- `GreenhouseStateEngine`:
    
    - `Task<TransitionResult> TickAsync(deviceId)`
        
    - Reads current/latest state (last snapshot or default to Idle).
        
    - Constructs the concrete state class, calls `TickAsync`, persists next snapshot if changed.
        
- `StateService` (Application/Services):
    
    - Wraps engine, manages EF persistence (save snapshot), and applies actuator commands via an **Actuator Adapter** (below).
        

---

### Step 3 — Adapter Pattern (external integrations)

**Goal (why):** Decouple **how** commands/notifications are executed (HTTP, Simulator) from **what** the State/Control logic wants to do.

**Install Http Extensions**

- Since `SmartGreenhouse.Application` project is a Console application, it doesn't include necessary packages, unlike the `SmartGreenhouse.Api` project. Install the necessary package into the `SmartGreenhouse.Application` project using following command in the project folder. 

```bash
dotnet add package Microsoft.Extensions.Http
```


**Actuator Adapter**

- `IActuatorAdapter`
    
    ```csharp
    public interface IActuatorAdapter
    {
        Task ApplyAsync(int deviceId, IReadOnlyList<ActuatorCommand> commands, CancellationToken ct = default);
    }
    ```
    
- Implementations:
    
    - `SimulatedActuatorAdapter` — logs intended actions.
        
    - `HttpActuatorAdapter` — POSTs to a configurable endpoint per device (e.g., `/devices/{id}/actuators`).

**Notification Adapter**

- `INotificationAdapter`
    
    ```csharp
    public interface INotificationAdapter
    {
        Task NotifyAsync(int deviceId, string title, string message, CancellationToken ct = default);
    }
    ```
    
- Implementations:
    
    - `ConsoleNotificationAdapter` — log to console.
        
    - `WebhookNotificationAdapter` — POST JSON to configured webhook URL.
 
**AdapterRegistry**

Below is an example solution for the `AdapterRegistry`

```chsarp
using System.Net.Http;
using SmartGreenhouse.Application.Abstractions;
using SmartGreenhouse.Application.Adapters.Actuators;
using SmartGreenhouse.Application.Adapters.Notifications;

namespace SmartGreenhouse.Application.Adapters;

public class AdapterRegistry
{
    private readonly SimulatedActuatorAdapter _simAct;
    private readonly HttpActuatorAdapter _httpAct;
    private readonly ConsoleNotificationAdapter _consoleNotify;
    private readonly IHttpClientFactory _httpFactory;

    private string _actuatorMode = "Simulated";
    private string _notificationMode = "Console";
    private string? _webhookUrl;

    public AdapterRegistry(
        SimulatedActuatorAdapter simAct,
        HttpActuatorAdapter httpAct,
        ConsoleNotificationAdapter consoleNotify,
        IHttpClientFactory httpFactory)
    {
        _simAct = simAct;
        _httpAct = httpAct;
        _consoleNotify = consoleNotify;
        _httpFactory = httpFactory;
    }

    public void SetActuatorMode(string mode) => _actuatorMode = mode;
    public void SetNotificationMode(string mode, string? webhookUrl = null)
    {
        _notificationMode = mode;
        _webhookUrl = webhookUrl;
    }

    public IActuatorAdapter ResolveActuator()
        => _actuatorMode switch
        {
            "Http" => _httpAct,
            _ => _simAct
        };

    public INotificationAdapter ResolveNotifier()
        => _notificationMode switch
        {
            "Webhook" => new WebhookNotificationAdapter(_httpFactory.CreateClient(), _webhookUrl ?? "http://localhost/webhook"),
            _ => _consoleNotify
        };
}

```
        

**Configuration**

- Add `AdapterSettingsRequest` DTO and `/api/state/adapters` endpoint for simple runtime switching (e.g., use Simulated vs HTTP).
    
- You can store adapter “mode” in memory for demo purposes or persist per device (bonus).
    

**Why Adapter here:**  
Strategy/State produce `ActuatorCommand`s and messages in a **domain shape**. Adapters **translate** them into whatever the vendor/protocol expects, so core logic doesn’t change when integrations change.

---

### Step 4 — APIs to drive state & adapters

**Goal (why):** Provide a minimal surface to run and observe the state machine + adapters.

**Contracts (Api/Contracts):**

- `RunStateTickRequest { int DeviceId }`
    
- `AdapterSettingsRequest { string ActuatorMode, string NotificationMode, string? WebhookUrl }`
    
    - `ActuatorMode`: `"Simulated" | "Http" | "Mqtt"`
        
    - `NotificationMode`: `"Console" | "Webhook"`
        

**Controllers:**

- `StateController`:
    
    - `POST /api/state/tick` — runs a single tick; returns `NextStateName` + `Commands`.
        
    - `GET /api/state/current?deviceId=` — returns latest snapshot.
        
    - `GET /api/state/history?deviceId=` — returns recent snapshots (e.g., last 50).
        
    - `POST /api/state/adapters` — switch adapter modes.
        
- (Optional) Extend `ControlController` to show how state + strategy can co-exist (strategy decides targets, state ensures safe transitions).
    

---

### Step 5 — DI wiring

**Goal (why):** Register states, engine, adapters with sensible lifetimes.

**Program.cs additions (sketch):**

```csharp
// HTTP clients for adapters
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<HttpActuatorAdapter>(client =>
{
    // Default external IoT hub or device controller URL (example)
    client.BaseAddress = new Uri("http://localhost:5055/");
});
builder.Services.AddHttpClient("WebhookClient");

// Adapters
builder.Services.AddSingleton<INotificationAdapter, ConsoleNotificationAdapter>();
builder.Services.AddSingleton<IActuatorAdapter, SimulatedActuatorAdapter>();
builder.Services.AddSingleton<AdapterRegistry>();

// State engine & service
builder.Services.AddScoped<GreenhouseStateEngine>();
builder.Services.AddScoped<StateService>();

// Optionally register concrete states in DI if you want to resolve by name:
builder.Services.AddScoped<IdleState>();
builder.Services.AddScoped<CoolingState>();
builder.Services.AddScoped<IrrigatingState>();
builder.Services.AddScoped<AlarmState>();
```

> Keep adapters **stateless** → Singleton is fine. Anything touching EF → Scoped.

---

### Step 6 — Migration & run

**Goal (why):** Persist state history.

Commands:

```bash
dotnet ef migrations add A4_DeviceStateSnapshots 
  -p SmartGreenhouse.Infrastructure 
  -s SmartGreenhouse.Api 
  -o Data/Migrations

dotnet ef database update 
  -p SmartGreenhouse.Infrastructure 
  -s SmartGreenhouse.Api
```

---

## 🔬 Smoke tests

1. **Set adapters (optional)**
    

```bash
curl -X POST http://localhost:5080/api/state/adapters \
  -H "Content-Type: application/json" \
  -d '{ "actuatorMode":"Simulated", "notificationMode":"Console" }'
```

2. **Ensure device & some readings exist** (reuse A2/A3 endpoints). For cooling demo, create a reading with temp high (≥ 26).
    
3. **Run a state tick**
    

```bash
curl -X POST http://localhost:5080/api/state/tick \
  -H "Content-Type: application/json" \
  -d '{ "deviceId": 1 }'
```

**Expected:** returns next state (e.g., `CoolingState`) and commands `[{"actuatorName":"Fan","action":"On"}]`. Console shows simulated actuator actions.

4. **Check current state**
    

```bash
curl "http://localhost:5080/api/state/current?deviceId=1"
```

5. **Capture new reading** (e.g., temp decreased ≤ 24), run **tick** again → expect `IdleState` with Fan Off.
    
6. **Force Alarm** (e.g., extreme high temp + low moisture), tick → expect `AlarmState`, notification via selected adapter.
    

---

## 🧠 Design notes (what you should learn)

- **State vs Strategy:**
    
    - _Strategy_ chooses actions for a condition (one shot).
        
    - _State_ models **evolving behavior over time**, including transitions and “what to do next”.
        
- **Adapter:**
    
    - Keeps your app logic independent of vendor APIs.
        
    - Swapping Simulated/HTTP/MQTT shouldn’t change State/Strategy code.
        

---

## 🌱 Summary: How the New Adapter & State Features Integrate With the System

In this stage of the Smart Greenhouse project, we transformed the system from **data-driven and reactive** (responding to sensor readings) into a **control-driven and autonomous** system capable of managing greenhouse behavior over time.

### ✅ State Machine: Device Operating Modes

The **state machine** adds lifecycle logic for each device, allowing it to move between modes such as:

- **Idle** → everything stable; no actions needed
    
- **Cooling** → fan turns on if temperature too high
    
- **Irrigating** → pump turns on if soil moisture too low
    
- **Alarm** → critical condition detected; emergency response
    

Instead of responding to just one reading, the system now **remembers previous behavior**, evaluates current conditions, and decides what to do next.  
This makes the greenhouse **autonomous**, not just reactive.

### ✅ Adapter Pattern: Flexible Actuator Outputs

The **adapter layer** controls how the system sends commands.  
Before, actions were simulated or logged.  
Now we can choose between different output mechanisms:

| Mode         | Result                                                          |
| ------------ | --------------------------------------------------------------- |
| Simulated    | Commands printed for testing                                    |
| Console      | Development debugging                                           |
| HTTP         | Calls another IoT service/device                                |
| MQTT (Later) | Communicates with real IoT hardware (ESP32, Raspberry Pi, etc.) |

By abstracting actuators behind `IActuatorAdapter`, we made it easy to **swap real hardware in and out without changing business logic**.

### 🔄 Putting It All Together

1. **Device sends sensor readings**
    
2. **System stores data**
    
3. **State engine evaluates conditions**
    
4. System **transitions to appropriate state**
    
5. State triggers **actuator commands**
    
6. **Adapter layer delivers commands**
    
    - Simulated output (dev)
        
    - HTTP actuator service
        
    - MQTT (Later) → real greenhouse hardware
        

This creates a pipeline:

```
Sensor → Backend → State Machine → Actuator Commands → Adapter → Hardware
```

### 🌟 Why this matters

These updates make the greenhouse:

- **Autonomous** — makes decisions continuously
    
- **Extensible** — new hardware modes added easily
    
- **Hardware-agnostic** — code doesn't care whether we control
    
    - a real fan/pump
        
    - a virtual device
        
    - a test script
        

---

### 🧠 In one sentence

> The new state engine decides _what to do_, and the adapter system decides _how to do it_, enabling real-world automation with flexible hardware integration.
