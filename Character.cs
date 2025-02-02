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
    public Ability currentAbility { get; private set; }
    public List<Ability> abilities { get; private set; } = new List<Ability>();
    public bool isDead { get; private set; } = false;

    public Character(string name, Vector2I location, float maxHealth, int maxEndurance, List<Ability> abilities)
    {
        this.name = name;
        this.location = location;
        this.maxHealth = maxHealth;
        this.maxEndurance = maxEndurance;
        this.health = maxHealth;
        this.endurance = maxEndurance;
        this.abilities = abilities;
        this.currentAbility = abilities.Count > 0 ? abilities[0] : null;
    }

    public void TakeHit(float amount)
    {
        // Negative amount = healing, Positive amount = damage
        health = Mathf.Clamp(health - amount, 0, maxHealth);
        if (health <= 0)
            isDead = true;
            endurance = 0;
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
        return new Character("Dog", location, 10f, 1, abilities);
    }

}
