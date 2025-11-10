## Testing — prevent or fix later, that is the question

Testing is where your code steps out of theory and into the light.

Architecture diagrams, patterns, elegant class names—those are the rehearsals. Tests are opening night: either the system does what you claim, or it doesn’t. When you treat testing as a separate chore bolted on afterward, it feels annoying and fragile. When you treat it as part of how you design, the codebase starts to feel like a place you can move around in without being scared.

Let’s start there: a story-first look at testing itself. Then we’ll weave in design patterns—Factory, DI-style Singleton, State, and Observer—to show how they naturally create places for good tests to attach.

---

## Testing — making your code prove it

Imagine your system as a city at night. Services are buildings. Messages are cars. Requests are people moving through streets. You deploy a change: new routing, new lighting, a different layout. Without tests, your process is “walk around and hope nothing is on fire.” With tests, you’ve installed **sensors**: doors that scream if they don’t open, traffic lights that complain if all turn green, alarms if a critical block goes dark.

You don’t test because you distrust yourself. You test because you know **future you** (and teammates) will be tired, rushed, overconfident, or brand new to the project. Tests are the note you leave them: _“If you touch this wire, this is what must still be true.”_

A healthy testing mindset has a few quiet convictions:

- Behavior is more important than implementation.
    
- Fast, repeatable checks are better than heroic manual clicking.
    
- If something is hard to test, that’s design feedback, not an invitation to give up.
    

Think less “compliance” and more “tight feedback loop”:  
change → run tests → learn immediately. That loop is what keeps large systems, games, and apps from collapsing under their own cleverness.

---

## A brief guide, without the buzzword fog

Start simple. You don’t need a taxonomy carved in stone; you need a feeling for scale.

Most of your safety comes from very small tests: a scoring rule, a pricing rule, a state machine, a mapper. They run in milliseconds, touch no network, no files, no container. They answer: _“Given these inputs, do we compute the right thing?”_ Dozens, then hundreds, then thousands.

Around them: a smaller ring that checks how pieces talk to each other. Does the API actually call the repository? Does saving then loading preserve data? These tests are slower, but reassuring.

On the outside: a thin halo of full “push a request through the stack” or “click through the UI” checks. These are expensive and a little fragile, so you keep only the ones that guard the main arteries.

If you can’t write the small tests without invoking the whole world, the design is too entangled. That’s the moment to reach for patterns.

Key things good tests quietly insist on:

- You can run them often.
    
- They don’t randomly change their minds.
    
- When one fails, it’s obvious what contract got broken.
    

Everything below is about structuring your code so those properties are natural.

---

## How design patterns quietly make testing easier

Patterns, done right, are not ornamental. They carve the code into roles and boundaries that tests can reason about. They say:

- “Creation happens here.”
    
- “Global behavior flows through this abstraction.”
    
- “Mode-specific logic lives in these objects.”
    
- “Events are announced from here, and listeners stand over there.”
    

Those are precisely the points where you can inject fakes, drive behavior, and assert outcomes.

Let’s walk through four patterns with compact, concrete examples.

I’ll keep them realistic but lean; the point is the seam they create.

---

### 1. Factory — test what happens, not how it’s constructed

Think of a payment flow, or a boss spawn, or a mail sender. Without a factory, everything `new`s concrete classes directly, dragging real dependencies into your tests.

With a factory, you move the “how to build this thing” into one place and depend on an interface everywhere else. Tests slip in a harmless implementation and watch how your code behaves without calling real systems.

```csharp
public interface IPizzaOven
{
    Task BakeAsync(string pizzaName);
}

public interface IPizzaOvenFactory
{
    IPizzaOven Create();
}

// Production implementation:
public sealed class StoneOven : IPizzaOven
{
    private readonly int _temperature;
    public StoneOven(int temperature) { _temperature = temperature; }

    public Task BakeAsync(string pizzaName)
    {
        Console.WriteLine($"Baking {pizzaName} at {_temperature}°C in stone oven.");
        return Task.CompletedTask;
    }
}

public sealed class StoneOvenFactory : IPizzaOvenFactory
{
    public IPizzaOven Create() => new StoneOven(480);
}

// Code under test:
public sealed class PizzaService
{
    private readonly IPizzaOvenFactory _factory;
    public PizzaService(IPizzaOvenFactory factory) { _factory = factory; }

    public async Task MakeMargheritaAsync()
    {
        var oven = _factory.Create();
        await oven.BakeAsync("Margherita");
    }
}
```

In production, you use `StoneOvenFactory`. In tests:

```csharp
public sealed class FakeOven : IPizzaOven
{
    public List<string> Baked { get; } = new();
    public Task BakeAsync(string pizzaName)
    {
        Baked.Add(pizzaName);
        return Task.CompletedTask;
    }
}

public sealed class FakeOvenFactory : IPizzaOvenFactory
{
    public FakeOven Oven { get; } = new();
    public IPizzaOven Create() => Oven;
}
```

Now your test is just:

```csharp
[Fact]
public async Task PizzaService_BakesMargherita()
{
    var fakeFactory = new FakeOvenFactory();
    var svc = new PizzaService(fakeFactory);

    await svc.MakeMargheritaAsync();

    Assert.Contains("Margherita", fakeFactory.Oven.Baked);
}
```

No real oven. No console spam required. The Factory pattern turned object-creation spaghetti into a single, testable gateway.

**Key idea:** factories give tests a clean handle on “what kind of thing is used,” without rewriting production code.

---

### 2. DI-style Singleton — one instance, zero hidden globals

Some things are singular: configuration, clock, metrics sink. The classic Singleton pattern answers this by hiding a static instance. That works—until you want a test to see a different config or a frozen clock.

A DI-style singleton expresses the same intent (“one shared thing”) but keeps it injectable and replaceable.

```csharp
public interface IGameClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemGameClock : IGameClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public sealed class DailyBonusService
{
    private readonly IGameClock _clock;
    public DailyBonusService(IGameClock clock) { _clock = clock; }

    public bool IsBonusAvailable() =>
        _clock.UtcNow.Hour == 0;
}
```

Production: register `SystemGameClock` as a singleton in your DI container. Tests:

```csharp
public sealed class FakeClock : IGameClock
{
    public DateTime UtcNow { get; set; }
}

[Fact]
public void Bonus_OnlyAtMidnight()
{
    var clock = new FakeClock { UtcNow = new DateTime(2025, 11, 10, 00, 00, 00) };
    var svc = new DailyBonusService(clock);

    Assert.True(svc.IsBonusAvailable());
}
```

Same concept—**one clock**—but now your tests can bend time without touching static globals.

**Key idea:** treat “only one instance” as a lifetime rule, not as an excuse for invisible dependencies.

---

### 3. State — turn tangled conditionals into testable modes

Anywhere behavior depends on “mode” (gameplay states, workflow steps, connection lifecycle) you’ll see the usual suspects: `isActive`, `isPaused`, `isDead`, `isSomething`. Tests then have to reconstruct weird flag combinations to reach a scenario.

The State pattern says: make each mode its own object. The active state decides what inputs mean and when to transition. Tests then ask, “In this state, what happens?”

```csharp
public enum Input { PressJump, PressPause, Resume }

public interface IGameState
{
    string Name { get; }
    IGameState Handle(Input input);
}

public sealed class Playing : IGameState
{
    public string Name => "Playing";

    public IGameState Handle(Input input) => input switch
    {
        Input.PressPause => new Paused(),
        _ => this
    };
}

public sealed class Paused : IGameState
{
    public string Name => "Paused";

    public IGameState Handle(Input input) => input switch
    {
        Input.Resume => new Playing(),
        _ => this
    };
}

public sealed class Game
{
    public IGameState State { get; private set; } = new Playing();

    public void Handle(Input input)
    {
        State = State.Handle(input);
    }
}
```

Test reads like the design doc:

```csharp
[Fact]
public void Game_PausesAndResumes()
{
    var game = new Game();

    Assert.Equal("Playing", game.State.Name);

    game.Handle(Input.PressPause);
    Assert.Equal("Paused", game.State.Name);

    game.Handle(Input.Resume);
    Assert.Equal("Playing", game.State.Name);
}
```

No chasing flags. No risk that some unrelated branch forgot to check `isPaused`. Each state is small, coherent, and testable in isolation.

**Key idea:** when you encode modes explicitly, tests become stories instead of archaeology.

---

### 4. Observer — make effects visible without hard wiring

Often you have a central event—“order completed”, “HP changed”, “enemy killed”—and many things should react: achievements, analytics, UI, sound, etc.

Tightly coupled version: the core class calls every dependency directly. Tests get tangled or hit real services.

Observer-style version: the core class emits a signal; listeners subscribe. Tests attach a tiny listener and assert what it saw.

```csharp
public sealed class OrderService
{
    public event Action<int>? OrderCompleted;  // orderId

    public void Complete(int orderId)
    {
        // domain logic...
        OrderCompleted?.Invoke(orderId);
    }
}
```

Test:

```csharp
[Fact]
public void OrderService_RaisesEventOnComplete()
{
    var svc = new OrderService();
    int? received = null;
    // Initialize OrderCompleted action
    svc.OrderCompleted += id => received = id;

    svc.Complete(42);

    Assert.Equal(42, received);
}
```

In production you hook real observers (emails, logs, UI). In tests you hook a minimal probe.

**Key idea:** Observer turns “many effects” into something you can observe and assert without faking an entire environment.

---

## Pulling it together

Design patterns are not magic stickers you slap on for style points. Used thoughtfully, they:

- make dependencies explicit instead of hidden,
    
- isolate behaviors into small units instead of giant blobs,
    
- centralize creation and side effects instead of scattering them.
    

All of those are exactly what good tests beg for.

If you ever catch yourself saying, “This is so coupled, testing it will be painful,” don’t just write a painful test—take it as a hint. That’s where a factory, an interface, a state object, or an event can turn a messy tangle into something your tests (and your future self) can actually live with.
