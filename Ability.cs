
using Godot;

public class Ability
{
    public float power;
    public int range;
    public int cost;

    public Ability(float power, int range, int cost)
    {
        this.power = power;
        this.range = range;
        this.cost = cost;
    }
}