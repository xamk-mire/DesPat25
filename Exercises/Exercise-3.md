# Exercise 3: Factory and Abstract Factory Patterns

## Exercise Overview

In this exercise, you will implement both Factory and Abstract Factory design patterns by creating a game character creation system. You'll learn how these patterns promote loose coupling, encapsulate object creation logic, and make code more maintainable and extensible.

## Learning Objectives

By completing this assignment, you will:

- Understand the Factory Method pattern and when to use it
- Understand the Abstract Factory pattern and when to use it
- Learn to encapsulate object creation logic
- Practice creating flexible and extensible code architectures
- Distinguish between the two factory patterns and their appropriate use cases

## Scenario

You're developing a fantasy RPG game where players can create characters from different races (Human, Elf, Orc) and different themes (Medieval, Futuristic). Each combination should produce characters with appropriate equipment and abilities.

## Part 1: Factory Method Pattern

### Task 1.1: Basic Character Factory

Create a character creation system using the Factory Method pattern.

**Requirements:**

1. Create an abstract `Character` class with the following properties:

   - `Name` (string)
   - `Health` (int)
   - `Strength` (int)
   - `Magic` (int)
   - `Weapon` (string)

2. Create concrete character classes:

   - `Human`: Health=100, Strength=80, Magic=60, Weapon="Sword"
   - `Elf`: Health=80, Strength=60, Magic=100, Weapon="Bow"
   - `Orc`: Health=120, Strength=100, Magic=40, Weapon="Club"

3. Create an abstract `CharacterFactory` class with:

   - Abstract method `CreateCharacter(string name)`
   - Method `DisplayCharacter(Character character)` that prints character stats

4. Create concrete factory classes:

   - `HumanFactory`
   - `ElfFactory`
   - `OrcFactory`

**Expected Output Example:**

```
Character: Aragorn
Race: Human
Health: 100, Strength: 80, Magic: 60
Weapon: Sword
```

### Task 1.2: Factory Registration System

Extend your factory to support dynamic character type registration.

**Requirements:**







1.  Create a `CharacterFactoryRegistry` class that:
	- Maintains a dictionary of character types to factory instances, example. below
	```csharp
	private readonly Dictionary<string, CharacterFactory> _factories;
	```
	- Has `RegisterFactory(string type, CharacterFactory factory)` method
		- Example use in code (creates a HumanFactory under the name "Human")
		 ```csharp
		 var registry = new CharacterFactoryRegistry();
		 registry.RegisterFactory("Human", new HumanFactory());
		 ```
	- Has `CreateCharacter(string type, string name)` method
	- Example use in code (creates new human character called "Boromir")
	```csharp
	 var dynamicHuman = registry.CreateCharacter("Human", "Boromir");
	```
	- Throws appropriate exceptions for unregistered types
    
3.  Demonstrate registering all three factories and creating characters dynamically based on string input.

## Part 2: Abstract Factory Pattern

### Task 2.1: Themed Equipment Factory

Now extend the system to support different game themes using the Abstract Factory pattern. Each theme provides different equipment sets.

**Requirements:**

1. Create equipment interfaces:

   ```csharp
   public interface IWeapon
   {
       string Name { get; }
       int Damage { get; }
       string Attack();
   }

   public interface IArmor
   {
       string Name { get; }
       int Defense { get; }
       string Defend();
   }
   ```

2. Create concrete equipment for Medieval theme:

   - `MedievalSword`, `MedievalBow`, `MedievalClub` (weapons)
   - `ChainMail`, `LeatherArmor`, `IronPlate` (armor)

3. Create concrete equipment for Futuristic theme:

   - `LaserSword`, `PlasmaRifle`, `EnergyMace` (weapons)
   - `EnergyShield`, `NanoSuit`, `PowerArmor` (armor)

4. Create abstract factory interface:

   ```csharp
   public interface IEquipmentFactory
   {
       IWeapon CreateWeapon(CharacterType type);
       IArmor CreateArmor(CharacterType type);
   }
   ```

5. Implement concrete factories:

   - `MedievalEquipmentFactory`
   - `FuturisticEquipmentFactory`

### Task 2.2: Complete Character Creation System

Integrate both patterns to create a complete character creation system.

**Requirements:**

1. Modify your `Character` class to include:

   - `IWeapon Weapon` property
   - `IArmor Armor` property
   - `DisplayFullStats()` method

2. Update your character factories to accept an `IEquipmentFactory` parameter and equip characters with theme-appropriate gear.
3. Create a `GameCharacterBuilder` class that:

   - Takes character type and theme as parameters
   - Uses the appropriate factories to create fully equipped characters
   - Demonstrates the interaction between both factory patterns

## Part 3: Testing and Demonstration

Create a `Program.cs` that demonstrates:

1. **Factory Method Pattern Demo:**

   - Create characters using individual factories
   - Use the registry system to create characters dynamically (centralized creation logic)
   - Handle invalid character types gracefully (code doesn't crash)

2. **Abstract Factory Pattern Demo:**

   - Create the same character types with different equipment themes
   - Show how changing the abstract factory changes the entire equipment family

3. **Combined System Demo:**

   - Create a party of 6 characters (2 of each race) with mixed themes
   - Display their complete statistics including themed equipment

**Expected Output Example:**

```
=== Medieval Human Warrior ===
Name: Sir Lancelot
Race: Human
Health: 100, Strength: 80, Magic: 60
Weapon: Medieval Sword (Damage: 25) - "Slashes with steel blade"
Armor: Chain Mail (Defense: 15) - "Blocks with interlocked rings"

=== Futuristic Elf Ranger ===
Name: Zara-X1
Race: Elf
Health: 80, Strength: 60, Magic: 100
Weapon: Plasma Rifle (Damage: 30) - "Fires concentrated plasma burst"
Armor: Nano Suit (Defense: 20) - "Adapts to incoming damage"
```

## Bonus Tasks

1. **Dynamic Theme Loading:** Implement a system that can load new equipment themes at runtime using reflection or configuration files.
2. **Character Abilities Factory:** Create another abstract factory for character abilities/spells that varies by theme (Medieval magic vs. Futuristic tech abilities).
3. **Factory Chain:** Implement a chain of factories where character creation goes through multiple factory layers (race → class → equipment → abilities).
