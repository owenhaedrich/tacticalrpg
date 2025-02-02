using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class EnemyAI
{
    private static readonly Vector2I TILE_GROUND = new Vector2I(1, 0);

    public enum PathfindingStrategy
    {
        DumbPath,
        SmartPath
    }

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

    public static EnemyAction GetAction(Vector2I enemyPos, Character[] targets, int abilityRange, TileMapLayer map, Character enemy)
    {
        // Filter out dead targets
        var livingTargets = targets.Where(t => !t.isDead).ToArray();
        if (livingTargets.Length == 0) return new EnemyAction(Vector2I.Zero, false, Vector2I.Zero);

        Vector2I targetPos = FindClosestTarget(enemyPos, livingTargets);
        
        // Check if any target is in ability range
        foreach (Character target in livingTargets)
        {
            if (IsInAbilityRange(enemyPos, target.location, abilityRange))
            {
                return new EnemyAction(Vector2I.Zero, true, target.location);
            }
        }

        // Get locations of all characters to consider for pathfinding
        var occupiedPositions = targets.Select(t => t.location).ToHashSet();
        
        // If no target in range, move towards closest target using enemy's pathfinding strategy
        Vector2I moveDir = GetMoveDirection(enemyPos, targetPos, map, occupiedPositions, enemy.pathfinding);
        return new EnemyAction(moveDir, false, Vector2I.Zero);
    }

    private static bool IsInAbilityRange(Vector2I source, Vector2I target, int range)
    {
        return source.DistanceTo(target) <= range;
    }

    public static Vector2I GetMoveDirection(Vector2I enemyPos, Vector2I targetPos, TileMapLayer map, HashSet<Vector2I> occupiedPositions, PathfindingStrategy strategy)
    {
        return FindPath(enemyPos, targetPos, map, strategy, occupiedPositions);
    }

    private static Vector2I FindPath(Vector2I start, Vector2I target, TileMapLayer map, PathfindingStrategy strategy, HashSet<Vector2I> occupiedPositions)
    {
        return strategy switch
        {
            PathfindingStrategy.DumbPath => DumbPath(start, target, map),
            _ => SmartPath(start, target, map, occupiedPositions) 
        };
    }

    private static Vector2I DumbPath(Vector2I start, Vector2I target, TileMapLayer map)
    {
        Vector2I direction = target - start;
        
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

            if (IsValidMove(start + xMove, map))
                return xMove;
            if (IsValidMove(start + yMove, map))
                return yMove;
        }

        // Try the normalized direction if it's valid
        if (IsValidMove(start + normalizedDir, map))
            return normalizedDir;

        return Vector2I.Zero;
    }

    private static Vector2I SmartPath(Vector2I start, Vector2I target, TileMapLayer map, HashSet<Vector2I> occupiedPositions)
    {
        var astarGrid = new AStarGrid2D();
        var usedCells = map.GetUsedCells();
        
        if (usedCells.Count == 0)
        {
            GD.Print("No used cells found in map!");
            return Vector2I.Zero;
        }

        // Calculate the actual region bounds based on used cells
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (var cell in usedCells)
        {
            minX = Math.Min(minX, cell.X);
            minY = Math.Min(minY, cell.Y);
            maxX = Math.Max(maxX, cell.X);
            maxY = Math.Max(maxY, cell.Y);
        }

        // Add padding to ensure start and target are within bounds
        minX = Math.Min(minX, Math.Min(start.X, target.X));
        minY = Math.Min(minY, Math.Min(start.Y, target.Y));
        maxX = Math.Max(maxX, Math.Max(start.X, target.X));
        maxY = Math.Max(maxY, Math.Max(start.Y, target.Y));

        // Create region with the calculated bounds
        var region = new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1);
        astarGrid.Region = region;
        astarGrid.CellSize = Vector2.One;
        astarGrid.DiagonalMode = AStarGrid2D.DiagonalModeEnum.Never;
        astarGrid.DefaultComputeHeuristic = AStarGrid2D.Heuristic.Manhattan;
        astarGrid.Update();

        // Initialize all cells as solid
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                var pos = new Vector2I(x, y);
                if (region.HasPoint(pos))
                {
                    astarGrid.SetPointSolid(pos, true);
                }
            }
        }

        // Mark walkable paths
        foreach (var cell in usedCells)
        {
            if (region.HasPoint(cell) && IsValidMove(cell, map))
            {
                bool isOccupiedByLiving = occupiedPositions.Contains(cell) && cell != target && cell != start;
                astarGrid.SetPointSolid(cell, isOccupiedByLiving);
            }
        }

        // Ensure start and target are walkable
        if (!region.HasPoint(start) || !region.HasPoint(target))
        {
            return DumbPath(start, target, map);
        }

        astarGrid.SetPointSolid(start, false);
        astarGrid.SetPointSolid(target, false);

        var path = astarGrid.GetPointPath(start, target);
        
        if (path == null || path.Length < 2)
        {
            return DumbPath(start, target, map);
        }

        var nextStep = new Vector2I((int)path[1].X, (int)path[1].Y);
        return (nextStep - start).Sign();
    }

    private static bool HasAdjacentWalkableCell(Vector2I pos, AStarGrid2D grid)
    {
        var adjacentOffsets = new Vector2I[]
        {
            new Vector2I(-1, 0),
            new Vector2I(1, 0),
            new Vector2I(0, -1),
            new Vector2I(0, 1)
        };

        foreach (var offset in adjacentOffsets)
        {
            var adjacent = pos + offset;
            if (grid.IsInBoundsv(adjacent) && !grid.IsPointSolid(adjacent))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsValidMove(Vector2I pos, TileMapLayer map)
    {
        if (!map.GetUsedRect().HasPoint(pos)) return false;
        var atlas = map.GetCellAtlasCoords(pos);
        return atlas == TILE_GROUND;
    }

    public static Vector2I FindClosestTarget(Vector2I enemyPos, Character[] targets)
    {
        if (targets.Length == 0) return Vector2I.Zero;
        
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
}
