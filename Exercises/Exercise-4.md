# Exercise 4: Weather Station — Implement the Classic Observer Pattern

## Objective

You will design and implement a small weather monitoring system using the **Observer pattern** from scratch.
The goal is to learn how to:

* Define clean interfaces for **Subject** and **Observer**.
* Implement **subscribe / unsubscribe / notify** correctly.
* Avoid common pitfalls like duplicate subscriptions, exceptions in observers, and unsafe iteration.

---

## Scenario

You are building a **Weather Station** that gathers temperature readings and broadcasts them to various displays:

* A **ConsoleDisplay** that prints live readings.
* A **StatsDisplay** that keeps track of min, max, and average temperatures.
* A **FileLogger** that stores readings (simulated with in-memory storage).

>[!NOTE]
> You will implement everything manually—no .NET `IObservable<T>`.

---

## Step-by-Step Instructions

### Step 1 — Define the domain model

1. Create a `TemperatureReading` type to represent each reading.

   * It should include:

     * `SensorId` (string)
     * `Timestamp` (DateTimeOffset)
     * `Celsius` (double)

*(Hint: a C# `record struct` works well here.)*

---

### Step 2 — Design the Observer pattern interfaces

1. Create an interface for observers, e.g. `IWeatherObserver`.

   * It needs a single method that gets called when a new reading arrives.
2. Create an interface for the subject, e.g. `IWeatherSubject`.

   * It should support:

     * `Subscribe(IWeatherObserver observer)`
     * `Unsubscribe(IWeatherObserver observer)`

**Important requirements:**

* Subscribing the same observer more than once should have **no effect** (no duplicate notifications).
* Unsubscribing a non-existent observer should be safe (no errors).

---

### Step 3 — Implement the Weather Station (Subject)

1. Create a `WeatherStation` class that implements `IWeatherSubject`.
2. Internally maintain a collection of observers.

   * A `HashSet<IWeatherObserver>` is recommended to prevent duplicates.
3. Implement `Subscribe` and `Unsubscribe`:

   * They should add/remove observers as appropriate.
4. Add a `Publish(TemperatureReading reading)` method:

   * Take a **snapshot** (e.g., convert the collection to an array) before notifying observers.
   * Loop through the snapshot and call the observer method.
   * If an observer throws an exception, catch it so other observers still receive notifications.
5. (Optional but recommended) Add basic thread-safety by synchronizing access to the observer collection.

---

### Step 4 — Implement the Observers

You need at least three observers:

1. **ConsoleDisplay**

   * Prints each reading to the console in a readable format.

2. **StatsDisplay**

   * Keeps track of:

     * Minimum temperature
     * Maximum temperature
     * Average temperature
   * Initialize min/max properly so that the first reading updates them.

3. **FileLogger**

   * Simulates logging by storing each reading in a list of strings in memory.
   * Each stored entry might look like:
     `"{Timestamp},{SensorId},{Celsius}"`

*(You may decide how each observer stores or displays the information.)*

---

### Step 5 — Demonstration

Write a small `Main` method to demonstrate the system:

1. Create a `WeatherStation` instance.
2. Create one instance of each observer.
3. Subscribe all observers to the station.
4. Publish several temperature readings (pretend they come from sensors).
5. Unsubscribe the console display.
6. Publish additional readings.
7. Show that:

   * The console display no longer prints new readings.
   * Stats display reflects all readings it received.
   * File logger has all the data.

---

### Step 6 — Testing

Example output of the application

```bash
--- Console Displays ---
[2025-10-06T09:42:54.7513638+00:00] A: 18,4 °C
[2025-10-06T09:43:54.7513638+00:00] A: 19,1 °C
[2025-10-06T09:44:54.7513638+00:00] B: 17,3 °C

--- StatsDisplay ---
[Stats] count=4, min=17,3, max=20,0, avg=18,70

--- FileLogger ---
[FileLogger] lines written=4
  First line: 2025-10-06T09:42:54.7513638+00:00,A,18,40
  Last line : 2025-10-06T09:45:54.7513638+00:00,A,20,00
```

---

## Key Pitfalls to Avoid

* **Concurrent modification:** Don’t modify the observer list while iterating over it; always work on a snapshot.
* **Exceptions in observers:** Ensure one bad observer cannot stop the rest.
* **Duplicate subscriptions:** Use a `HashSet` or explicit checks.
* **Resource leaks:** Remember to unsubscribe observers that are no longer needed.
