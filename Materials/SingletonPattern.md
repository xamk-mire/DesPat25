# Singleton — one oven controller, many cooks

In a busy pizzeria, there’s exactly **one deck oven**. If different teams created their _own_ “controllers,” lunch might crank the deck to 500 °C while night shift quietly dials a different object to 320 °C. The _hardware_ stays confused—and so do you. What you want is a single authority: **one controller** everyone talks to.

Below: first the bug (multiple controllers), then two fixes—**classic Singleton** and **DI singleton**—using the same `OvenController` idea so you can compare apples to apples.

---

## 0) The bug: multiple controllers (don’t do this)

```csharp
using System;

public sealed class OvenController
{
    public int TemperatureCelsius { get; private set; } = 485;
    public void SetTemperature(int c) => TemperatureCelsius = c;
}

// Two teams “new up” their own controllers (oops).
public sealed class LunchRush
{
    private readonly OvenController _oven = new();            // instance A
    public void Prep() { _oven.SetTemperature(500); }
    public int Temp() => _oven.TemperatureCelsius;
}

public sealed class NightShift
{
    private readonly OvenController _oven = new();            // instance B (different!)
    public void Prep() { _oven.SetTemperature(320); }
    public int Temp() => _oven.TemperatureCelsius;
}

public static class DemoBug
{
    public static void Main()
    {
        var lunch = new LunchRush();
        var night = new NightShift();

        lunch.Prep();
        night.Prep();

        Console.WriteLine($"Lunch sees: {lunch.Temp()}°C"); // 500
        Console.WriteLine($"Night sees: {night.Temp()}°C"); // 320
        // Two different temps => two different objects => global oven state is ambiguous.
    }
}
```

---

## 1) Classic Singleton: one global access point

- **When:** no DI container; want minimal ceremony.
- **Trade-off:** easy access, but it’s a _global_; tests must work around statics.

```csharp
using System;

public sealed class OvenController
{
    private OvenController() { }                              // nobody else can 'new' it
    public static OvenController Instance { get; } = new();   // eager, thread-safe init

    public int TemperatureCelsius { get; private set; } = 485;
    public void SetTemperature(int c)
    {
        if (c is < 200 or > 550) throw new ArgumentOutOfRangeException(nameof(c));
        TemperatureCelsius = c;
    }
}

// Teams now share the same instance
public sealed class LunchRush
{
    public void Prep() => OvenController.Instance.SetTemperature(500);
    public int Temp() => OvenController.Instance.TemperatureCelsius;
}

public sealed class NightShift
{
    public void Prep() => OvenController.Instance.SetTemperature(320);
    public int Temp() => OvenController.Instance.TemperatureCelsius;
}

public static class DemoSingleton
{
    public static void Main()
    {
        var lunch = new LunchRush();
        var night = new NightShift();

        lunch.Prep();
        Console.WriteLine($"After lunch: {lunch.Temp()}°C");  // 500

        night.Prep();
        Console.WriteLine($"After night:  {lunch.Temp()}°C");  // 320 (same shared controller)
    }
}
```

> Variations: use `Lazy<OvenController>` if you want deferred, thread-safe initialization with I/O in the ctor.

---

## 2) DI singleton: same effect, cleaner seams

- **When:** you have (or can use) a DI container.
- **Benefits:** explicit dependencies, **easy testing**, and lifecycle managed by the container.

```csharp
using System;
using Microsoft.Extensions.DependencyInjection;

public interface IOvenController
{
    int TemperatureCelsius { get; }
    void SetTemperature(int c);
}

public sealed class OvenController : IOvenController
{
    public int TemperatureCelsius { get; private set; } = 485;
    public void SetTemperature(int c)
    {
        if (c is < 200 or > 550) throw new ArgumentOutOfRangeException(nameof(c));
        TemperatureCelsius = c;
    }
}

public sealed class LunchRush
{
    private readonly IOvenController _oven;
    public LunchRush(IOvenController oven) { _oven = oven; }
    public void Prep() => _oven.SetTemperature(500);
    public int Temp() => _oven.TemperatureCelsius;
}

public sealed class NightShift
{
    private readonly IOvenController _oven;
    public NightShift(IOvenController oven) { _oven = oven; }
    public void Prep() => _oven.SetTemperature(320);
    public int Temp() => _oven.TemperatureCelsius;
}

public static class DemoDiSingleton
{
    public static void Main()
    {
        // 1) Register a SINGLE shared controller for the whole app
        var services = new ServiceCollection()
            .AddSingleton<IOvenController, OvenController>()
            .AddTransient<LunchRush>()
            .AddTransient<NightShift>()
            .BuildServiceProvider();

        // 2) Resolve both teams; they receive the same IOvenController instance
        var lunch = services.GetRequiredService<LunchRush>();
        var night = services.GetRequiredService<NightShift>();

        lunch.Prep();
        Console.WriteLine($"After lunch: {lunch.Temp()}°C");  // 500

        night.Prep();
        Console.WriteLine($"After night:  {lunch.Temp()}°C");  // 320 (shared)
    }
}
```

### Testing the DI version (drop-in fake)

```csharp
using Microsoft.Extensions.DependencyInjection;

public sealed class FakeOven : IOvenController
{
    public int TemperatureCelsius { get; private set; } = 0;
    public void SetTemperature(int c) => TemperatureCelsius = c; // trivial, deterministic
}

public static class DemoTest
{
    public static void Main()
    {
        var services = new ServiceCollection()
            .AddSingleton<IOvenController, FakeOven>()   // swap the real controller
            .AddTransient<LunchRush>()
            .BuildServiceProvider();

        var lunch = services.GetRequiredService<LunchRush>();
        lunch.Prep();
        Console.WriteLine(lunch.Temp()); // predictable test value (500 in FakeOven)
    }
}
```

---

## Which one should you choose?

- **Classic Singleton**

  - ✅ Smallest footprint; no DI required
  - ⚠️ Global access hides dependencies; harder to fake in tests; risk of state leakage

- **DI Singleton**

  - ✅ Explicit dependencies, great tests, container handles lifetime
  - ⚠️ Requires DI setup (which most modern apps already have)

**Rule of thumb:** if you already use DI (ASP.NET Core, workers, desktop apps with `ServiceCollection`), prefer the **DI singleton**. If you’re in a minimal console/lib with no DI, a tiny **sealed + private ctor + static instance** is fine—keep state minimal and behavior predictable.
