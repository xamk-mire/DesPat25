# **Assignment 3: Implementing Observer & Strategy Patterns**


---

### **Introduction**

In this assignment, you will continue developing the **Smart Greenhouse IoT System** by introducing two key **object-oriented design patterns** — the **Observer** pattern and the **Strategy** pattern.

The Smart Greenhouse is now capable of collecting and storing sensor readings through devices that measure conditions such as temperature, humidity, and soil moisture. However, the system still lacks _intelligence_:

- It doesn’t yet respond automatically when something unusual happens (for example, when the temperature gets too high).
    
- It also doesn’t know _how_ to decide which actions to take to maintain optimal growing conditions.
    

This assignment focuses on solving those two problems by adding **reactive alerts** and **dynamic control policies**.

---

### **Why These Patterns Matter**

- The **Observer pattern** allows the system to _react automatically_ to events — such as new sensor readings — without hard-coding the logic into the main program. This makes the alerting system flexible and extensible.  
    For example, new observers can be added later to send email alerts, log to a dashboard, or push messages to a notification service.
    
- The **Strategy pattern** enables the system to _choose between different algorithms or policies at runtime_.  
    Each greenhouse device can follow its own control strategy (for example, one device might use a simple on/off cooling policy, while another uses a gradual irrigation approach).  
    This pattern separates the _decision-making logic_ from the main control flow, making it easier to update and experiment with new behaviors.
    

Together, these patterns transform the Smart Greenhouse from a **data collection system** into a **reactive and adaptive control system** — one that not only records environmental data but also responds to it intelligently.

---

## 🎯 **Assignment Overview**

In this assignment, you’ll enhance your **Smart Greenhouse** backend by introducing two important **object-oriented design patterns**:

1. **Observer Pattern** — used to create an event-driven alerting system that reacts automatically when sensor readings exceed defined thresholds.
    
2. **Strategy Pattern** — used to configure different control algorithms for greenhouse actuators (fans, pumps, lights, etc.) based on sensor data.
    

By the end, your system will be:

- **Reactive**, generating alerts dynamically.
    
- **Configurable**, allowing you to switch control strategies at runtime.
    
- **Extensible**, with new alerts or strategies added without touching existing code.
    

---

## 🧱 **Repository Layout (After Assignment 3)**

🆕 = new or modified files.

```
backend/
├── SmartGreenhouse.sln
├── SmartGreenhouse.Domain/
│   └── Entities/
│       ├── Device.cs
│       ├── SensorReading.cs
│       ├── 🆕 AlertRule.cs
│       ├── 🆕 AlertNotification.cs
│       └── 🆕 ControlProfile.cs
│
├── SmartGreenhouse.Infrastructure/
│   └── Data/
│       ├── AppDbContext.cs  (🆕 updated DbSets)
│
├── SmartGreenhouse.Application/
│   ├── Abstractions/🆕 ObserverContracts.cs
│   ├── Events/
│   │   ├── 🆕 ReadingPublisher.cs
│   │   └── Observers/
│   │       ├── 🆕 LogObserver.cs
│   │       └── 🆕 AlertRuleObserver.cs
│   ├── Control/
│   │   ├── 🆕 ControlContracts.cs
│   │   ├── 🆕 HysteresisCoolingStrategy.cs
│   │   ├── 🆕 MoistureTopUpStrategy.cs
│   │   ├── 🆕 ControlStrategySelector.cs
│   │   └── 🆕 ControlService.cs
│   └── Services/🆕 CaptureReadingService.cs (updated)
│
├── SmartGreenhouse.Api/
│   ├── Contracts/
│   │   ├── CaptureReadingRequest.cs
│   │   ├── 🆕 UpsertAlertRuleRequest.cs
│   │   ├── 🆕 SetControlProfileRequest.cs
│   │   └── 🆕 EvaluateControlRequest.cs
│   ├── Controllers/
│   │   ├── DevicesController.cs
│   │   ├── ReadingsController.cs (🆕 updated capture)
│   │   ├── 🆕 AlertRulesController.cs
│   │   ├── 🆕 AlertsController.cs
│   │   └── 🆕 ControlController.cs
│   └── Program.cs (🆕 updated DI)
```

---

## ⚙️ **Step-by-Step Implementation Guide**

> Build after each step to catch errors early.

---

## Step 0 — Baseline check (no code changes)

**Goal:** Verify that the Assignment 2 solution runs correctly before making changes. This ensures you don’t confuse new bugs with old setup issues.

- `cd backend/src/SmartGreenhouse.Api`
    
- `dotnet run` → open `http://localhost:5080/swagger`
    

---

### **Step 1 — Add New Domain Entities**

**🎯 Goal:**  
Define database tables for alerts and control configurations.

**Learning goals:**  
How to model data to support both event-driven alerts and flexible control logic.

**Tasks:**

1. Create three new entity classes:
    
    - `AlertRule` → stores device + sensor + operator + threshold.
        
    - `AlertNotification` → records triggered alerts.
        
    - `ControlProfile` → maps a device to a chosen control strategy + parameters.
        
2. Add these `DbSet<>`s to `AppDbContext`.
    
3. Add sensible indexes (e.g. on `DeviceId` and `SensorType`).
    

---

### **Step 2 — Implement the Observer Pattern (Alerts)**

**🎯 Goal:**  
Make the system automatically react when new sensor readings are captured—without putting alert logic inside the capture service.

**Learning goals:**  
How to decouple “event producers” (who raise events) from “event consumers” (who respond) using the Observer pattern.

---

#### 🧩 2.1 Create Observer Contracts

Create a new file `Application/Abstractions/ObserverContracts.cs` with:

- **`IReadingEvent`** → represents a reading event (DeviceId, SensorType, Value, Timestamp).
    
- **`IReadingObserver`** → defines a listener that reacts to readings.
    
- **`IReadingPublisher`** → publishes events to all observers.
    

**Explanation:**  
These interfaces formalize communication between the _publisher_ and its _observers_.  
By defining contracts, you can easily add new observers (like an email notifier) later without editing the publisher.

---

#### 🧠 2.2 Implement the Publisher (ReadingPublisher)

Create `Application/Events/ReadingPublisher.cs`.

It loops through all registered `IReadingObserver` instances and calls their `OnReadingAsync`.

**Explanation:**  
This acts as the “subject” in the Observer pattern.  
It doesn’t care what each observer does—it just delivers the message.  
This means you can add or remove observers freely.

---

#### 🔍 2.3 Add Observers

Create a folder `Application/Events/Observers/` and add:

1. **`LogObserver.cs`**
    
    - Simply writes the reading data to the console for debugging.
        
    - Helps verify the event system works before more complex observers are added.
        
2. **`AlertRuleObserver.cs`**
    
    - Queries the database for active rules matching the device + sensor type.
        
    - Compares the new reading value against each rule’s threshold.
        
    - If a rule triggers, creates an `AlertNotification` record in the database.
        

**Explanation:**  
The `AlertRuleObserver` embodies the real-world use of the Observer pattern:  
whenever a reading event occurs, it automatically checks and reacts.  
You can later extend it to send emails, push notifications, or MQTT messages—all without changing the reading-capture logic.

---

#### ⚙️ 2.4 Update CaptureReadingService

Open `Application/Services/CaptureReadingService.cs`.

After saving a reading to the database, create a new ReadingEvent:

Example: 
```csharp
await _publisher.PublishAsync(new ReadingEvent(
    reading.DeviceId, reading.SensorType, reading.Value, reading.Timestamp
));
```

**Explanation:**  
This line _emits an event_ whenever a reading is stored.  
The publisher then notifies all subscribed observers (LogObserver, AlertRuleObserver, etc.).

---

#### 🧩 2.5 Register Everything in Program.cs

Add registrations for:

- `ReadingPublisher`
- `LogObserver`
- `AlertRuleObserver`

**Explanation:**  
They should be registered as _scoped_ because observers depend on `AppDbContext` (also scoped).  
This prevents lifetime mismatches during EF migrations or runtime.

---

### **Step 3 — Implement the Strategy Pattern (Control Policies)**

**🎯 Goal:**  
Allow different devices to follow different control rules (e.g., how to cool vs. how to irrigate) without rewriting the logic.

**Learning goals:**  
How to encapsulate interchangeable algorithms (strategies) behind a common interface and select them at runtime.

---

#### 🧩 3.1 Create Strategy Contracts

In `Application/Control/ControlContracts.cs`, define:

- **`IControlStrategy`** — interface for all control algorithms (method: `EvaluateAsync`).
    
- **`ControlContext`** — holds device readings + optional parameters.
    
- **`ActuatorCommand`** — describes an action (e.g., “Fan On”, “Pump Off”).
    

**Explanation:**  
This abstraction allows the program to evaluate _any_ control strategy the same way.  
Strategies can later handle heating, lighting, irrigation, or new actuators.

---

#### ⚙️ 3.2 Implement Concrete Strategies

Create two classes under `Application/Control/`:

1. **`HysteresisCoolingStrategy.cs`**
    
    - If temperature ≥ 26 → return `Fan On`.
        
    - If temperature ≤ 24 → return `Fan Off`.
        
    - Otherwise, no action.
        
2. **`MoistureTopUpStrategy.cs`**
    
    - If soil moisture < 30% → return `Pump On`.
        
    - Else → `Pump Off`.
        

**Explanation:**  
Each strategy encapsulates one decision algorithm.  
They implement the same interface, so they’re interchangeable.  
A new strategy (e.g. “LightingMaintainStrategy”) could be added later with zero code changes elsewhere.

---

#### 🧠 3.3 Implement the Strategy Selector

Add `Application/Control/ControlStrategySelector.cs`.

It loads the device’s `ControlProfile` from the database and resolves the right strategy instance.

- If the device has no profile, default to `HysteresisCooling`.
    
- Parse `ParametersJson` (if present) to load custom thresholds (e.g., `onAbove: 28`, `offBelow: 25`).
    

**Explanation:**  
This class is the “brain” that links data (storage) to behavior (strategy).  
It decides _which_ strategy should run for each device.

---

#### 🧩 3.4 Create ControlService

Add `Application/Control/ControlService.cs`.

- Loads the most recent readings for a device.
    
- Groups them by sensor type and passes to the selector.
    
- Executes the chosen strategy and returns `ActuatorCommand[]`.
    

**Explanation:**  
This service acts as the “controller brain” that applies strategies to real data.  
You can use it to trigger actuators or just preview suggested actions in the UI.

---

#### ⚙️ 3.5 Register Control Services and Strategies in Program.cs

Add registrations for:

- `HysteresisCoolingStrategy`
- `MoistureTopUpStrategy`
- `ControlStrategySelector`
- `ControlService`

**Explanation:**  
Strategies can be registered as singletons (they should hold no state).  
The selector and service are scoped (since they access the database).

---

#### 🧠 3.6 Test Strategy Evaluation

After implementing endpoints, test by:

1. Setting a control profile for the device.
    
2. Calling `/api/control/evaluate`.
    
3. Inspecting returned `ActuatorCommand[]`.
    

**Example Result:**

```json
[
  { "actuatorName": "Fan", "action": "On" }
]
```

**Explanation:**  
This proves your system can apply different control policies dynamically.

---

### **Step 4: Add New API Endpoints**

**🎯 Goal:**  
Expose REST API endpoints to manage alert rules, notifications, and control profiles.

**Learning goals:**  
How to create clean and reusable API endpoints with DTOs (Data Transfer Objects).

**Tasks:**

1. Add **controllers:**
    
    - `AlertRulesController` → create/list alert rules.
        
    - `AlertsController` → list triggered notifications.
        
    - `ControlController` → set and evaluate control profiles.
        
2. Add **DTOs** in `Api/Contracts`:
    
    - `UpsertAlertRuleRequest`
        
    - `SetControlProfileRequest`
        
    - `EvaluateControlRequest`
        

**Result:**  
You can configure alerts, trigger readings, and evaluate control actions directly through the API.

---

### **Step 5: Apply Database Migration**

**🎯 Goal:**  
Update your database schema to include new entities for alerts and control profiles.

**Learning goals:**  
How to evolve your EF Core model while maintaining data integrity.

**Commands:**

```bash
dotnet ef migrations add A3_AlertsAndControl \
  -p SmartGreenhouse.Infrastructure \
  -s SmartGreenhouse.Api \
  -o Data/Migrations

dotnet ef database update \
  -p SmartGreenhouse.Infrastructure \
  -s SmartGreenhouse.Api
```

If you get DI errors, make sure `ReadingPublisher` and `IReadingObserver`s are **scoped**, not singleton.

---

### **Step 6: Test Everything (Quick Smoke Tests)**

**🎯 Goal:**  
Verify that alerts trigger correctly and control strategies return actuator commands.

**Learning goals:**  
How to manually test your endpoints using JSON requests.

**Commands:**

```bash
# Create an alert rule
curl -X POST http://localhost:5080/api/alertrules \
  -H "Content-Type: application/json" \
  -d '{ "deviceId": 1, "sensorType": "Temperature", "operatorSymbol": ">", "threshold": 26, "isActive": true }'

# Capture a temperature reading
curl -X POST http://localhost:5080/api/readings/capture \
  -H "Content-Type: application/json" \
  -d '{ "deviceId": 1, "sensorFamily": "Temperature" }'

# Check alerts
curl http://localhost:5080/api/alerts?deviceId=1

# Set control profile
curl -X POST http://localhost:5080/api/control/profile \
  -H "Content-Type: application/json" \
  -d '{ "deviceId": 1, "strategyKey": "HysteresisCooling", "parameters": { "onAbove": 26, "offBelow": 24 } }'

# Evaluate control
curl -X POST http://localhost:5080/api/control/evaluate \
  -H "Content-Type: application/json" \
  -d '{ "deviceId": 1 }'
```

---

## 📖 **Concept Summaries**

### **Observer Pattern**

- Decouples event generation (reading capture) from event handling (alert creation).
    
- Makes the system extendable (new observers → no core changes).
    

### **Strategy Pattern**

- Encapsulates control algorithms as independent classes.
    
- Allows swapping strategies per device via data only (no code change).
    

---

## 🧠 **Reflection Questions**

1. How does the Observer pattern improve separation of concerns in the capture flow?
    
2. Why is the Strategy pattern ideal for customizable device behavior?
    
3. How could you extend either pattern to support real hardware actuators or external alert systems?
    
4. What would go wrong if you hard-coded all logic inside controllers instead?
    

---
