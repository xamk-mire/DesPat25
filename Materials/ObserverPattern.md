## The Observer Pattern - subscribe to get the latest news

At dawn, a harbor master lights a beacon. He doesn’t hail each vessel by name; he simply _casts a signal_. Any ship that cares adjusts course. That’s the Observer pattern: one source changes; many listeners may care; the source stays blissfully unaware of who they are. Ships arrive, depart, or sink without the lighthouse rewriting its duties.

In code, the lighthouse is the **Subject**; ships are **Observers**. Without Observer, the lighthouse keeps a phone book and dials every ship on every sunrise. With Observer, the lighthouse shines; ships subscribe to the light, and unsubscribe when they sail away.

- The subject **broadcasts state changes** without knowing who listens.
    
- Observers can **attach/detach at runtime**—extensibility without edits to the subject.
    
- You choose **error handling and isolation** policies so one bad ship doesn’t sink the fleet.
    
---

# The “bad” solution (tight coupling, hardcoding dependencies)

**Scenario:** A `WeatherStation` produces temperature updates. A `Display`, a `Logger`, and an `AlertService` all need to react.

In the bad design, `WeatherStation` *directly* calls each dependent class. Adding/removing a listener means editing `WeatherStation`; testability suffers, and failure in one listener can break the whole flow.

```csharp
using System;

namespace BadObserver
{
    public class Display { public void Update(int c) => Console.WriteLine($"[Display] {c}°C"); }
    public class Logger  { public void Update(int c) => Console.WriteLine($"[Logger]  {c}°C"); }
    public class Alerts  { public void Update(int c) { if (c >= 30) Console.WriteLine("[Alert] Heat!"); } }

    public class WeatherStation
    {
        private int _temp;
        private readonly Display _display;
        private readonly Logger _logger;
        private readonly Alerts _alerts;

        public WeatherStation(Display d, Logger l, Alerts a)
        {
            _display = d;
            _logger = l;
            _alerts = a;
        }

        public void SetTemperature(int c)
        {
            _temp = c;
            _display.Update(_temp);
            _logger.Update(_temp);
            _alerts.Update(_temp);
        }
    }

    public static class Program
    {
        public static void Main()
        {
            var station = new WeatherStation(new Display(), new Logger(), new Alerts());
            station.SetTemperature(27);
            station.SetTemperature(31);
        }
    }
}

```

### What’s wrong here?

* `WeatherStation` must **know** every consumer and their method signatures.
* Adding/removing consumers requires **editing** the subject (violates Open/Closed Principle).
* An exception in one consumer can **stop** updates to others.
* Reuse and testing are **hard**: you can’t spin up `WeatherStation` without providing *all* dependencies.

---


# Step-by-step refactor to a manual Observer

## Step 1 — Introduce a tiny observer contract

We define what it means to “care about temperature” without naming concrete classes.

```csharp
public interface ITemperatureObserver
{
    void OnTemperatureChanged(int celsius);
}
```

## Step 2 — Make observers implement the contract

Rename `Update` → `OnTemperatureChanged` to remove subject-specific knowledge from the method signature.

```csharp
public class Display : ITemperatureObserver
{
    public void OnTemperatureChanged(int c) => Console.WriteLine($"[Display] {c}°C");
}

public class Logger : ITemperatureObserver
{
    public void OnTemperatureChanged(int c) => Console.WriteLine($"[Logger] Logged {c}°C");
}

public class Alerts : ITemperatureObserver
{
    private readonly int _threshold;
    public Alerts(int threshold = 30) => _threshold = threshold;
    public void OnTemperatureChanged(int c) { if (c >= _threshold) Console.WriteLine("[Alert] Heat!"); }
}
```

## Step 3 — Remove hardwired dependencies from the subject

Kill the `Display/Logger/Alerts` fields and constructor parameters; add a private list of observers instead.

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

public class WeatherStation
{
    private int _temp;
    private readonly List<ITemperatureObserver> _observers = new();
```

## Step 4 — Add a subscription API with safe unsubscription

Return `IDisposable` so callers can cleanly detach (avoids leaks when the subject lives longer).

```csharp
    public IDisposable Subscribe(ITemperatureObserver observer)
    {
        if (observer == null) throw new ArgumentNullException(nameof(observer));
        if (!_observers.Contains(observer)) _observers.Add(observer);
        return new Unsubscriber(_observers, observer);
    }

    private sealed class Unsubscriber : IDisposable
    {
        private readonly List<ITemperatureObserver> _list;
        private ITemperatureObserver? _observer;
        public Unsubscriber(List<ITemperatureObserver> list, ITemperatureObserver observer)
        { _list = list; _observer = observer; }

        public void Dispose()
        {
            if (_observer != null) { _list.Remove(_observer); _observer = null; }
        }
    }
```

## Step 5 — Replace direct calls with a notification loop

Update `SetTemperature` to notify _whoever_ is subscribed. Snapshot the list so observers can unsubscribe during callbacks. Decide an error policy (log & continue shown here).

```csharp
    public void SetTemperature(int c)
    {
        if (c == _temp) return; // optional: suppress duplicates
        _temp = c;

        foreach (var o in _observers.ToArray())
        {
            try { o.OnTemperatureChanged(_temp); }
            catch (Exception ex) { Console.Error.WriteLine($"[WeatherStation] Observer failed: {ex.Message}"); }
        }
    }
}
```

## Step 6 — Compose at the edges (not inside the subject)

Construction no longer wires observers into `WeatherStation`; composition code does.

```csharp
public static class Program
{
    public static void Main()
    {
        var station = new WeatherStation();
        var display = new Display();
        var logger  = new Logger();
        var alerts  = new Alerts(threshold: 30);

        using var sub1 = station.Subscribe(display);
        using var sub2 = station.Subscribe(logger);
        using var sub3 = station.Subscribe(alerts);

        station.SetTemperature(27); // all three notified
        station.SetTemperature(31); // alerts triggers

        // Detach one observer without touching WeatherStation
        sub2.Dispose();
        station.SetTemperature(29); // logger no longer notified
    }
}
```

---

# The full “fixed” version (manual Observer, consolidated)

```csharp
using System;
using System.Collections.Generic;

namespace ManualObserverRefactor
{
    public interface ITemperatureObserver
    {
        void OnTemperatureChanged(int celsius);
    }

    public class WeatherStation
    {
        private int _temp;
        private readonly List<ITemperatureObserver> _observers = new();

        public IDisposable Subscribe(ITemperatureObserver observer)
        {
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            if (!_observers.Contains(observer)) _observers.Add(observer);
            return new Unsubscriber(_observers, observer);
        }

        public void SetTemperature(int c)
        {
            if (c == _temp) return;
            _temp = c;

            var snapshot = _observers.ToArray();
            foreach (var o in snapshot)
            {
                try { o.OnTemperatureChanged(_temp); }
                catch (Exception ex) { Console.Error.WriteLine($"[WeatherStation] Observer failed: {ex.Message}"); }
            }
        }

        private sealed class Unsubscriber : IDisposable
        {
            private readonly List<ITemperatureObserver> _list;
            private ITemperatureObserver? _observer;
            public Unsubscriber(List<ITemperatureObserver> list, ITemperatureObserver observer)
            { _list = list; _observer = observer; }

            public void Dispose()
            {
                if (_observer != null) { _list.Remove(_observer); _observer = null; }
            }
        }
    }

    public class Display : ITemperatureObserver
    {
        public void OnTemperatureChanged(int c) => Console.WriteLine($"[Display] {c}°C");
    }

    public class Logger : ITemperatureObserver
    {
        public void OnTemperatureChanged(int c) => Console.WriteLine($"[Logger] Logged {c}°C");
    }

    public class Alerts : ITemperatureObserver
    {
        private readonly int _threshold;
        public Alerts(int threshold = 30) => _threshold = threshold;
        public void OnTemperatureChanged(int c) { if (c >= _threshold) Console.WriteLine("[Alert] Heat!"); }
    }

    public static class Program
    {
        public static void Main()
        {
            var station = new WeatherStation();
            using var d = station.Subscribe(new Display());
            using var l = station.Subscribe(new Logger());
            using var a = station.Subscribe(new Alerts(30));

            station.SetTemperature(27);
            station.SetTemperature(31);

            l.Dispose();            // detach logger
            station.SetTemperature(29);
        }
    }
}
```

---

## Key points (quick bullets)

- **Define a tiny observer interface** per signal; avoid subject-specific method names.
    
- **Strip dependencies** out of the subject; store a list of observers instead.
    
- **Expose Subscribe() → IDisposable** so callers can detach and prevent leaks.
    
- **Notify via a snapshot** and choose an **error policy** (log & continue is common).

- **Compose at the edges**, not inside the subject—tests and features stay simple.
---

## Lecture code example

```csharp

namespace ObserverExample
{
    // Observer
    public interface IObserver
    {
        void Update(float temperature, float humidity);
    }

    // Subject
    public interface ISubject
    {
        void AddObserver(IObserver observer);
        void RemoveObserver(IObserver observer);
        void NotifyObservers();
    }

    // Concrete Subject
    public class WeatherStation : ISubject
    {
        private readonly List<IObserver> _observers = new();
        private float _temperature;
        private float _humidity;

        public void SetMeasurements(float temperature, float humidity)
        {
            _temperature = temperature;
            _humidity = humidity;
            NotifyObservers();
        }

        public void AddObserver(IObserver observer) => _observers.Add(observer);

        public void RemoveObserver(IObserver observer) => _observers.Remove(observer);

        public void NotifyObservers()
        {
            foreach (var obs in _observers)
                obs.Update(_temperature, _humidity);
        }
    }

    // Concrete Observer
    public class PhoneAppDisplay : IObserver
    {
        private readonly string _name;
        public PhoneAppDisplay(string name) => _name = name;

        public void Update(float temperature, float humidity)
        {
            Console.WriteLine($"{_name} -> Temp: {temperature}°C, Humidity: {humidity}%");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var station = new WeatherStation();
            var phone = new PhoneAppDisplay("Phone");
            var watch = new PhoneAppDisplay("Watch");

            station.AddObserver(phone);
            station.AddObserver(watch);

            station.SetMeasurements(22.5f, 55f);
            station.SetMeasurements(25.0f, 50f);

            // Remove watch
            station.RemoveObserver(watch);

            // Only phone should be updated
            station.SetMeasurements(19.2f, 60f);
        }
    }
}
```
    

    

