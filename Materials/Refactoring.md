## Refactoring — Change the how not the what

Refactoring is how you turn “it works (please don’t touch it)” into “it works (go ahead, change it).”

You already know the first half of the story: a feature is rushed, a deadline looms, you stack one more `if`, one more flag, one more duplication. It passes tests, demos fine, ships. Three months later you’re afraid of that file. No one wants to fix the bug parked in the middle of it because everything is tangled and no one remembers why.

Refactoring is the quiet, disciplined act of changing **how** the code is written without changing **what** it does. It’s not polishing for aesthetics. It’s paying down structural debt so future changes are cheaper, tests are clearer, and patterns you care about can actually breathe.

Think of it as cleaning the kitchen between rushes. You don’t change the menu; you make sure the next service won’t kill anyone.

---

## What refactoring really is (and isn’t)

Refactoring is not a rewrite on a heroic weekend. It’s surgical:

* You keep behavior the same.
* You make many small steps.
* You lean on tests like a climbing rope.

Done well, refactoring turns mystery code into code that:

* tells you what it does just by reading it,
* is split so each piece has one job,
* has seams where you can attach tests, mocks, or new features.

Refactoring goes wrong when it’s:

* untested (“I cleaned it up and… production is down”),
* purely cosmetic (renaming everything without clarifying behavior),
* pattern cosplay (forcing Factory/State/Observer where they don’t belong).

Good refactoring is boringly purposeful. Every extracted method, renamed class, or introduced interface serves a specific clarity, reuse, or testability goal.

---

## A small, honest example: from tangled to testable

Let’s work with something simple and slightly game-flavored: calculating an enemy’s damage.

### The “it works, don’t touch it” version

```csharp
public sealed class DamageService
{
    // Returns final damage based on a pile of rules.
    public int CalculateDamage(string enemyType, int level, bool enraged, bool crit, float resist)
    {
        // base damage
        int dmg;
        if (enemyType == "orc")
        {
            dmg = 10 + level * 2;
            if (enraged)
                dmg += 5;
        }
        else if (enemyType == "dragon")
        {
            dmg = 50 + level * 5;
            if (enraged)
                dmg += 20;
        }
        else
        {
            dmg = 5 + level;
        }

        // crit
        if (crit)
        {
            dmg = (int)(dmg * 1.5f);
        }

        // resist (0.0–1.0)
        dmg -= (int)(dmg * resist);

        // safeguard but also hides issues:
        if (dmg < 0) dmg = 0;

        return dmg;
    }
}
```

It’s short, but all policy is fused:

* new enemy → another `else if`,
* new modifier → more inline math,
* testing specific rules means repeating the same huge method context.

You can’t easily reuse or compose pieces; you just stare at this one function and hope.

---

## Step-by-step refactor: same behavior, better shape

We’ll move in small, safe steps, the way you should in a real codebase.

### 1. Make implicit concepts explicit (types & names)

Replace magic strings and grab-bag parameters with tiny types.

```csharp
public enum EnemyKind
{
    Orc,
    Dragon,
    Other
}

public readonly record struct DamageContext(
    EnemyKind Kind,
    int Level,
    bool Enraged,
    bool Critical,
    float Resist // 0..1
);
```

Now refactor the method signature (behavior unchanged):

```csharp
public sealed class DamageService
{
    public int Calculate(DamageContext ctx)
    {
        int dmg;

        switch (ctx.Kind)
        {
            case EnemyKind.Orc:
                dmg = 10 + ctx.Level * 2;
                if (ctx.Enraged) dmg += 5;
                break;

            case EnemyKind.Dragon:
                dmg = 50 + ctx.Level * 5;
                if (ctx.Enraged) dmg += 20;
                break;

            default:
                dmg = 5 + ctx.Level;
                break;
        }

        if (ctx.Critical)
            dmg = (int)(dmg * 1.5f);

        dmg -= (int)(dmg * ctx.Resist);
        if (dmg < 0) dmg = 0;

        return dmg;
    }
}
```

Functionally identical (your tests should confirm), but clearer. No patterns yet; just honesty.

### 2. Extract policy: open the door for tests and extensions

Now we notice two things:

* per-enemy base damage logic,
* global modifiers (crit, resist).

We tease those apart.

```csharp
public interface IBaseDamageRule
{
    int GetBaseDamage(DamageContext ctx);
}

public sealed class EnemyBaseDamageRule : IBaseDamageRule
{
    public int GetBaseDamage(DamageContext ctx) => ctx.Kind switch
    {
        EnemyKind.Orc    => 10 + ctx.Level * 2 + (ctx.Enraged ? 5  : 0),
        EnemyKind.Dragon => 50 + ctx.Level * 5 + (ctx.Enraged ? 20 : 0),
        _                => 5  + ctx.Level
    };
}

public sealed class DamageService
{
    private readonly IBaseDamageRule _baseRule;

    public DamageService(IBaseDamageRule baseRule)
    {
        _baseRule = baseRule;
    }

    public int Calculate(DamageContext ctx)
    {
        var dmg = _baseRule.GetBaseDamage(ctx);

        if (ctx.Critical)
            dmg = (int)(dmg * 1.5f);

        dmg -= (int)(dmg * ctx.Resist);
        return dmg < 0 ? 0 : dmg;
    }
}
```

Behavior: still the same. But now:

* base rules are isolated (easy to test in tiny unit tests),
* we can inject alternative rules in tests or special modes,
* the main method reads like a story.

At this point, we’ve **refactored**, not “rewritten.” The tests you wrote at the start should still be green.

---

## Where patterns slide in, almost by themselves

Here’s where the earlier design patterns stop being theory and show up as natural refactoring destinations.

### Factory: when creation noise pollutes refactors

As you tease apart logic, you’ll find “heavy” constructions: HTTP clients, DB contexts, engines. Instead of building those inline, you push them behind a factory, which:

* keeps refactors local,
* lets tests supply lightweight or fake versions.

Refactoring move:

* see `new BigThing()` scattered everywhere,
* introduce `IBigThingFactory`,
* use it as a seam.

### DI-style Singleton: when “just one” was a hidden assumption

You’ll notice services that your code assumes are unique (config, clock, logger). Classic singletons make refactoring risky. Converting them to injectable dependencies:

* makes tests simpler,
* removes hidden globals,
* lets you refactor call sites gradually.

Refactoring move:

* wrap static access in an interface,
* inject it,
* register real one as singleton in DI.

### State: when conditionals scream to be split

Big “modeful” methods (like your player controller, order workflow, connection state) are prime refactoring candidates.

Refactoring move:

* identify modes (e.g., Idle, Charging, Firing),
* move their behavior into small classes,
* introduce a context that holds current state.

Suddenly:

* each state can be tested in isolation,
* adding a new mode doesn’t risk the old ones.

### Observer: when changes have too many direct dependents

You’ll find classes that call 5–6 others on each event. That makes refactors brittle.

Refactoring move:

* replace direct calls with an event or message,
* let interested parts subscribe.

Now you:

* test the publisher with a simple listener,
* test listeners with synthetic events,
* refactor interactions without touching everyone.

---

## The discipline: how to refactor without breaking everything

Refactoring is less about bravery and more about rhythm:

1. **Wrap behavior in tests first.** Even a couple of key tests is better than vibes.
2. **Change in small steps.** Extract a method. Introduce a parameter. Add an interface. Run tests.
3. **Prefer renames and extractions over rewrites.** Rewrite only when you understand what’s there.
4. **Introduce patterns only where they reduce pain.** If a pattern doesn’t make tests or changes easier, it’s decoration.

A good refactor leaves the codebase feeling **calmer**:

* fewer special cases,
* clearer names,
* smaller functions,
* behaviors where you expect them.

---

### Key highlights

* Refactoring changes structure, **not** behavior; tests are your safety net.
* When code is hard to test, that’s a **signal** your design wants a seam.
* Factories, DI singletons, State, and Observer are natural refactoring targets: they turn hidden mess into explicit, testable structure.
* Done regularly in small slices, refactoring keeps “legacy” code from forming in the first place.
