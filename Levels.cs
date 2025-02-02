using Godot;
using System.Collections.Generic;

public static class Levels
{
    public class LevelData
    {
        public Character[] Party { get; set; }
        public Character[] Enemies { get; set; }
    }

    private static readonly Dictionary<int, LevelData> levelConfigs = new()
    {
        {
            0, new LevelData
            {
                Party = new[] {
                    Character.Domli(new Vector2I(3, 3)),
                    Character.Zash(new Vector2I(5, 5))
                },
                Enemies = new[] {
                    Character.Dog(new Vector2I(15, 11)),
                    Character.EarthSpirit(new Vector2I(17, 11)),
                    Character.Goblin(new Vector2I(16, 9)),
                    Character.GiantBug(new Vector2I(14, 10))
                }
            }
        },
    };

    public static LevelData GetLevel(int level)
    {
        return levelConfigs.GetValueOrDefault(level) ?? levelConfigs[1];
    }
}
