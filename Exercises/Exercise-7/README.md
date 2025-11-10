# Exercise 7 — Refactoring: From God‑Class to Patterns with Passing Tests

## Overview

This exercise uses the **LegacyShop** ready-to-run .NET 8 repository as the template. You will receive this repository, which includes a deliberately bad implementation of an order processing system (`OrderProcessor.cs`) and a failing xUnit test suite. The goal is to refactor the codebase using appropriate **design patterns** until all tests pass and new tests can be easily written.

> Target runtime: .NET 8 / .NET 9
> Test framework: xUnit

---

## Learning Objectives

- Practice **refactoring** hard-to-test legacy code
- Replace conditional logic and magic strings with **Strategy** and **Factory** patterns
- Isolate external dependencies using **Dependency Injection** and **Adapter/Repository** patterns
- Remove hidden side effects (e.g., console output) to enable **deterministic testing**
- Write new, maintainable tests against abstractions

---

## Setup Instructions

> [!NOTE]
> The solution template doesn't include runnable program file, so instead you can run the application using the tests

1. Download and move the provided `LegacyShop` (`Exercise-7`) repository inside your own exercise repository.
2. Open the solution folder in your IDE or terminal.
3. Run the following commands:

   ```bash
   dotnet restore
   dotnet test
   ```

   You’ll see failing tests — this is expected.

4. Your task is to refactor the code in `src/LegacyShop` so that all existing tests pass **without modifying the test assertions**.

---

## Refactoring Goals

1. **Pass all existing tests** in `tests/LegacyShop.Tests`.
2. Make it easy to add new tests without changing existing logic.
3. Apply SOLID principles and proper design patterns:

   - Use **Strategy** for shipping and payment fee calculations.
   - Use a **Factory** (or registry) to select strategies instead of `switch` statements or magic strings.
   - Implement **exclusive discount logic** using a composite or chain (e.g., first-match or chain-of-responsibility).
   - Apply **Dependency Injection** to inject strategies and policies.
   - Remove side effects such as console logging from the domain layer.
   - Ensure **tax is 10%** of `(subtotal − discount + shipping)` and excludes payment fees.

---

## Acceptance Criteria

- All three provided tests pass **without changing their assertions**.
- `OrderProcessor` no longer uses magic strings, `switch` statements, or console output.
- New payment or shipping methods can be added without modifying the core logic.
- Discounts are **exclusive** — the first matching policy applies.
- Tax calculation correctly excludes payment fees.

---

## Stretch Goals

Once the base refactor passes all tests, extend the solution by adding the following features:

1. **New payment method:** `Crypto` with a 1.5% fee.
2. **Free shipping promotion:** Subtotal ≥ 250 results in zero shipping cost.
3. **Date-based discount:** Introduce an `IDateTimeProvider` abstraction and create a `FirstDayOfMonthDiscount` policy (e.g., flat $3 off on the first day of each month).
4. **IoC container integration:** Wire everything together using `Microsoft.Extensions.DependencyInjection` to demonstrate testable dependency resolution.

---

## Instructor Notes

- Run `dotnet test` before and after refactoring to visualize progress.
- Encourage incremental commits at key refactoring milestones.
- Evaluate based on:
  - Removal of switch statements and magic strings.
  - Application of correct design patterns.
  - Clarity and extensibility of refactored architecture.

---

## Deliverables

- Refactored code in `src/LegacyShop` with patterns correctly implemented.
- All tests passing in `tests/LegacyShop.Tests`.
- (Optional) Additional tests verifying new payment and shipping logic.
