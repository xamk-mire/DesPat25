## The Factory Pattern — a story about who should own `new`

Imagine a bustling café just after sunrise. Orders fly in: _“Double espresso!” “Oat-milk latte, extra hot!” “Cappuccino—dry!”_  
If every barista had to memorize the _construction details_ of each drink—precise ratios, steaming rules, foam textures—chaos would creep in. Training would be slow, mistakes common, and adding a new seasonal special would mean teaching everyone all over again.

Good cafés solve this with **recipes** and **a single point where drinks are created**. Baristas work from a consistent “maker” that knows _how_ to assemble any drink; everyone else just asks for a drink by name. That “maker” is your **factory**.

Below, we’ll first look at code that behaves like a frantic café—baristas (callers) mixing drinks themselves. Then we’ll refactor to the **Factory (Factory Method) pattern** so creation is centralized, extendable, and testable. We’ll keep our story in the café, and the code in C#.

---

## The Messy Café (Bad Code)

Here, every part of the app knows **exactly** how to build each coffee. The result is tight coupling and endless `switch` statements. Add a new drink? You’ll be editing code all over the place.

```csharp
using System;

namespace CoffeeShop.Bad
{
    public interface ICoffee
    {
        string Name { get; }
        void Prepare(); // imagine steps: grind, brew, steam, pour...
    }

    public class Espresso : ICoffee
    {
        public string Name => "Espresso";
        public void Prepare() => Console.WriteLine("Pulling a rich, 30ml shot.");
    }

    public class Latte : ICoffee
    {
        public string Name => "Latte";
        public void Prepare() => Console.WriteLine("Pulling shot, steaming milk silky, pouring art.");
    }

    public class Cappuccino : ICoffee
    {
        public string Name => "Cappuccino";
        public void Prepare() => Console.WriteLine("Pulling shot, airy foam, classic thirds.");
    }

    // Everywhere in the app… the same branching logic repeats.
    public class OrderScreen
    {
        public void OnOrderSubmitted(string drinkCode)
        {
            ICoffee coffee;
            switch (drinkCode.ToLowerInvariant())
            {
                case "espresso":
                    coffee = new Espresso();
                    break;
                case "latte":
                    coffee = new Latte();
                    break;
                case "cappuccino":
                    coffee = new Cappuccino();
                    break;
                default:
                    throw new ArgumentException($"Unknown drink: {drinkCode}");
            }

            // Now this UI knows *construction details*—and so does the printer, and the API…
            coffee.Prepare();
            Console.WriteLine($"Serving: {coffee.Name}");
        }
    }
}
```

**Problems with this approach**

- Every caller knows too much: if you add **Mocha**, you must hunt down every `switch` and update it.
- Violates the **Open/Closed Principle**: adding a drink **modifies** existing code instead of **extending** behavior.
- Harder to test: creation is entangled with UI, printing, payment, etc.
- Duplication grows: the same mapping logic creeps into multiple places.

---

## The Well-Run Café (Good Code: Factory Pattern / Factory Method)

We promote a single **CoffeeShop** that knows how to create drinks via a **factory method**. Callers say _what_ they want; the shop decides _how_ to build it. Shared flow (take payment → create → prepare → serve) is stable; **creation varies** through an overridable method.

### Core contracts and products

```csharp
using System;

namespace CoffeeShop.Good
{
    // Product interface
    public interface ICoffee
    {
        string Name { get; }
        void Prepare();
    }

    // Concrete products
    public class Espresso : ICoffee
    {
        public string Name => "Espresso";
        public void Prepare() => Console.WriteLine("Pulling a rich, 30ml shot.");
    }

    public class Latte : ICoffee
    {
        public string Name => "Latte";
        public void Prepare() => Console.WriteLine("Pulling shot, steaming milk silky, pouring art.");
    }

    public class Cappuccino : ICoffee
    {
        public string Name => "Cappuccino";
        public void Prepare() => Console.WriteLine("Pulling shot, airy foam, classic thirds.");
    }
```

### The creator (factory) and the factory _method_

```csharp
    // Creator: defines the stable workflow, defers creation to the factory method.
    public abstract class CoffeeShop
    {
        // Stable order flow—kept in one place.
        public ICoffee Order(string drinkCode)
        {
            TakePayment(drinkCode);
            var coffee = CreateCoffee(drinkCode); // <-- Factory Method
            coffee.Prepare();
            Serve(coffee);
            return coffee;
        }

        protected abstract ICoffee CreateCoffee(string drinkCode);

        protected virtual void TakePayment(string drinkCode) =>
            Console.WriteLine($"Taking payment for: {drinkCode}");

        protected virtual void Serve(ICoffee coffee) =>
            Console.WriteLine($"Serving: {coffee.Name}");
    }
```

### Concrete creators (different “shops” can create differently)

```csharp
    // A classic Italian shop: limited, purist menu.
    public class ItalianCoffeeShop : CoffeeShop
    {
        protected override ICoffee CreateCoffee(string drinkCode) =>
            drinkCode.ToLowerInvariant() switch
            {
                "espresso"    => new Espresso(),
                "cappuccino"  => new Cappuccino(),
                _ => throw new ArgumentException($"Not on Italian menu: {drinkCode}")
            };
    }

    // A modern specialty shop: broader, milk-forward menu.
    public class SpecialtyCoffeeShop : CoffeeShop
    {
        protected override ICoffee CreateCoffee(string drinkCode) =>
            drinkCode.ToLowerInvariant() switch
            {
                "espresso"   => new Espresso(),
                "latte"      => new Latte(),
                "cappuccino" => new Cappuccino(),
                _ => throw new ArgumentException($"Unknown drink: {drinkCode}")
            };
    }
}
```

### Using it

```csharp
using CoffeeShop.Good;

class Program
{
    static void Main()
    {
        CoffeeShop italian = new ItalianCoffeeShop();
        italian.Order("espresso");

        CoffeeShop specialty = new SpecialtyCoffeeShop();
        specialty.Order("latte");

        // Adding a new drink? Create a new ICoffee (e.g., Mocha) and update only the shop(s)
        // that actually serve it—or add a brand-new shop subtype with its own menu logic.
    }
}
```

**Why this is better**

- The **recipe binder** (factory method) centralizes creation; baristas don’t freestyle.
- You can launch a **new location** (subclass) with its own menu without disrupting others.
- Shared steps (payment/prepare/serve) live in one place; **only creation varies**.
- It’s easier to test: you can mock a `CoffeeShop` or test each `CreateCoffee` in isolation.

---

## Notes, Nuance, and Practicalities

- **“Factory pattern” vs. “Factory Method”:** In GoF terms, what we implemented is **Factory Method**—a creator class defers object creation to a specialized method that subclasses override. In day-to-day dev talk, many say “factory pattern” to mean this.
- **Open/Closed in practice:** You typically **extend** by adding a _new_ product (`ICoffee` implementation) and teaching only the **relevant** shop(s) how to make it. Existing callers still just `Order("mocha")` from the shop that supports it.
- **Where to put the mapping?** We put it in each concrete shop for clarity. In larger systems, you can move mapping into a **registrable** lookup inside the shop (e.g., a `Dictionary<string, Func<ICoffee>>`) to avoid long switches—still a factory method because the shop remains the creator.
- **Dependency Injection:** If your coffees have dependencies (grinders, temperature controllers), the shop can receive those via constructor injection and pass them to product constructors. The factory method stays the single gateway for creation.
- **Testing strategy:** Unit-test `CreateCoffee` behavior per shop and each `ICoffee.Prepare` sequence. Because creation is centralized, tests are simpler and less brittle.

---

## Quick Spot-Check Guide (when to reach for a factory)

- You see `new` scattered everywhere and copy-pasted `switch`es on _type codes_.
- Adding a product means touching several unrelated classes.
- You want one stable flow (e.g., “take payment → create → prepare → serve”) where only the _created thing_ varies.
- You anticipate product families evolving over time (seasonal specials, regional menus).

---

## The three cousins (with plain analogies)

- **Simple Factory**: a counter where you say “two house keys, please,” and the clerk handles blanks, alignment, and polishing. Callers get the result without seeing the machine. It’s just a function or object that returns the right product and hides messy steps.
- **Factory Method**: a kitchen that keeps plating, timing, and service the same, but lets each _specialized chef_ pick the protein for a dish. The base class defines the flow; subclasses override the creation step, deciding _which_ concrete product to use.
- **Abstract Factory**: furnishing a room by choosing a _style_—Scandinavian, Industrial—and receiving a matching **family**: chair, table, lamp that all fit together. One factory hands out several related products that are guaranteed to be compatible.

You’ll meet all three in the wild. Start with a simple factory; reach for Factory Method when the choice hangs on subclass behavior; use Abstract Factory when consumers need a _set_ of coordinated parts.

---

## A tiny, realistic taste (TypeScript)

Here’s a “concierge” that chooses and prepares a store. Notice how async prep and selection disappear from callers:

```ts
interface Store {
  get(key: string): Promise<string | null>;
}

class MemoryStore implements Store {
  private data = new Map<string, string>();
  async get(k: string) {
    return this.data.get(k) ?? null;
  }
}

class RedisStore implements Store {
  constructor(private conn: unknown) {}
  async get(k: string) {
    /* talk to Redis */ return null;
  }
}

interface StoreFactory {
  create(): Promise<Store>;
}

class DefaultStoreFactory implements StoreFactory {
  private cached?: Store;

  async create(): Promise<Store> {
    if (this.cached) return this.cached; // pooling
    const kind = process.env.STORE_KIND ?? 'memory'; // selection
    this.cached =
      kind === 'redis'
        ? await this.makeRedis() // async setup
        : new MemoryStore();
    return this.cached;
  }

  private async makeRedis(): Promise<Store> {
    const conn = await this.connectWithRetries(); // policy location
    return new RedisStore(conn);
  }

  private async connectWithRetries() {
    /* metrics, backoff, etc. */ return {};
  }
}

// Caller code stays clean and swappable in tests:
async function loadUser(factory: StoreFactory, id: string) {
  const store = await factory.create();
  return store.get(`user:${id}`);
}
```

In tests you can inject a `FakeStoreFactory` and skip the world.

## Mental model: _who owns the “mess”?_

Every nontrivial system accumulates “construction mess”: parameter validation; third-party SDK glue; environment-dependent choices; cross-cutting concerns like logging, metrics, and auth; lifecycle rules (cache vs fresh). If callers own the mess, it spreads. If a factory owns it, it’s contained—auditable, testable, and changeable.

## When factories go wrong

A factory can become a **God Factory**—a single file with a growing `switch` that knows every product. Prefer a _registry_ (map string → maker) so new types can register themselves, or split by bounded context. Beware the **Service Locator** anti-pattern: a global “box” you reach into at runtime. It hides dependencies and sabotages tests. Prefer passing a **factory interface** through constructors so usage is explicit.

Another smell is a **leaky factory**: callers still branch on concrete types after receiving an object. That means your abstraction is thin—strengthen the interface so consumers don’t need to peek.

## DI containers and factories: teammates, not rivals

A dependency-injection container is like a city planner who knows where every workshop is and how to wire a whole block at once. It’s effectively a big, declarative factory of _graphs_ of objects. You still want small **assisted factories** for runtime values (e.g., “create an `Invoice` for this `customerId`”), and for places where you need explicit policy or async work that DI doesn’t capture cleanly.

## Choosing among the cousins (quick intuition)

- Need to **hide construction ceremony** or pick one of a few variants? Start with a **Simple Factory**—often just a function.
- Have a **base workflow** with a single creation point that changes by subclass? Use **Factory Method**.
- Need **families of matching parts** (widgets, drivers, themed components)? Reach for an **Abstract Factory**.

## !! When not to bother !!!

If you’re building a tiny data object with no I/O, no variants, and no policy, a constructor is clearer. Factories add indirection; earn that indirection with real needs.

## A short field guide (what to look for)

When you see long constructor parameter lists copied around; tests struggling with I/O at object creation; environment checks sprinkled across the code; or every new feature requiring edits to a dozen `new` calls—those are footprints leading straight to a factory.

---

### Key points to remember

- Factories **centralize** creation and **decouple** callers from concrete types.
- They are the natural home for **cross-cutting policies** and **async setup**.
- Pick **Simple / Factory Method / Abstract Factory** based on whether you need one product, subclass-driven choice, or a coordinated family.
- Avoid **God Factories** and **Service Locator**; prefer small, explicit factory interfaces.
- Use constructors when creation is trivial; **introduce a factory when the “how” starts to sprawl**.
