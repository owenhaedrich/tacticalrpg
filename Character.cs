using Godot;
using System.Collections.Generic;

public class Character
{
    public Vector2I location;
    public int health;
    public int maxHealth;
    public int endurance;
    public int maxEndurance;
    public List<Ability> abilities;

    public Character(Vector2I location, int maxHealth, int maxEndurance)
    {
        this.location = location;
        this.maxHealth = maxHealth;
        this.maxEndurance = maxEndurance;
        this.health = maxHealth;
        this.endurance = maxEndurance;
        this.abilities = new List<Ability>();
    }
}
