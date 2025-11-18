# 🌿 Assignment 5 — Real-Time IoT Integration with ESP32 & WebSocket Dashboard

### **Introduction**

In this assignment, you will extend the Smart Greenhouse system into a fully real-time IoT application. Your goal is to receive live sensor data from simulated ESP32 devices, process it in the backend, store it in the database, and display updates instantly in the frontend.

To achieve this, you will:

- Implement an **embedded MQTT broker** in the .NET backend
- Handle incoming MQTT messages using `Esp32MessageHandler`
- Map readings to DTOs and save them in the database
- Broadcast updates to the frontend through **WebSockets**
- Build a **live dashboard** in React that updates automatically
- Test the full system using a provided Python moisture simulator

By the end of the assignment, your backend, frontend, and simulator will work together to form a complete real-time data pipeline, just like a real-world IoT monitoring system.

---

## 🧩 Part 1 — Implement `Esp32MessageHandler` (Before MQTT Broker)

**Goal:** Build the core message-handling pipeline first, independent of MQTT and WebSockets.

### Tasks

1. **Create `IEsp32MessageHandler` interface**  
   Location: `SmartGreenhouse.Application/Mqtt/IEsp32MessageHandler.cs`

   ```csharp
   public interface IEsp32MessageHandler
   {
       Task HandleAsync(string topic, string payload, CancellationToken ct = default);
   }
   ```

2. **Create `Esp32MessageHandler` class**  
   Location: `SmartGreenhouse.Application/Mqtt/Esp32MessageHandler.cs`  
   Inject:

   - `IDbContextFactory<AppDbContext>`
   - `ILogger<Esp32MessageHandler>`
   - `IRealTimeNotifier` (from Part 2)

3. **Define internal payload model (for JSON)**

   ```csharp
   private sealed class Esp32Payload
   {
       public double Value { get; set; }
       public string Unit { get; set; } = "";
       public DateTime? Timestamp { get; set; }
   }
   ```

4. **Parse topic and payload**

   - Topic format: `greenhouse/{deviceName}/sensor/{sensorType}`
   - Use `Enum.TryParse<SensorTypeEnum>` for sensor type.
   - Deserialize payload using `System.Text.Json` (case-insensitive or via attributes).

5. **Resolve/create `Device` and save `SensorReading`**

   - Find `Device` by `DeviceName`.
   - If missing, create and save.
   - Create `SensorReading` with:
     - `DeviceId`
     - `SensorType`
     - `Value`
     - `Unit`
     - `Timestamp ?? DateTime.UtcNow`
   - Save changes.

6. **After saving, call a helper method that will later map to DTO and notify clients** (implemented in Part 2).

---

## 🧩 Part 2 — Define DTOs in Application Layer & Real-Time Notifier

**Goal:** Create DTOs in the **Application** layer and an abstraction for broadcasting them.

### Tasks

1. **Create `ReadingDto` in Application layer**  
   Location: `SmartGreenhouse.Application/Contracts/ReadingDto.cs`

   ```csharp
   using SmartGreenhouse.Domain.Enums;

   namespace SmartGreenhouse.Application.Contracts;

   public record ReadingDto(
       int Id,
       int DeviceId,
       string DeviceName,
       SensorTypeEnum SensorType,
       double Value,
       string Unit,
       DateTime Timestamp
   );
   ```

2. **Create `IRealTimeNotifier` in Application**  
   Location: `SmartGreenhouse.Application/RealTime/IRealTimeNotifier.cs`

   ```csharp
   using SmartGreenhouse.Application.Contracts;

   namespace SmartGreenhouse.Application.RealTime;

   public interface IRealTimeNotifier
   {
       Task BroadcastReadingAsync(ReadingDto dto, CancellationToken ct = default);
   }
   ```

3. **Add mapping + broadcasting to `Esp32MessageHandler`**

   After saving the `SensorReading`, map it to `ReadingDto`:

   ```csharp
   private ReadingDto MapToDto(SensorReading reading, Device device)
       => new(
           reading.Id,
           reading.DeviceId,
           device.DeviceName,
           reading.SensorType,
           reading.Value,
           reading.Unit,
           reading.Timestamp
       );
   ```

   And then:

   ```csharp
   var dtoOut = MapToDto(reading, device);
   await _realTimeNotifier.BroadcastReadingAsync(dtoOut, ct);
   ```

**Important:**  
Now the **Application layer owns the DTO and the broadcast contract**.  
The API layer will just implement the notifier over WebSockets.

---

## 🧩 Part 3 — Implement WebSocket Real-Time Broadcasting in API Layer

**Goal:** Let browsers connect via WebSocket and receive `ReadingDto` messages serialized to camelCase.

### Tasks

1. **Create `LiveReadingHub` in API layer**  
   Location: `SmartGreenhouse.Api/RealTime/LiveReadingHub.cs`

   ```csharp
   using System.Net.WebSockets;
   using System.Text;
   using System.Text.Json;
   using SmartGreenhouse.Application.Contracts;

   namespace SmartGreenhouse.Api.RealTime;

   public class LiveReadingHub
   {
       private readonly List<WebSocket> _clients = new();
       private readonly object _lock = new();

       private static readonly JsonSerializerOptions CamelCaseOptions = new()
       {
           PropertyNamingPolicy = JsonNamingPolicy.CamelCase
       };

       public void Register(WebSocket socket)
       {
           lock (_lock) _clients.Add(socket);
       }

       public void Unregister(WebSocket socket)
       {
           lock (_lock) _clients.Remove(socket);
       }

       public async Task BroadcastAsync(ReadingDto dto, CancellationToken ct = default)
       {
           var json = JsonSerializer.Serialize(dto, CamelCaseOptions);
           var bytes = Encoding.UTF8.GetBytes(json);

           List<WebSocket> snapshot;
           lock (_lock) snapshot = _clients.ToList();

           foreach (var socket in snapshot)
           {
               if (socket.State == WebSocketState.Open)
               {
                   try
                   {
                       await socket.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
                   }
                   catch
                   {
                       Unregister(socket);
                   }
               }
               else
               {
                   Unregister(socket);
               }
           }
       }
   }
   ```

2. **Implement `WebSocketRealTimeNotifier` in API layer**  
   Location: `SmartGreenhouse.Api/RealTime/WebSocketRealTimeNotifier.cs`

   ```csharp
   using SmartGreenhouse.Application.Contracts;
   using SmartGreenhouse.Application.RealTime;

   namespace SmartGreenhouse.Api.RealTime;

   public class WebSocketRealTimeNotifier : IRealTimeNotifier
   {
       private readonly LiveReadingHub _hub;

       public WebSocketRealTimeNotifier(LiveReadingHub hub)
       {
           _hub = hub;
       }

       public Task BroadcastReadingAsync(ReadingDto dto, CancellationToken ct = default)
       {
           return _hub.BroadcastAsync(dto, ct);
       }
   }
   ```

   > Note: API depends on Application for `ReadingDto` and `IRealTimeNotifier`.  
   > Application does **not** depend on API → no circular dependency.

3. **Wire WebSockets in `Program.cs`**

   ```csharp
   builder.Services.AddSingleton<LiveReadingHub>();
   builder.Services.AddSingleton<IRealTimeNotifier, WebSocketRealTimeNotifier>();

   var app = builder.Build();

   app.UseWebSockets();

   app.Map("/ws/live-readings", async context =>
   {
       if (!context.WebSockets.IsWebSocketRequest)
       {
           context.Response.StatusCode = StatusCodes.Status400BadRequest;
           return;
       }

       var hub = context.RequestServices.GetRequiredService<LiveReadingHub>();
       var socket = await context.WebSockets.AcceptWebSocketAsync();

       hub.Register(socket);

       var buffer = new byte[4096];
       while (socket.State == WebSocketState.Open)
       {
           var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
           if (result.MessageType == WebSocketMessageType.Close)
               break;
       }

       hub.Unregister(socket);
       await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
   });
   ```

---

## 🧩 Part 4 — Add Embedded MQTT Broker and Connect ESP32 Devices

**Goal:** Now that the handler & notifier are ready, wire in actual MQTT device communication.

### Tasks

1. **Install MQTTnet (if not already)**

   > [!NOTE]
   > Ensure that your MQTTnet is using the version 4 if you are using .NET 8
   > MQTTnet version 5 is not currently at the time of writing this assignment fully compatible with the .NET 8.

   > [!NOTE]
   > In additional note -> the AI tools seem to confuse MQTTnet version 4 either with version 3 or 5 because the version 4 is lacking solid documentation.

   ```bash
   dotnet add SmartGreenhouse.Api package MQTTnet --version 4.3.7.1207
   ```

2. **Create `MqttBrokerHostedService` in API layer**

   ```csharp
   using MQTTnet;
   using MQTTnet.Server;
   using System.Text;
   using SmartGreenhouse.Application.Mqtt;

   public class MqttBrokerHostedService : IHostedService
   {
       private MqttServer? _server;
       private readonly IEsp32MessageHandler _handler;

       public MqttBrokerHostedService(IEsp32MessageHandler handler)
       {
           _handler = handler;
       }

       public async Task StartAsync(CancellationToken ct)
       {
           var options = new MqttServerOptionsBuilder()
               .WithDefaultEndpoint()
               .WithDefaultEndpointPort(1883)
               .Build();

           _server = new MqttFactory().CreateMqttServer(options);

           _server.InterceptingPublishAsync += async e =>
           {
               var topic = e.ApplicationMessage.Topic ?? "";
               var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

               await _handler.HandleAsync(topic, payload, ct);
           };

           await _server.StartAsync();
       }

       public async Task StopAsync(CancellationToken ct)
       {
           if (_server != null)
               await _server.StopAsync();
       }
   }
   ```

3. **Register broker in `Program.cs`**

   ```csharp
   builder.Services.AddHostedService<MqttBrokerHostedService>();
   ```

4. **Configure ESP32**

   - `mqtt_server` = IP of machine running the .NET API
   - Port `1883`
   - Topic: `greenhouse/{deviceName}/sensor/{sensorType}`
   - Payload JSON: `{ "value": 63.2, "unit": "%", "timestamp": "..." }`

---

## 🧩 Part 5 — Frontend Live Dashboard (WebSocket Client)

**Goal:** Show live readings in the React UI as soon as they arrive.

### Tasks

1. **Create a WebSocket hook**

   ```ts
   export type LiveReading = {
     id: number;
     deviceId: number;
     deviceName: string;
     sensorType: string;
     value: number;
     unit: string;
     timestamp: string;
   };
   ```

   Connect to `ws://localhost:5080/ws/live-readings`, parse JSON, keep latest readings.

2. **Create `LiveReadingsPanel` component**

   - Show status (Live / Connecting / Disconnected)
   - Table listing:
     - Time
     - Device
     - Sensor type
     - Value + unit

3. **Create `LiveDashboardPage` and route `/live`**

   - Explain briefly: ESP32 → MQTT → backend → WebSocket → UI
   - Include `<LiveReadingsPanel />`.

4. **Test end-to-end**

   - Start backend
   - Start frontend
   - Start ESP32
   - Confirm new readings appear automatically.

---

## 🧩 Part 6 — Test the System Using `moisture_sim.py` (Python MQTT Simulator)

**Goal:** Verify that the entire path works using a **simulated ESP32 device**, without real hardware:

> `moisture_sim.py` → MQTT broker in Api → `Esp32MessageHandler` → DB → WebSocket → React Live Dashboard

### 6.1. Understand the simulator

You’re given a Python script `moisture_sim.py` that:

- Connects to an MQTT broker using `paho.mqtt.client`
- Publishes to topic:
  ```text
  greenhouse/{DEVICE_NAME}/sensor/{SENSOR_TYPE}
  ```
- Uses payload:
  ```json
  {
    "value": <moisture>,
    "unit": "%",
    "raw": <raw_adc_value>
  }
  ```
- Periodically simulates soil moisture based on a virtual ADC value.

Your backend `Esp32MessageHandler` should **already expect**:

- Topic pattern: `greenhouse/{deviceName}/sensor/{sensorType}`
- JSON with at least `value` (double) and `unit` (string)

So the simulator is a perfect stand-in for a real ESP32. (The simulator is generated using real esp32 code as the example -> `esp32-demo.ino`)

---

### 6.2. Configure the simulator to talk to your backend

In `moisture_sim.py`:

```python
MQTT_SERVER = "10.21.0.147"  # <-- change this to YOUR backend machine's IP
MQTT_PORT = 1883
DEVICE_NAME = "GreenhousePi"
SENSOR_TYPE = "SoilMoisture"
```

You must:

1. Replace `"10.21.0.147"` with the **IP address of the machine running the .NET backend**.

   - On Windows: `ipconfig`
   - On macOS/Linux: `ifconfig` or `ip a`

2. Ensure port `1883` matches the port your broker listens on (from `MqttServerOptionsBuilder`).

💡 The simulator and backend must be on the same network (e.g. Xamk lab network).

---

### 6.3. Installation requirements (Python side)

On the machine where you’ll run the simulator:

1. Install Python 3.
2. Install `paho-mqtt`:

   ```bash
   pip install paho-mqtt
   ```

3. Save the Python script as `moisture_sim.py`.

---

### 6.4. Run all components together

Students must run **three things at the same time**:

1. **Backend (Api)**

   From `SmartGreenhouse.Api` directory:

   ```bash
   dotnet run
   ```

   Check that:

   - MQTT broker starts (log lines from `MqttBrokerHostedService`).
   - WebSocket endpoint `/ws/live-readings` is available.

2. **Frontend (React + Vite)**

   From the frontend folder:

   ```bash
   npm install       # if not already done
   npm run dev
   ```

   Open the live dashboard page (e.g. `http://localhost:5173/live`).

3. **Python moisture simulator**

   From the folder with `moisture_sim.py`:

   ```bash
   python moisture_sim.py
   ```

   You should see output like:

   ```text
   Connecting to MQTT broker <your-ip>:1883 ...
   MQTT: Connected successfully
   Using topic: greenhouse/GreenhousePi/sensor/SoilMoisture
   Raw: 3120  Moisture %: 23.45
   Publishing to greenhouse/GreenhousePi/sensor/SoilMoisture: {"value":23.45,"unit":"%","raw":3120}
   MQTT publish ok
   ```

---

### 6.5. What should happen end-to-end

When all three processes are running:

1. **Simulator** publishes a message:

   - Topic: `greenhouse/GreenhousePi/sensor/SoilMoisture`
   - Payload: e.g. `{"value": 23.45, "unit": "%", "raw": 3120}`

2. **Backend MQTT broker** receives it and passes it to `Esp32MessageHandler`.
3. **Esp32MessageHandler**:

   - Parses topic (`deviceName = "GreenhousePi"`, `sensorType = "SoilMoisture"`).
   - Deserializes the JSON and reads `value` and `unit`.
   - Looks up or creates `Device` with `DeviceName = "GreenhousePi"`.
   - Creates and saves a `SensorReading`.
   - Maps it to `ReadingDto`.
   - Calls `IRealTimeNotifier.BroadcastReadingAsync(dto)`.

4. **WebSocket layer**:

   - `WebSocketRealTimeNotifier` receives the `ReadingDto`.
   - `LiveReadingHub` broadcasts it as **camelCase JSON** to all connected browser clients.

5. **Frontend**:

   - Your WebSocket hook receives the `ReadingDto` JSON.
   - It updates `readings` state.
   - `LiveReadingsPanel` shows a new row with:
     - Time
     - Device: `GreenhousePi`
     - Sensor: `SoilMoisture`
     - Value: e.g. `23.45 %`
