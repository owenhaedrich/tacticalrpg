using Godot;
using System.Collections.Generic;

public class Ability
{
    public string name;
    public float power;
    public int range;
    public int cost;
    public List<Trait> traits;

    public Ability(string name, float power, int range, int cost, List<Trait> traits = null)
    {
        this.name = name;
        this.power = power;
        this.range = range;
        this.cost = cost;
        this.traits = traits ?? new List<Trait>(); // If traits is null, set it to an empty list
    }
}