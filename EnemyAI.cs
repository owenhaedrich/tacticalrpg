using Godot;
using System;

public class EnemyAI
{
    private static readonly Vector2I TILE_GROUND = new Vector2I(1, 0);

    public struct EnemyAction
    {
        public Vector2I moveDirection;
        public bool useAbility;
        public Vector2I targetPosition;

        public EnemyAction(Vector2I move, bool ability, Vector2I target)
        {
            moveDirection = move;
            useAbility = ability;
            targetPosition = target;
        }
    }

    public static EnemyAction GetAction(Vector2I enemyPos, Character[] targets, int abilityRange, TileMapLayer map)
    {
        Vector2I targetPos = FindClosestTarget(enemyPos, targets);
        
        // Check if any target is in ability range
        foreach (Character target in targets)
        {
            if (IsInAbilityRange(enemyPos, target.location, abilityRange))
            {
                return new EnemyAction(Vector2I.Zero, true, target.location);
            }
        }

        // If no target in range, move towards closest target
        Vector2I moveDir = GetMoveDirection(enemyPos, targetPos, map);
        return new EnemyAction(moveDir, false, Vector2I.Zero);
    }

    private static bool IsInAbilityRange(Vector2I source, Vector2I target, int range)
    {
        return source.DistanceTo(target) <= range;
    }

    public static Vector2I GetMoveDirection(Vector2I enemyPos, Vector2I targetPos, TileMapLayer map)
    {
        Vector2I direction = targetPos - enemyPos;
        
        // Normalize the direction to single step
        Vector2I normalizedDir = new Vector2I(
            Math.Sign(direction.X),
            Math.Sign(direction.Y)
        );

        // If diagonal movement, try to move in either X or Y direction
        if (normalizedDir.X != 0 && normalizedDir.Y != 0)
        {
            Vector2I xMove = new Vector2I(normalizedDir.X, 0);
            Vector2I yMove = new Vector2I(0, normalizedDir.Y);

            if (IsValidMove(enemyPos + xMove, map))
                return xMove;
            if (IsValidMove(enemyPos + yMove, map))
                return yMove;
        }

        // Try the normalized direction if it's valid
        if (IsValidMove(enemyPos + normalizedDir, map))
            return normalizedDir;

        return Vector2I.Zero;
    }

    public static Vector2I FindClosestTarget(Vector2I enemyPos, Character[] targets)
    {
        Vector2I closestDir = Vector2I.Zero;
        float closestDistance = float.MaxValue;

        foreach (Character target in targets)
        {
            float distance = enemyPos.DistanceTo(target.location);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestDir = target.location;
            }
        }

        return closestDir;
    }

    private static bool IsValidMove(Vector2I pos, TileMapLayer map)
    {
        return map.GetCellAtlasCoords(pos) == TILE_GROUND;
    }
}
