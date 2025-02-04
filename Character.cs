using Godot;
using System.Collections.Generic;

public class Character
{
    public Vector2I location;
    public float health;
    public float maxHealth;
    public int endurance;
    public int maxEndurance;
    public string name;
    public Ability currentAbility;
    public List<Ability> abilities = new List<Ability>();
    public bool isDead = false;
    public EnemyAI.PathfindingStrategy pathfinding;

    public Character(string name, Vector2I location, float maxHealth, int maxEndurance, 
                    List<Ability> abilities, EnemyAI.PathfindingStrategy pathfinding = EnemyAI.PathfindingStrategy.SmartPath)
    {
        this.name = name;
        this.location = location;
        this.maxHealth = maxHealth;
        this.maxEndurance = maxEndurance;
        this.health = maxHealth;
        this.endurance = maxEndurance;
        this.abilities = abilities;
        this.currentAbility = abilities.Count > 0 ? abilities[0] : null;
        this.pathfinding = pathfinding;
    }

    public void TakeHit(float amount)
    {
        // Negative amount = healing, Positive amount = damage
        health = Mathf.Clamp(health - amount, 0, maxHealth);
        if (health <= 0)
        {
            isDead = true;
            endurance = 0;
        }
    }

    //Heroes

    public static Character Zash(Vector2I location)
    {
        List<Ability> abilities = new List<Ability>
        {
            new Ability("Skysplitter Cleave", 10f, 1, 1),
        };
        return new Character("Zash", location, 30f, 2, abilities);
    }

    public static Character Domli(Vector2I location)
    {
        List<Ability> abilities = new List<Ability>
        {
            new Ability("Healing Ray", -5f, 3, 1),
        };
        return new Character("Domli", location, 20f, 3, abilities);
    }

    //Enemies

    public static Character Dog(Vector2I location)
    {
        List<Ability> abilities = new List<Ability>
        {
            new Ability("Bite", 5f, 1, 1),
        };
        return new Character("Dog", location, 10f, 5, abilities);
    }

    public static Character EarthSpirit(Vector2I location)
    {
        List<Ability> abilities = new List<Ability>
        {
            new Ability("Stone Wall", 6f, 1, 1),
            new Ability("Earth Tremor", 3f, 2, 3),
        };
        return new Character("Earth Spirit", location, 15f, 5, abilities, EnemyAI.PathfindingStrategy.FlankPath);
    }

    public static Character Goblin(Vector2I location)
    {
        List<Ability> abilities = new List<Ability>
        {
            new Ability("Dagger Slash", 4f, 1, 1),
            new Ability("Dirty Trick", 2f, 2, 2),
            new Ability("Rock Throw", 1f, 3, 1),
        };
        return new Character("Goblin", location, 8f, 6, abilities, EnemyAI.PathfindingStrategy.CautiousPath);
    }

    public static Character GiantBug(Vector2I location)
    {
        List<Ability> abilities = new List<Ability>
        {
            new Ability("Mandible Crush", 9f, 1, 1),
            new Ability("Acid Spray", 4f, 2, 2),
        };
        return new Character("Giant Bug", location, 20f, 5, abilities, EnemyAI.PathfindingStrategy.FlankPath);
    }
}
