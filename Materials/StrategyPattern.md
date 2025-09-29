## Strategy Pattern - Choosing the correct stragey to reach the goal

Imagine a chef with a single recipe card taped to the counter. Every diner—vegan, gluten-free, spice-lover—gets the same dish, because the kitchen only knows one way to cook. Chaos ensues: special cases get penciled in the margins, arrows point to substitutions, the page is stained with contradictory notes.

Now imagine the chef with a menu of techniques instead: pan-sear, sous-vide, roast. For each diner, the chef chooses a technique fit for purpose. The recipe (context) is stable; the technique (strategy) varies, and new techniques can be added without rewriting the entire cookbook.

That’s the Strategy pattern: encapsulate a family of algorithms, make them interchangeable, and let the context delegate work to the chosen strategy.

 - Swap algorithms at runtime without changing the caller.

 - Open for extension, closed for modification.

 - Test each strategy in isolation.
---

# The “bad” solution — a growing `switch` you can’t escape

**Scenario:** We need to compute shipping cost for an order. Different carriers/policies compute cost differently.

```csharp
using System;

namespace BadStrategy
{
    public enum ShippingMethod { FlatRate, ByWeight, ByDistance }

    public sealed class Order
    {
        public decimal WeightKg { get; }
        public int DistanceKm { get; }
        public Order(decimal weightKg, int distanceKm)
        { WeightKg = weightKg; DistanceKm = distanceKm; }
    }

    // God-method with switch explosion
    public sealed class ShippingCostService
    {
        public decimal Calculate(Order order, ShippingMethod method)
        {
            switch (method)
            {
                case ShippingMethod.FlatRate:
                    return 5.00m;

                case ShippingMethod.ByWeight:
                    return 3.00m + order.WeightKg * 1.25m;

                case ShippingMethod.ByDistance:
                    return 2.00m + order.DistanceKm * 0.40m;

                default:
                    throw new NotSupportedException($"Unknown method {method}");
            }
        }
    }

    public static class Program
    {
        public static void Main()
        {
            var svc = new ShippingCostService();
            var order = new Order(weightKg: 2.2m, distanceKm: 18);

            Console.WriteLine(svc.Calculate(order, ShippingMethod.FlatRate));
            Console.WriteLine(svc.Calculate(order, ShippingMethod.ByWeight));
            Console.WriteLine(svc.Calculate(order, ShippingMethod.ByDistance));
        }
    }
}
```

**Why this hurts:** every new policy means editing `ShippingCostService` (Open/Closed violation), switch logic sprawls, testing each algorithm requires steering through the same method, and runtime choice logic sprawls across your codebase.

---

# Step-by-step refactor to Strategy

## Step 1 — Define the strategy contract

One interface per *kind* of algorithm.

```csharp
public interface IShippingStrategy
{
    decimal Calculate(Order order);
}
```

## Step 2 — Move each algorithm into its own class

Single responsibility, independent tests.

```csharp
public sealed class FlatRateShipping : IShippingStrategy
{
    private readonly decimal _rate;
    public FlatRateShipping(decimal rate = 5.00m) => _rate = rate;
    public decimal Calculate(Order order) => _rate;
}

public sealed class WeightBasedShipping : IShippingStrategy
{
    private readonly decimal _base, _perKg;
    public WeightBasedShipping(decimal @base = 3.00m, decimal perKg = 1.25m)
    { _base = @base; _perKg = perKg; }
    public decimal Calculate(Order order) => _base + order.WeightKg * _perKg;
}

public sealed class DistanceBasedShipping : IShippingStrategy
{
    private readonly decimal _base, _perKm;
    public DistanceBasedShipping(decimal @base = 2.00m, decimal perKm = 0.40m)
    { _base = @base; _perKm = perKm; }
    public decimal Calculate(Order order) => _base + order.DistanceKm * _perKm;
}
```

## Step 3 — Introduce a context that *uses* a strategy, not a switch

The context doesn’t know which algorithm it runs—only that it can run one.

```csharp
public sealed class ShippingCalculator
{
    private IShippingStrategy _strategy;
    public ShippingCalculator(IShippingStrategy strategy) => _strategy = strategy;

    // Allow swapping at runtime if you like
    public void Use(IShippingStrategy strategy) => _strategy = strategy;

    public decimal Calculate(Order order) => _strategy.Calculate(order);
}
```

## Step 4 — Decide *where* to choose the strategy

Keep selection out of the algorithm code. Use DI, a small factory, or a dictionary in composition code.

```csharp
using System;
using System.Collections.Generic;

public static class StrategyRegistry
{
    private static readonly Dictionary<string, IShippingStrategy> _map =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["flat"]     = new FlatRateShipping(),
            ["weight"]   = new WeightBasedShipping(),
            ["distance"] = new DistanceBasedShipping()
        };

    public static IShippingStrategy Resolve(string key) =>
        _map.TryGetValue(key, out var s)
            ? s
            : throw new ArgumentException($"Unknown strategy key '{key}'");
}
```

## Step 5 — Wire it up at the edges (app/DI), not in the core

Now adding a brand-new algorithm *doesn’t* change the calculator.

```csharp
using System;

namespace StrategyRefactor
{
    // (Order + strategies + ShippingCalculator + StrategyRegistry assumed present)

    public static class Program
    {
        public static void Main()
        {
            var order = new Order(weightKg: 2.2m, distanceKm: 18);

            // Choose strategy at runtime (config, user input, A/B test, feature flag, etc.)
            var calculator = new ShippingCalculator(StrategyRegistry.Resolve("flat"));
            Console.WriteLine($"Flat:     {calculator.Calculate(order):0.00}");

            calculator.Use(StrategyRegistry.Resolve("weight"));
            Console.WriteLine($"ByWeight: {calculator.Calculate(order):0.00}");

            calculator.Use(StrategyRegistry.Resolve("distance"));
            Console.WriteLine($"ByDist:   {calculator.Calculate(order):0.00}");
        }
    }
}
```

---

# The full “fixed” version (consolidated)

```csharp
using System;
using System.Collections.Generic;

namespace StrategyPattern
{
    public sealed class Order
    {
        public decimal WeightKg { get; }
        public int DistanceKm { get; }
        public Order(decimal weightKg, int distanceKm)
        { WeightKg = weightKg; DistanceKm = distanceKm; }
    }

    public interface IShippingStrategy { decimal Calculate(Order order); }

    public sealed class FlatRateShipping : IShippingStrategy
    {
        private readonly decimal _rate;
        public FlatRateShipping(decimal rate = 5.00m) => _rate = rate;
        public decimal Calculate(Order order) => _rate;
    }

    public sealed class WeightBasedShipping : IShippingStrategy
    {
        private readonly decimal _base, _perKg;
        public WeightBasedShipping(decimal @base = 3.00m, decimal perKg = 1.25m)
        { _base = @base; _perKg = perKg; }
        public decimal Calculate(Order order) => _base + order.WeightKg * _perKg;
    }

    public sealed class DistanceBasedShipping : IShippingStrategy
    {
        private readonly decimal _base, _perKm;
        public DistanceBasedShipping(decimal @base = 2.00m, decimal perKm = 0.40m)
        { _base = @base; _perKm = perKm; }
        public decimal Calculate(Order order) => _base + order.DistanceKm * _perKm;
    }

    public sealed class ShippingCalculator
    {
        private IShippingStrategy _strategy;
        public ShippingCalculator(IShippingStrategy strategy) => _strategy = strategy;
        public void Use(IShippingStrategy strategy) => _strategy = strategy;
        public decimal Calculate(Order order) => _strategy.Calculate(order);
    }

    public static class StrategyRegistry
    {
        private static readonly Dictionary<string, IShippingStrategy> _map =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["flat"]     = new FlatRateShipping(),
                ["weight"]   = new WeightBasedShipping(),
                ["distance"] = new DistanceBasedShipping()
            };

        public static IShippingStrategy Resolve(string key) =>
            _map.TryGetValue(key, out var s)
                ? s
                : throw new ArgumentException($"Unknown strategy key '{key}'");
    }

    public static class Program
    {
        public static void Main()
        {
            var order = new Order(weightKg: 2.2m, distanceKm: 18);

            var calc = new ShippingCalculator(StrategyRegistry.Resolve("flat"));
            Console.WriteLine($"Flat:     {calc.Calculate(order):0.00}");

            calc.Use(StrategyRegistry.Resolve("weight"));
            Console.WriteLine($"ByWeight: {calc.Calculate(order):0.00}");

            calc.Use(StrategyRegistry.Resolve("distance"));
            Console.WriteLine($"ByDist:   {calc.Calculate(order):0.00}");
        }
    }
}
```

---

## Key points (quick bullets)

* Encapsulate each algorithm behind a **Strategy interface**.
* Keep the **context** dumb: it only calls `strategy.Calculate()`.
* Select strategies at the edges (config/DI/factory), not with `switch`es in the core.
* Adding a new algorithm **doesn’t edit existing classes** (Open/Closed).
* Testing becomes **trivial**: plug in a fake `IShippingStrategy` and assert interactions.

