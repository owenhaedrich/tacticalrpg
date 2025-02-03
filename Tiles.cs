using Godot;

// Atlas coordinates
public static class Tiles
{
	public static readonly Vector2I Ground = new(1, 0);
	public static readonly Vector2I Player = new(0, 0);
	public static readonly Vector2I Movement = new(1, 1);
	public static readonly Vector2I Enemy = new(1, 2);
	public static readonly Vector2I Dead = new(2, 1);
	public static readonly Vector2I Wall = new(0, 1);
	public static readonly Vector2I AbilityTarget = new(0, 2);
}