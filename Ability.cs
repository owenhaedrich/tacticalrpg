using Godot;
using System.Collections.Generic;

public class Ability
{
    public string name;
    public float power;
    public int range;
    public int cost;
    public List<Effect> effects;

    public Ability(string name, float power, int range, int cost, List<Effect> effects = null)
    {
        this.name = name;
        this.power = power;
        this.range = range;
        this.cost = cost;
        this.effects = effects ?? new List<Effect>();
    }
}