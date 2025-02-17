using Godot;
using System.Collections.Generic;
using System.Linq;

public static class MovementUtils
{
    private static HashSet<Character> allCharacters = new HashSet<Character>();
    private static Character currentCharacter;
    public static readonly Vector2I[] Cardinals = { Vector2I.Right, Vector2I.Up, Vector2I.Left, Vector2I.Down };
    public static readonly Vector2I[] Diagonals = {
        new Vector2I(-1, 1), new Vector2I(1, 1),
        new Vector2I(-1, -1), new Vector2I(1, -1)
    };

    public static void RegisterCharacter(Character character)
    {
        allCharacters.Add(character);
    }

    public static void UnregisterCharacter(Character character)
    {
        allCharacters.Remove(character);
    }

    public static void SetCurrentCharacter(Character character)
    {
        currentCharacter = character;
    }

    public static bool HasEnoughEndurance(int cost)
    {
        return currentCharacter != null && currentCharacter.endurance >= cost;
    }

    public static HashSet<Vector2I> UpdateOccupiedPositions()
    {
        var positions = new HashSet<Vector2I>();
        foreach (var character in allCharacters.Where(c => !c.isDead))
        {
            positions.Add(character.location);
        }
        return positions;
    }

    public static bool IsInBounds(Vector2I pos, TileMapLayer map)
    {
        var rect = map.GetUsedRect();
        return pos.X >= 0 && pos.Y >= 0 && 
               pos.X < rect.Size.X && pos.Y < rect.Size.Y;
    }

    public static bool IsValidMove(Vector2I pos, TileMapLayer map)
    {
        if (!IsInBounds(pos, map))
            return false;

        var occupied = UpdateOccupiedPositions();
        bool isValidTerrain = map.GetCellAtlasCoords(pos) == Tiles.Ground || 
                             map.GetCellAtlasCoords(pos) == Tiles.Dead;
        return isValidTerrain && !occupied.Contains(pos);
    }

    public static bool IsAtDistance(Vector2I pos, Vector2I target, int distance) =>
        GetManhattanDistance(pos, target) == distance;

    public static int GetManhattanDistance(Vector2I a, Vector2I b) =>
        Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);

    public static Vector2I DirectionTo(Vector2I from, Vector2I to) =>
        (to - from).Sign();
}
