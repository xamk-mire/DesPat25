## The State Design Pattern — A Symphony of Changing Behaviors

Software, at its heart, is a story of transformation. Objects live through phases—birth, growth, decay—each phase altering how they act and react. Yet, in many codebases, we represent this evolution with a mess of `if` and `switch` statements, like a novelist interrupting every chapter to remind readers what happened in the last one. The **State Design Pattern** liberates us from that tangle. It allows an object to change its behavior when its internal state changes, making the object appear to evolve seamlessly, almost **as if it were self-aware**.

Imagine an **airport passenger** making their journey through the terminals.  
At **Check-in**, they may show a passport and drop off luggage, but they cannot board the plane. At **Security**, their luggage is gone, their hands are raised, and “show passport” now means something entirely different. Finally, at the **Gate**, the same command—“show boarding pass”—has a new purpose altogether. The person hasn’t changed; their **context** has. The behaviors are dictated not by the person, but by the **state** they’re in. This is the essence of the State pattern: the same request can yield a completely different response, depending on the current state of the object.

This pattern elegantly separates **what** an object does from **when** it should do it. Each state becomes a self-contained world with its own rules, its own laws of motion. The object itself, like a conductor of an orchestra, doesn’t play an instrument—it merely delegates to the section currently performing. As the music shifts from allegro to adagio, so too does the behavior, without the conductor ever needing to rewrite the score.

The alternative—one we’ve all seen—is the sprawling `switch` statement. A single class carries the heavy burden of knowing every possible state, every possible transition, and every invalid move. It’s as if the passenger had to remember every rule of every terminal, every security checkpoint, and every gate at once. It works, for a while, until new terminals are built, new security policies arrive, and soon the whole structure collapses under its own weight.

Let’s take a concrete example. Picture an **e-commerce order** that passes through the familiar lifecycle of “New → Paid → Shipped → Delivered.” Along the way, the order must forbid certain actions: you can’t ship before payment, you can’t cancel after delivery. Many developers instinctively reach for `if`-chains or `switch` blocks to control these transitions—but that approach quickly hardens into brittle logic.

---

### The Naïve Approach — The Giant Switch

```csharp
public enum OrderStatus { New, Paid, Shipped, Delivered, Cancelled }

public sealed class Order
{
    public OrderStatus Status { get; private set; } = OrderStatus.New;

    public void Pay()
    {
        switch (Status)
        {
            case OrderStatus.New:
                Status = OrderStatus.Paid;
                break;
            default:
                throw new InvalidOperationException($"Cannot pay when {Status}");
        }
    }

    public void Ship()
    {
        switch (Status)
        {
            case OrderStatus.Paid:
                Status = OrderStatus.Shipped;
                break;
            default:
                throw new InvalidOperationException($"Cannot ship when {Status}");
        }
    }

    public void Deliver()
    {
        switch (Status)
        {
            case OrderStatus.Shipped:
                Status = OrderStatus.Delivered;
                break;
            default:
                throw new InvalidOperationException($"Cannot deliver when {Status}");
        }
    }

    public void Cancel()
    {
        switch (Status)
        {
            case OrderStatus.New:
            case OrderStatus.Paid:
                Status = OrderStatus.Cancelled;
                break;
            default:
                throw new InvalidOperationException($"Cannot cancel when {Status}");
        }
    }
}
```

At first glance, this seems fine—logical, even tidy. But as soon as your business grows, new realities intrude: partial shipments, backorders, refunds, failed payments. Each one demands new cases, new conditions, and eventually, your once-crisp `Order` class becomes an unholy tangle of state-dependent behavior. The code no longer _tells a story_; it mutters contradictions.

---

### The State Pattern Solution — When Behavior Becomes Modular

Enter the **State Design Pattern**, which transforms that rigid hierarchy into something fluid and elegant.  
In this refactored version, the `Order` class no longer micromanages its every move. Instead, it delegates behavior to a family of state objects—each representing a specific phase of its lifecycle.

Each state defines **what can be done**, **what cannot**, and **where to go next**.  
To the outside world, the `Order` still exposes simple methods like `Pay()`, `Ship()`, or `Deliver()`, but internally, each call is routed to the appropriate state, much like a customer service hotline that transfers you to the right department depending on your issue.

```csharp
public interface IOrderState
{
    void Pay(Order order);
    void Ship(Order order);
    void Deliver(Order order);
    void Cancel(Order order);
    string Name { get; }
}

public sealed class Order
{
    private IOrderState _state = new NewState();
    public string Status => _state.Name;

    public void Pay()    => _state.Pay(this);
    public void Ship()   => _state.Ship(this);
    public void Deliver()=> _state.Deliver(this);
    public void Cancel() => _state.Cancel(this);

    internal void TransitionTo(IOrderState next)
    {
        Console.WriteLine($"[Order] {_state.Name} → {next.Name}");
        _state = next;
    }
}
```

Each concrete state embodies its own rules:

```csharp
public sealed class NewState : IOrderState
{
    public string Name => "New";
    public void Pay(Order order)     => order.TransitionTo(new PaidState());
    public void Ship(Order order)    => throw Invalid("Ship");
    public void Deliver(Order order) => throw Invalid("Deliver");
    public void Cancel(Order order)  => order.TransitionTo(new CancelledState());
    private static Exception Invalid(string op) => new InvalidOperationException($"{op} not allowed in New");
}
```

And so on for `PaidState`, `ShippedState`, and `DeliveredState`.

Now, when you call:

```csharp
var order = new Order();
order.Pay();
order.Ship();
order.Deliver();
```

you’ll see a beautiful log of transitions:

```
[Order] New → Paid
[Order] Paid → Shipped
[Order] Shipped → Delivered
```

There are no tangled conditionals, no cross-contaminated rules. Each state is a small, self-contained world that can be extended or replaced at will.

---

### The Deeper Beauty

What makes the State pattern elegant isn’t just that it removes `switch` statements—it’s that it **restores narrative clarity** to the code. Each state class tells its own story: “Here’s what I allow, and here’s what I forbid.” If new business logic emerges, you don’t edit a god-class—you add a new chapter. The pattern turns a monolithic decision tree into a living ecosystem of interchangeable behaviors.

As software evolves, your objects—like the airport traveler—will pass through checkpoints, changing not in form, but in **response**. With the State pattern, those transitions happen naturally, without chaos or contradiction. The result is code that reads not as machinery, but as choreography.

---


## When the World Changes: Extending the Order Workflow

Our e-commerce system suddenly faces new demands:

1. **Partial shipments:** Some items can leave the warehouse before others are ready.
    
2. **Refunds:** Orders can now be refunded, but only if they’ve been paid for and not fully delivered.
    

In the old, switch-driven version, you would brace yourself for another round of `if`-laced surgery, carefully threading conditions through every method while praying you don’t break existing logic. But with the State pattern, the process feels almost poetic.  
You simply **add new states**, each encapsulating its own rules, like adding new verses to an existing composition.

---

### Introducing the New States

#### `PartiallyShippedState`

This state acknowledges the messy middle ground where the order is neither “in transit” nor “complete.” It permits additional shipments and, eventually, full delivery.

```csharp
public sealed class PartiallyShippedState : IOrderState
{
    public string Name => "Partially Shipped";

    public void Pay(Order order)     => throw Invalid("Pay");
    public void Ship(Order order)    => order.TransitionTo(new ShippedState());
    public void Deliver(Order order) => order.TransitionTo(new DeliveredState());
    public void Cancel(Order order)  => throw Invalid("Cancel");

    private static Exception Invalid(string op)
        => new InvalidOperationException($"{op} not allowed in {nameof(PartiallyShippedState)}");
}
```

#### `RefundedState`

This state represents closure from a financial reversal. Once entered, it forbids all other operations—an irrevocable full stop.

```csharp
public sealed class RefundedState : IOrderState
{
    public string Name => "Refunded";

    public void Pay(Order order)     => throw Invalid("Pay");
    public void Ship(Order order)    => throw Invalid("Ship");
    public void Deliver(Order order) => throw Invalid("Deliver");
    public void Cancel(Order order)  => throw new InvalidOperationException("Already refunded");

    private static Exception Invalid(string op)
        => new InvalidOperationException($"{op} not allowed in Refunded");
}
```

#### `PaidState` (Revisited)

Now we allow `Refund()`—but only before shipping begins.

```csharp
public sealed class PaidState : IOrderState
{
    public string Name => "Paid";

    public void Pay(Order order)     => throw Invalid("Pay");
    public void Ship(Order order)    => order.TransitionTo(new PartiallyShippedState());
    public void Deliver(Order order) => throw Invalid("Deliver");
    public void Cancel(Order order)  => order.TransitionTo(new CancelledState());

    // New behavior
    public void Refund(Order order)  => order.TransitionTo(new RefundedState());

    private static Exception Invalid(string op)
        => new InvalidOperationException($"{op} not allowed in Paid");
}
```

Notice the elegance: we didn’t need to rewrite the `Order` class, nor did we have to sprinkle new conditions across old methods.  
We merely extended the **contract** (by adding a new operation, `Refund`), and gave certain states the power to respond to it.

---

### Updating the Interface

Naturally, we need to update the `IOrderState` interface and the `Order` class itself to include the new behavior.

```csharp
public interface IOrderState
{
    void Pay(Order order);
    void Ship(Order order);
    void Deliver(Order order);
    void Cancel(Order order);
    void Refund(Order order); // New
    string Name { get; }
}

public sealed class Order
{
    private IOrderState _state = new NewState();
    public string Status => _state.Name;

    public void Pay()    => _state.Pay(this);
    public void Ship()   => _state.Ship(this);
    public void Deliver()=> _state.Deliver(this);
    public void Cancel() => _state.Cancel(this);
    public void Refund() => _state.Refund(this);

    internal void TransitionTo(IOrderState next)
    {
        Console.WriteLine($"[Order] {_state.Name} → {next.Name}");
        _state = next;
    }
}
```

And our `NewState`, `ShippedState`, `DeliveredState`, and `CancelledState` simply implement the new `Refund` method as invalid—no surprises, no danger of silent breakage.

---

### The New Story in Motion

Let’s run through a scenario that would have caused chaos in a switch-based system.

```csharp
var order = new Order();
order.Pay();         // New → Paid
order.Refund();      // Paid → Refunded
```

Output:

```
[Order] New → Paid
[Order] Paid → Refunded
```

No tangled conditionals. No regression nightmares.  
Just a clear, narratively coherent flow.

Or consider partial shipping:

```csharp
var order = new Order();
order.Pay();         // New → Paid
order.Ship();        // Paid → Partially Shipped
order.Ship();        // Partially Shipped → Shipped
order.Deliver();     // Shipped → Delivered
```

Output:

```
[Order] New → Paid
[Order] Paid → Partially Shipped
[Order] Partially Shipped → Shipped
[Order] Shipped → Delivered
```

Every step is clean, deterministic, and extendable. If the business later decides to allow partial refunds on partially shipped orders, you simply enrich that one class with its own rule. The rest of the system remains untouched.

---

## Why This Matters

In the real world, change is the only constant. Business logic evolves; requirements shift. The State pattern doesn’t just make your code “look nice”—it creates a **living architecture** where new behaviors can grow organically without rotting the old ones. Each state becomes a **self-governing module**, fluent in its own rules but obedient to a common protocol.

Your code ceases to be a bureaucratic web of conditionals. It becomes a story—  
a choreography of transformations where objects gracefully adapt, without knowing the details of their own metamorphosis.

---

## Complete C# example (with refunds and partial shipments)

This example models an order lifecycle with these rules:

- `New → Paid → PartiallyShipped → Shipped → Delivered`
    
- You can **Cancel** from `New` or `Paid`.
    
- You can **Refund** only when `Paid` (full refund, terminal).
    
- Partial shipments move `Paid → PartiallyShipped`; more shipping can lead to `Shipped`, and then `Delivered`.
    

```csharp
using System;

namespace StatePatternDemo
{
    // --- State Contract ---
    public interface IOrderState
    {
        string Name { get; }
        void Pay(Order order);
        void Ship(Order order);
        void Deliver(Order order);
        void Cancel(Order order);
        void Refund(Order order);
    }

    // --- Context ---
    public sealed class Order
    {
        private IOrderState _state;

        public Order()
        {
            _state = NewState.Instance;
        }

        public string Status => _state.Name;

        // Public API: delegate to state
        public void Pay()     => _state.Pay(this);
        public void Ship()    => _state.Ship(this);
        public void Deliver() => _state.Deliver(this);
        public void Cancel()  => _state.Cancel(this);
        public void Refund()  => _state.Refund(this);

        internal void TransitionTo(IOrderState next)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            Console.WriteLine($"[Order] {_state.Name} → {next.Name}");
            _state = next;
        }
    }

    // --- Helper: common invalid operation ---
    internal static class Invalid
    {
        public static Exception Op(string op, string state)
            => new InvalidOperationException($"{op} not allowed in {state}");
    }

    // --- Concrete States ---
    public sealed class NewState : IOrderState
    {
        private NewState() { }
        public static NewState Instance { get; } = new NewState();
        public string Name => "New";

        public void Pay(Order order)     => order.TransitionTo(PaidState.Instance);
        public void Ship(Order order)    => throw Invalid.Op("Ship", Name);
        public void Deliver(Order order) => throw Invalid.Op("Deliver", Name);
        public void Cancel(Order order)  => order.TransitionTo(CancelledState.Instance);
        public void Refund(Order order)  => throw Invalid.Op("Refund", Name);
    }

    public sealed class PaidState : IOrderState
    {
        private PaidState() { }
        public static PaidState Instance { get; } = new PaidState();
        public string Name => "Paid";

        public void Pay(Order order)     => throw new InvalidOperationException("Already paid");
        public void Ship(Order order)    => order.TransitionTo(PartiallyShippedState.Instance);
        public void Deliver(Order order) => throw Invalid.Op("Deliver", Name);
        public void Cancel(Order order)  => order.TransitionTo(CancelledState.Instance);
        public void Refund(Order order)  => order.TransitionTo(RefundedState.Instance);
    }

    public sealed class PartiallyShippedState : IOrderState
    {
        private PartiallyShippedState() { }
        public static PartiallyShippedState Instance { get; } = new PartiallyShippedState();
        public string Name => "PartiallyShipped";

        public void Pay(Order order)     => throw Invalid.Op("Pay", Name);
        // Another shipment can complete all items -> Shipped
        public void Ship(Order order)    => order.TransitionTo(ShippedState.Instance);
        // Or fulfillment finalizes directly -> Delivered
        public void Deliver(Order order) => order.TransitionTo(DeliveredState.Instance);
        public void Cancel(Order order)  => throw Invalid.Op("Cancel", Name);
        public void Refund(Order order)  => throw Invalid.Op("Refund", Name); // business rule: no full refund after shipping starts
    }

    public sealed class ShippedState : IOrderState
    {
        private ShippedState() { }
        public static ShippedState Instance { get; } = new ShippedState();
        public string Name => "Shipped";

        public void Pay(Order order)     => throw Invalid.Op("Pay", Name);
        public void Ship(Order order)    => throw new InvalidOperationException("Already shipped");
        public void Deliver(Order order) => order.TransitionTo(DeliveredState.Instance);
        public void Cancel(Order order)  => throw Invalid.Op("Cancel", Name);
        public void Refund(Order order)  => throw Invalid.Op("Refund", Name);
    }

    public sealed class DeliveredState : IOrderState
    {
        private DeliveredState() { }
        public static DeliveredState Instance { get; } = new DeliveredState();
        public string Name => "Delivered";

        public void Pay(Order order)     => throw Invalid.Op("Pay", Name);
        public void Ship(Order order)    => throw Invalid.Op("Ship", Name);
        public void Deliver(Order order) => throw new InvalidOperationException("Already delivered");
        public void Cancel(Order order)  => throw Invalid.Op("Cancel", Name);
        public void Refund(Order order)  => throw Invalid.Op("Refund", Name);
    }

    public sealed class CancelledState : IOrderState
    {
        private CancelledState() { }
        public static CancelledState Instance { get; } = new CancelledState();
        public string Name => "Cancelled";

        public void Pay(Order order)     => throw Invalid.Op("Pay", Name);
        public void Ship(Order order)    => throw Invalid.Op("Ship", Name);
        public void Deliver(Order order) => throw Invalid.Op("Deliver", Name);
        public void Cancel(Order order)  => throw new InvalidOperationException("Already cancelled");
        public void Refund(Order order)  => throw Invalid.Op("Refund", Name);
    }

    public sealed class RefundedState : IOrderState
    {
        private RefundedState() { }
        public static RefundedState Instance { get; } = new RefundedState();
        public string Name => "Refunded";

        public void Pay(Order order)     => throw Invalid.Op("Pay", Name);
        public void Ship(Order order)    => throw Invalid.Op("Ship", Name);
        public void Deliver(Order order) => throw Invalid.Op("Deliver", Name);
        public void Cancel(Order order)  => throw Invalid.Op("Cancel", Name);
        public void Refund(Order order)  => throw new InvalidOperationException("Already refunded");
    }

    // --- Demo ---
    internal static class Program
    {
        private static void Main()
        {
            Console.WriteLine("=== Scenario A: straight-through fulfillment ===");
            var a = new Order();
            a.Pay();     // New -> Paid
            a.Ship();    // Paid -> PartiallyShipped
            a.Ship();    // PartiallyShipped -> Shipped
            a.Deliver(); // Shipped -> Delivered
            Console.WriteLine($"Final: {a.Status}\n");

            Console.WriteLine("=== Scenario B: refund before any shipping ===");
            var b = new Order();
            b.Pay();     // New -> Paid
            b.Refund();  // Paid -> Refunded
            Console.WriteLine($"Final: {b.Status}\n");

            Console.WriteLine("=== Scenario C: cancellation while new ===");
            var c = new Order();
            c.Cancel();  // New -> Cancelled
            Console.WriteLine($"Final: {c.Status}\n");

            Console.WriteLine("=== Scenario D: invalid moves produce clear errors ===");
            var d = new Order();
            try { d.Ship(); } catch (Exception ex) { Console.WriteLine(ex.Message); } // can't ship when New
            d.Pay();
            try { d.Deliver(); } catch (Exception ex) { Console.WriteLine(ex.Message); } // can't deliver when Paid
        }
    }
}
```

### Why this structure scales

- Each state is a **self-contained rulebook**. Adding `Backordered` or `PaymentFailed` doesn’t require spelunking through a monolith—just add a class and wire transitions.
    
- The context (`Order`) remains **stable** and tiny: it delegates and logs transitions.
    
- Errors are **crystal clear**, because invalid operations are rejected where the rule lives.
    

---

## 🔹 The Purpose of `Instance`

In the example, each concrete state class looks like this:

```csharp
public sealed class PaidState : IOrderState
{
    private PaidState() { }
    public static PaidState Instance { get; } = new PaidState();
    public string Name => "Paid";

    public void Pay(Order order)     => throw new InvalidOperationException("Already paid");
    public void Ship(Order order)    => order.TransitionTo(PartiallyShippedState.Instance);
    public void Deliver(Order order) => throw Invalid.Op("Deliver", Name);
    public void Cancel(Order order)  => order.TransitionTo(CancelledState.Instance);
    public void Refund(Order order)  => order.TransitionTo(RefundedState.Instance);
}
```

That `Instance` property is a **singleton instance** of the state — a single, shared object representing “being in the Paid state.”

So instead of creating a _new_ `PaidState()` every time the order transitions, we reuse this single instance:

```csharp
order.TransitionTo(PaidState.Instance);
```

---

## 🔹 Why This Matters

### 1. **States are often stateless**

Most concrete states in a State pattern are **pure behavior**.  
They don’t store unique data — only rules about what’s allowed and what transitions can occur.

Because of that, it’s wasteful to create a new instance every time.  
If 10,000 orders are in the “Paid” state, they can all safely share the same `PaidState.Instance`.

→ This reduces memory use and avoids redundant allocations.

---

### 2. **Clear identity, easy comparison**

Sometimes you need to compare states directly:

```csharp
if (_state == PaidState.Instance) { ... }
```

By using singletons, that comparison works by **reference** (`==`) instead of needing to check by type or string.  
You can tell immediately whether two orders are _literally in the same shared state object._

---

### 3. **Thread-safe and immutable**

The pattern above:

```csharp
public static PaidState Instance { get; } = new PaidState();
```

creates the instance once at type initialization, which is **thread-safe** and **immutable**.  
There’s no need for locks, and no risk of creating multiple copies.

---

### 4. **Cleaner transitions**

Without the singleton, every transition would create new objects:

```csharp
order.TransitionTo(new PaidState());
```

That works fine, but adds unnecessary allocation noise.  
Using `.Instance` emphasizes that the _behavior_, not the _object identity_, is what changes.

---

## 🔹 When Not to Use a Singleton State

If your states carry **instance-specific data**, such as a tracking number or a timestamp, you shouldn’t share them.

For example:

```csharp
public sealed class ShippedState : IOrderState
{
    private readonly string _trackingNumber;
    public ShippedState(string trackingNumber) => _trackingNumber = trackingNumber;
}
```

In that case, you _must_ instantiate a new one each time:

```csharp
order.TransitionTo(new ShippedState("TRACK123"));
```

So the rule of thumb is simple:

|State Type|Shared via `.Instance`?|
|---|---|
|Stateless (pure behavior)|✅ Yes, use `Instance` singleton|
|Stateful (holds data)|🚫 No, create new instance per use|

---

## 🔹 In short

- `Instance` = a reusable, singleton instance of a state.
    
- It’s used because most states are **stateless**, and there’s no reason to create duplicates.
    
- It saves memory, avoids redundant `new` calls, and lets you easily compare or reference states.
    

---

**Metaphorically:**  
Think of the `Instance` as a single rulebook sitting in the office.  
Every order that’s “in the Paid state” doesn’t get its own copy—it just refers to the same rulebook on the shelf.
