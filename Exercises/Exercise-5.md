# Exercise 5: Shopping Cart — Pricing with the Strategy Pattern

## Objective

Implement a flexible pricing system where the “how to compute total price” can be swapped at runtime via the **Strategy pattern**.

You will:

* Define a clean **strategy interface** and multiple concrete strategies.
* Inject/switch strategies at runtime (constructor, setter, or factory).
* Keep the **context** class minimal and closed for modification but open for extension.
* Unit-test behaviors, not implementations.

---

## Scenario

You’re building a shopping cart that needs different pricing rules:

* **StandardPricing** – total is sum of line items.
* **PercentageDiscount** – e.g., 15% off eligible items.
* **BuyXGetYFree** – e.g., buy 2, get 1 free for a SKU.
* **TieredBulkDiscount** – thresholds unlock better unit prices.
* **LoyaltyPointsPricing** (optional) – redeem points to reduce total.

Users (or the system) can choose which pricing strategy applies at checkout time.

---

## Domain (you design the types)

* `CartItem`: SKU, quantity, unit price.
* `Cart`: collection of `CartItem`s + maybe shipping and tax placeholders.
* `Money` can be `decimal` (C# best practice for currency).
* Assume currency is consistent; taxes/shipping can be ignored or added later.

*(Hint: avoid primitive obsession—wrap money/quantity in small types if you like, but keep it simple.)*

---

## Step-by-Step Instructions

### Step 1 — Define the Strategy contract

1. Create an interface for pricing strategies, e.g. `IPricingStrategy`.
2. It should expose **one method** that receives the cart and returns the final total (or a small result object containing breakdowns).

   * Example signature idea (you choose names):
     `PricingResult CalculateTotal(Cart cart)`
3. Define `PricingResult` to include at least:

   * `decimal Subtotal`, `decimal Discount`, `decimal Total`.
   * (Optional) a list of `AppliedRules` strings for explainability.

**Constraints**

* Strategy implementations must be **side-effect free** (pure with respect to the cart).
* Round monetary values consistently (banker’s rounding or explicit `MidpointRounding`).

---

### Step 2 — Create the Context

1. Create a `PriceCalculator` class that depends on `IPricingStrategy`.
2. The calculator should have **no branching** on kinds of strategies.
3. Provide at least one way to select/switch strategy:

   * Constructor injection
   * Setter injection
   * Factory (see Step 5)

**Rule**: `PriceCalculator` should remain unchanged as you add new strategies.

---

### Step 3 — Implement baseline strategy

1. Implement **StandardPricing**:

   * Sum `quantity * unitPrice` for all items → `Subtotal`.
   * `Discount = 0m`.
   * `Total = Subtotal`.

**Acceptance**: With a simple cart, result equals the arithmetic sum.

---

### Step 4 — Implement two promo strategies

Pick any **two** (or can do all 3) from below and implement them carefully:

#### A) PercentageDiscount

* Inputs: `percent` (0–100), optional SKU filter or category filter.
* Compute discount on eligible items only.
* Validate: percent must be within range.

#### B) BuyXGetYFree

* Inputs: `sku`, `buyQty`, `freeQty`.
* For each full `(buyQty + freeQty)` block, only charge for `buyQty`.
* Discount is value of the free units.

#### C) TieredBulkDiscount

* Inputs: thresholds like:

  * `qty >= 10` → 5% off unit price
  * `qty >= 50` → 12% off unit price
* Apply the **best** tier per SKU (or cart-wide; choose and document).

*(Document clearly how your rule applies—per SKU vs per cart.)*

---

### Step 5 — Strategy selection (Factory, optional but recommended)

1. Create a `PricingStrategyFactory` that can build strategies from config or context:

   * Example inputs: `"percentage:15"`, `"bxgy:SKU123:2:1"`, or a structured options object.
2. The factory returns an `IPricingStrategy`.
3. This keeps the calculator free of `switch` statements.

---

### Step 6 — Demonstration (manual)

* Build a few carts (small, medium, edge cases).
* Swap strategies and print a **short breakdown**:

  * Subtotal, Discount, Total
  * Which rule applied (for transparency)

Example console output:

```bash
== Standard ==
  Sku: SKU-APPLE | Quantity: x3 | UnitPrice 1,25 ?
  Sku: SKU-BANANA | Quantity: x6 | UnitPrice 0,80 ?
  Sku: SKU-CHOC | Quantity: x2 | UnitPrice 2,50 ?
  Subtotal: 13,55 ?, Discount: 0,00 ?, Total: 13,55 ?
    - Standard pricing (no discounts)

== 15% off all items ==
  Sku: SKU-APPLE | Quantity: x3 | UnitPrice 1,25 ?
  Sku: SKU-BANANA | Quantity: x6 | UnitPrice 0,80 ?
  Sku: SKU-CHOC | Quantity: x2 | UnitPrice 2,50 ?
  Subtotal: 13,55 ?, Discount: 2,03 ?, Total: 11,52 ?
    - Percentage discount: 15 % on all items

== 10% off CHOC only ==
  Sku: SKU-APPLE | Quantity: x3 | UnitPrice 1,25 ?
  Sku: SKU-BANANA | Quantity: x6 | UnitPrice 0,80 ?
  Sku: SKU-CHOC | Quantity: x2 | UnitPrice 2,50 ?
  Subtotal: 13,55 ?, Discount: 0,50 ?, Total: 13,05 ?
    - Percentage discount: 10 % on SKUs: SKU-CHOC

== Buy2Get1 on BANANA ==
  Sku: SKU-APPLE | Quantity: x3 | UnitPrice 1,25 ?
  Sku: SKU-BANANA | Quantity: x6 | UnitPrice 0,80 ?
  Sku: SKU-CHOC | Quantity: x2 | UnitPrice 2,50 ?
  Subtotal: 13,55 ?, Discount: 1,60 ?, Total: 11,95 ?
    - Buy 2 Get 1 Free on SKU-BANANA: free units=2

== Tiered bulk (per SKU) ==
  Sku: SKU-APPLE | Quantity: x3 | UnitPrice 1,25 ?
  Sku: SKU-BANANA | Quantity: x6 | UnitPrice 0,80 ?
  Sku: SKU-CHOC | Quantity: x2 | UnitPrice 2,50 ?
  Subtotal: 13,55 ?, Discount: 0,24 ?, Total: 13,31 ?
    - Tiered bulk on SKU-BANANA: qty=6, tier=5+ @ 5 %

Factory examples:

== Factory: percent:0.10:SKU-APPLE,SKU-CHOC ==
  Sku: SKU-APPLE | Quantity: x3 | UnitPrice 1,25 ?
  Sku: SKU-BANANA | Quantity: x6 | UnitPrice 0,80 ?
  Sku: SKU-CHOC | Quantity: x2 | UnitPrice 2,50 ?
  Subtotal: 13,55 ?, Discount: 0,88 ?, Total: 12,67 ?
    - Percentage discount: 10 % on SKUs: SKU-APPLE, SKU-CHOC

== Factory: bxgy:SKU-BANANA:2:1 ==
  Sku: SKU-APPLE | Quantity: x3 | UnitPrice 1,25 ?
  Sku: SKU-BANANA | Quantity: x6 | UnitPrice 0,80 ?
  Sku: SKU-CHOC | Quantity: x2 | UnitPrice 2,50 ?
  Subtotal: 13,55 ?, Discount: 1,60 ?, Total: 11,95 ?
    - Buy 2 Get 1 Free on SKU-BANANA: free units=2

== Factory: tiered:5@0.05|10@0.12 ==
  Sku: SKU-APPLE | Quantity: x3 | UnitPrice 1,25 ?
  Sku: SKU-BANANA | Quantity: x6 | UnitPrice 0,80 ?
  Sku: SKU-CHOC | Quantity: x2 | UnitPrice 2,50 ?
  Subtotal: 13,55 ?, Discount: 0,24 ?, Total: 13,31 ?
    - Tiered bulk on SKU-BANANA: qty=6, tier=5+ @ 5 %
```

---

## Acceptance Criteria (overall)

* New pricing strategies can be added **without changing** `PriceCalculator`.
* Strategies are deterministic and side-effect free.
* Monetary rounding is consistent and documented.

---

## Pitfalls to Avoid

* **God object context**: `PriceCalculator` should not “know” strategy details.
* **Hidden state**: Strategies should not mutate the cart or depend on external global state.
* **Rounding drift**: Apply rounding rules in one place (ideally at the end or consistently per line).
* **Misapplied discounts**: Be explicit whether discounts apply per line, per SKU, or cart-wide.
