## Abstract Factory — make one choice, and the whole room agrees

Imagine a interior decorator with a leather-bound book of “complete looks.” Tap **Modern**, and the room arrives as a package: steel stools, glass tables, cool LED lamps. Tap **Rustic**, and oiled oak, linen, and warm light flow in as if they’ve always belonged together. You never haggle over chair legs or lamp sockets; your one decision—the theme—quietly governs all the little ones. That’s the essence of **Abstract Factory**: **pick a family**, not a piece.

Without it, teams end up decorating by committee at every call site: three `if (theme)` branches for chair, table, lamp; three chances to drift off-theme. Requirements change, and the “Modern chair + Rustic lamp” bug slips through review. Abstract Factory pulls those scattered choices into one place. The client makes a single, high-level decision—_which factory?_—and receives a coherent set of objects guaranteed to work together.

The pattern is less about clever code and more about **coordination**. It creates a **protocol** for producing related products, and asks you to encode each style (or platform, brand, tenant, region) as a self-contained factory. Consistency becomes a property of the design, not a hope enforced by code review.

---

## The pain without Abstract Factory (branches everywhere)

Each place you furnish repeats the theme logic—three times.

```csharp
using System;

public interface IChair { string Sit(); }
public interface ITable { string Surface(); }
public interface ILamp  { int Brightness(); }

public static class Restaurant_1
{
    public static (IChair, ITable, ILamp) FurnishSection(string theme)
    {
        // 1) Chair
        IChair chair = theme == "Rustic"
            ? new RusticChair()
            : new ModernStool();

        // 2) Table
        ITable table = theme == "Rustic"
            ? new OakTable()
            : new GlassTable();

        // 3) Lamp
        ILamp lamp = theme == "Rustic"
            ? new WarmLamp(450)
            : new LedLamp(800);

        // Three branches per section = bugs when one is changed and others aren’t.
        return (chair, table, lamp);
    }

    private sealed class RusticChair : IChair { public string Sit() => "Comfy oak chair."; }
    private sealed class ModernStool : IChair { public string Sit() => "Tall, minimal stool."; }
    private sealed class OakTable    : ITable { public string Surface() => "oiled oak"; }
    private sealed class GlassTable  : ITable { public string Surface() => "tempered glass"; }
    private sealed class WarmLamp : ILamp { public WarmLamp(int l){_l=l;} private readonly int _l; public int Brightness()=>_l; }
    private sealed class LedLamp  : ILamp { public LedLamp(int l){_l=l;} private readonly int _l; public int Brightness()=>_l; }
}

// public static class Restaurant_2 {}
// public static class Restaurant_3 {}, etc...

public static class Demo0
{
    public static void Main()
    {
        var (chair, table, lamp) = Restaurant_1.FurnishSection("Modern");
        Console.WriteLine(chair.Sit());
        Console.WriteLine(table.Surface());
        Console.WriteLine(lamp.Brightness());
    }
}

```

## Abstract Factory — pick a theme once, get a matching family

Define a **factory interface** that creates the related products. Implement one factory per theme. Client code gets a **single decision point**: which factory to pass.

```csharp
public interface IChair { string Sit(); }
public interface ITable { string Surface(); }
public interface ILamp  { int Brightness(); }

// Abstract Factory: a contract for a *family* of furniture.
public interface IThemedFurnitureFactory
{
    IChair CreateChair();
    ITable CreateTable();
    ILamp  CreateLamp();
}

// Modern: steel + glass + bright LEDs
public sealed class ModernThemeFactory : IThemedFurnitureFactory
{
    public IChair CreateChair() => new SteelBarStool();
    public ITable CreateTable() => new GlassTable();
    public ILamp  CreateLamp()  => new LedLamp(800);

    private sealed class SteelBarStool : IChair { public string Sit() => "Tall, minimal stool."; }
    private sealed class GlassTable   : ITable { public string Surface() => "tempered glass"; }
    private sealed class LedLamp      : ILamp  { private readonly int l; public LedLamp(int l){ this.l = l; } public int Brightness() => l; }
}

// Rustic: oak + linen + warm glow
public sealed class RusticThemeFactory : IThemedFurnitureFactory
{
    public IChair CreateChair() => new OakDiningChair();
    public ITable CreateTable() => new OakTable();
    public ILamp  CreateLamp()  => new WarmLamp(450);

    private sealed class OakDiningChair : IChair { public string Sit() => "Comfy oak chair."; }
    private sealed class OakTable      : ITable { public string Surface() => "oiled oak"; }
    private sealed class WarmLamp      : ILamp  { private readonly int l; public WarmLamp(int l){ this.l = l; } public int Brightness() => l; }
}

// Client: one decision, three matching objects.
public static class Restaurant
{
    public static (IChair Chair, ITable Table, ILamp Lamp) Furnish(IThemedFurnitureFactory factory) =>
        (factory.CreateChair(), factory.CreateTable(), factory.CreateLamp());
}
```

Using it feels like flipping a single switch:

```csharp
var modern = Restaurant.Furnish(new ModernThemeFactory());
var rustic = Restaurant.Furnish(new RusticThemeFactory());

Console.WriteLine(modern.Table.Surface()); // "tempered glass"
Console.WriteLine(rustic.Lamp.Brightness()); // 450
```

Adding a theme is a chapter, not a scavenger hunt:

```csharp
public sealed class CoastalThemeFactory : IThemedFurnitureFactory
{
    public IChair CreateChair() => new RattanLounge();
    public ITable CreateTable() => new WhitewashedTable();
    public ILamp  CreateLamp()  => new SoftBlueLamp(500);

    private sealed class RattanLounge : IChair { public string Sit() => "Airy rattan lounge chair."; }
    private sealed class WhitewashedTable : ITable { public string Surface() => "whitewashed wood"; }
    private sealed class SoftBlueLamp : ILamp { private readonly int l; public SoftBlueLamp(int l){ this.l = l; } public int Brightness() => l; }
}
```

The client code remains untouched; only the **new** factory is introduced. That’s Open/Closed, expressed in furniture.

---

## Selection without a god-switch

You still need a doorway from a runtime “theme” to a concrete factory. Keep that choice centralized and explicit:

```csharp
public static class ThemeRegistry
{
    private static readonly Dictionary<string, Func<IThemedFurnitureFactory>> factories =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Modern"] = () => new ModernThemeFactory(),
            ["Rustic"] = () => new RusticThemeFactory(),
            ["Coastal"] = () => new CoastalThemeFactory()
        };

    public static IThemedFurnitureFactory For(string theme) =>
        factories.TryGetValue(theme, out var make)
            ? factory()
            : throw new NotSupportedException($"Unknown theme: {theme}");
}
```

The rest of the system stays blissfully unaware of concrete classes.

---

## Where the pattern really shines

Think beyond décor. Abstract Factory fits any domain where **families must move together**:

- UI toolkits: platform-native widgets by OS/skin.
- Cross-tenant branding: logos, palettes, copy that must match.
- Driver stacks: sensor + protocol + serializer bundles per device generation.
- Game worlds: biome-specific assets (terrain, flora, lighting) created as a set.

It’s the pattern you reach for when “consistency” is not a guideline but a **contract**.

---

## Pitfalls, gently

The danger isn’t overuse; it’s **leakage**. If clients grab a “Modern” factory but override the lamp ad hoc, you’re back to drift. Keep the family boundary firm, and resist a global **Service Locator**—inject the factory you want, don’t pull one from the shadows. And be pragmatic: if you only ever create a **single** product, a simple factory is lighter (factory pattern).

---

## Testing feels like rehearsal, not opening night

Because the whole family comes from one place, tests can stage predictable sets:

```csharp
public sealed class FakeThemeFactory : IThemedFurnitureFactory
{
    private sealed class FakeChair : IChair { public string Sit() => "Test seat"; }
    private sealed class FakeTable : ITable { public string Surface() => "test surface"; }
    private sealed class FakeLamp  : ILamp  { public int Brightness() => 123; }

    public IChair CreateChair() => new FakeChair();
    public ITable CreateTable() => new FakeTable();
    public ILamp  CreateLamp()  => new FakeLamp();
}
```

Swap the real factory for `FakeThemeFactory`, and you’ve isolated behavior without touching client code.

---

### Key points (quick)

- One decision (**which factory?**) yields a **coherent family** of objects.
- Clients depend on the **abstract factory**, not concrete classes—easy to swap.
- New families mean **adding a factory**, not editing call sites (Open/Closed).
- Keep selection centralized (a **registry**), avoid hidden globals, and don’t let clients mix pieces across families.
