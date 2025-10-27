
## The Adapter Pattern — When Devices Speak Different Languages

In modern systems, communication is rarely uniform.  
A sensor streams binary over UART, a newer module uses MQTT, and your application—running in the cloud or on a PC—just wants to **read telemetry** and **send commands** without caring how those messages travel.

But reality isn’t that kind. Each protocol has its own dialect: message framing, connection logic, authentication steps, and data formats. The engineer’s challenge is universal—**how do we make systems designed in isolation work together without rewriting them all?**

That’s what the **Adapter Pattern** is for.

The Adapter acts as a **translator** between incompatible interfaces. It allows classes that were never meant to cooperate to interact seamlessly, without either side knowing the details of the other. It’s the same reason you can plug a European device into a US wall socket: you don’t rebuild the appliance—you use an adapter.

Let’s see how that applies to something real: communicating with microcontrollers like the **ESP32**.

---

## The Problem: Hard-Coded Protocol Logic (Naïve Design)

Suppose you’re building an application to communicate with ESP32 devices.  
Some devices use **Serial (UART)**, others publish telemetry via **MQTT**. You just want your app to say:

> “Turn LED on” and “Read temperature”

But in your first attempt, you bake all communication logic directly into your controller class:

```csharp
// ❌ Naïve, tightly coupled controller
public class DeviceController
{
    private readonly bool _useSerial;
    private readonly SerialEsp32Driver _serial;
    private readonly MqttClient _mqtt;

    public DeviceController(bool useSerial)
    {
        _useSerial = useSerial;
        _serial = new SerialEsp32Driver("COM7", 115200);
        _mqtt = new MqttClient("mqtt://broker.local", "desktop-client");
    }

    public void Initialize()
    {
        if (_useSerial)
            _serial.Open();
        else
            _mqtt.Connect();
    }

    public void SetLed(string color, bool on)
    {
        if (_useSerial)
        {
            var payload = $"CMD:SET_LED;color={color};power={(on ? "on" : "off")}|AA";
            _serial.Write(System.Text.Encoding.ASCII.GetBytes(payload));
        }
        else
        {
            var json = $"{{\"name\":\"SET_LED\",\"args\":{{\"color\":\"{color}\",\"power\":\"{(on ? "on" : "off")}\"}}}}";
            _mqtt.Publish("esp32/cmd", json);
        }
    }

    public void ReadTelemetry()
    {
        if (_useSerial)
        {
            var line = _serial.ReadLine();
            Console.WriteLine($"[App] Parsed serial telemetry: {line}");
        }
        else
        {
            var msg = _mqtt.SubscribeAndWait("esp32/telemetry");
            Console.WriteLine($"[App] Parsed MQTT telemetry: {msg}");
        }
    }
}
```

This works, but it’s brittle:

- Every method has **two versions of the same logic**.
    
- Adding a new protocol (like HTTP or Bluetooth) means **editing this class everywhere**.
    
- Testing is painful: you can’t mock communication cleanly.
    

Your `DeviceController` has become a bilingual diplomat who insists on translating _manually_ every time someone speaks—clumsy, repetitive, and impossible to scale.

---

## The Adapter Pattern: Teaching Systems to Speak Through Translators

Instead of embedding protocol logic in the controller, we can define a **single abstraction**—a common interface for communicating with any ESP32, regardless of protocol.  
Each concrete protocol (Serial, MQTT, etc.) gets its own **adapter**, which translates that universal interface into whatever message format and behavior the device expects.

Now our controller talks through a **common language**, while each adapter handles translation behind the scenes.

---

## The Clean, Adapter-Based Solution (ESP32 Example)

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

// 1. Common interface (the Target)
public interface IMcuChannel
{
    string Name { get; }
    void Connect();
    void SendCommand(Command cmd);
    Telemetry ReadTelemetry();
}

// 2. Domain models
public record Command(string Name, Dictionary<string, string> Args);
public record Telemetry(double TemperatureC, double HumidityPct, DateTime TimestampUtc);

// 3. Simulated low-level “drivers” (unmodifiable)
public class SerialEsp32Driver
{
    public void Open() => Console.WriteLine("[Serial] Open COM7 @115200");
    public void Write(byte[] data) => Console.WriteLine("[Serial] TX " + Encoding.ASCII.GetString(data));
    public string ReadLine() { Thread.Sleep(100); return "TEL;temp=22.5;hum=43.0"; }
}

public class MqttClient
{
    public void Connect() => Console.WriteLine("[MQTT] Connected to broker");
    public void Publish(string topic, string payload) => Console.WriteLine($"[MQTT] PUB {topic} :: {payload}");
    public string SubscribeAndWait(string topic) { Thread.Sleep(100); return "{\"tempC\":23.8,\"humidity\":44.2}"; }
}

// 4. Adapters (each translates IMcuChannel → protocol)
public class SerialEsp32Adapter : IMcuChannel
{
    private readonly SerialEsp32Driver _driver;
    public string Name => "Serial";
    public SerialEsp32Adapter(SerialEsp32Driver driver) => _driver = driver;

    public void Connect() => _driver.Open();
    public void SendCommand(Command cmd)
    {
        var sb = new StringBuilder($"CMD:{cmd.Name}");
        foreach (var kv in cmd.Args) sb.Append($";{kv.Key}={kv.Value}");
        var frame = sb + "|AA";
        _driver.Write(Encoding.ASCII.GetBytes(frame));
    }
    public Telemetry ReadTelemetry()
    {
        var line = _driver.ReadLine();
        var parts = line.Split(';');
        double t = double.Parse(parts[1].Split('=')[1], CultureInfo.InvariantCulture);
        double h = double.Parse(parts[2].Split('=')[1], CultureInfo.InvariantCulture);
        return new Telemetry(t, h, DateTime.UtcNow);
    }
}

public class MqttEsp32Adapter : IMcuChannel
{
    private readonly MqttClient _client;
    public string Name => "MQTT";
    public MqttEsp32Adapter(MqttClient client) => _client = client;

    public void Connect() => _client.Connect();
    public void SendCommand(Command cmd)
    {
        var json = $"{{\"name\":\"{cmd.Name}\",\"args\":{{\"color\":\"{cmd.Args["color"]}\",\"power\":\"{cmd.Args["power"]}\"}}}}";
        _client.Publish("esp32/cmd", json);
    }
    public Telemetry ReadTelemetry()
    {
        var msg = _client.SubscribeAndWait("esp32/telemetry");
        double t = 23.8, h = 44.2; // pretend we parsed it
        return new Telemetry(t, h, DateTime.UtcNow);
    }
}

// 5. High-level controller — talks only to the adapter interface
public class DeviceController
{
    private readonly IMcuChannel _channel;
    public DeviceController(IMcuChannel channel) => _channel = channel;

    public void Initialize()
    {
        Console.WriteLine($"[App] Connecting via {_channel.Name}");
        _channel.Connect();
    }

    public void SetLed(string color, bool on)
    {
        var cmd = new Command("SET_LED", new() { ["color"] = color, ["power"] = on ? "on" : "off" });
        _channel.SendCommand(cmd);
    }

    public void PollTelemetry()
    {
        var t = _channel.ReadTelemetry();
        Console.WriteLine($"[App] Telemetry <- {t.TemperatureC:F1}°C, {t.HumidityPct:F1}%");
    }
}

// 6. Demo
public static class Program
{
    public static void Main()
    {
        Console.WriteLine("=== Serial ===");
        var serialCtrl = new DeviceController(new SerialEsp32Adapter(new SerialEsp32Driver()));
        serialCtrl.Initialize();
        serialCtrl.SetLed("red", true);
        serialCtrl.PollTelemetry();

        Console.WriteLine();
        Console.WriteLine("=== MQTT ===");
        var mqttCtrl = new DeviceController(new MqttEsp32Adapter(new MqttClient()));
        mqttCtrl.Initialize();
        mqttCtrl.SetLed("blue", false);
        mqttCtrl.PollTelemetry();
    }
}
```

**Output (conceptually):**

```
=== Serial ===
[App] Connecting via Serial
[Serial] Open COM7 @115200
[Serial] TX CMD:SET_LED;color=red;power=on|AA
[Serial] RX TEL;temp=22.5;hum=43.0
[App] Telemetry <- 22.5°C, 43.0%

=== MQTT ===
[App] Connecting via MQTT
[MQTT] Connected to broker
[MQTT] PUB esp32/cmd :: {"name":"SET_LED","args":{"color":"blue","power":"off"}}
[MQTT] RX esp32/telemetry :: {"tempC":23.8,"humidity":44.2}
[App] Telemetry <- 23.8°C, 44.2%
```

---

## The Difference in One Line

- The **bad code** forced your app to _speak every protocol directly_.
    
- The **Adapter pattern** lets your app _speak one universal language_, leaving translation to specialized adapters.
    

Now your `DeviceController` doesn’t care whether it’s controlling an ESP32 via Serial, MQTT, HTTP, or Bluetooth—  
it just _talks to a device._

---

## Why This Pattern Matters in Embedded Integration

Adapters are a quiet act of design empathy.  
They let old hardware and new systems coexist.  
They let engineers modernize gradually instead of tearing down everything at once.  
And they turn a brittle tangle of `if` statements into a modular, extensible bridge between worlds.

> In essence, the Adapter Pattern lets your software say,  
> “I understand you — even if you speak differently.”
