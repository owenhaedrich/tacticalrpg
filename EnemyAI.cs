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

    public static EnemyAction GetAction(Vector2I enemyPos, Character[] targets, int abilityRange, TileMapLayer map)
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
        
        // If no target in range, move towards closest target
        Vector2I moveDir = GetMoveDirection(enemyPos, targetPos, map, occupiedPositions);
        return new EnemyAction(moveDir, false, Vector2I.Zero);
    }

    private static bool IsInAbilityRange(Vector2I source, Vector2I target, int range)
    {
        return source.DistanceTo(target) <= range;
    }

    public static Vector2I GetMoveDirection(Vector2I enemyPos, Vector2I targetPos, TileMapLayer map, HashSet<Vector2I> occupiedPositions)
    {
        return FindPath(enemyPos, targetPos, map, PathfindingStrategy.SmartPath, occupiedPositions);
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
        
        GD.Print($"=== Path Finding Start ===");
        GD.Print($"From: {start} To: {target}");
        
        if (usedCells.Count == 0)
        {
            GD.Print("No used cells found in map!");
            return Vector2I.Zero;
        }

        var region = map.GetUsedRect();
        astarGrid.Region = region;
        astarGrid.CellSize = Vector2.One;
        astarGrid.DiagonalMode = AStarGrid2D.DiagonalModeEnum.Never;
        astarGrid.DefaultComputeHeuristic = AStarGrid2D.Heuristic.Manhattan;
        astarGrid.Update();

        // Initialize all cells as solid first
        for (int x = 0; x < region.Size.X; x++)
        {
            for (int y = 0; y < region.Size.Y; y++)
            {
                astarGrid.SetPointSolid(new Vector2I(x, y), true);
            }
        }

        // Mark walkable paths, considering occupied positions
        int walkableCells = 0;
        foreach (var cell in usedCells)
        {
            if (IsValidMove(cell, map))
            {
                // Allow pathfinding through occupied cells, but not stopping on them
                bool isOccupied = occupiedPositions.Contains(cell) && cell != target;
                astarGrid.SetPointSolid(cell, isOccupied);
                if (!isOccupied) walkableCells++;
            }
        }

        GD.Print($"Grid size: {region.Size}");
        GD.Print($"Used cells count: {usedCells.Count}");
        GD.Print($"Walkable cells: {walkableCells}");

        // Validate and ensure endpoints are walkable
        if (!region.HasPoint(start) || !region.HasPoint(target))
        {
            GD.Print($"Position out of region bounds! Region: {region}, Start: {start}, Target: {target}");
            return DumbPath(start, target, map);
        }

        // Force endpoints to be walkable and check surrounding cells
        astarGrid.SetPointSolid(start, false);
        astarGrid.SetPointSolid(target, false);

        // Debug path possibilities
        var startConnected = HasAdjacentWalkableCell(start, astarGrid);
        var targetConnected = HasAdjacentWalkableCell(target, astarGrid);
        
        GD.Print($"Start has adjacent walkable cells: {startConnected}");
        GD.Print($"Target has adjacent walkable cells: {targetConnected}");

        if (!startConnected || !targetConnected)
        {
            GD.Print("Start or target is isolated!");
            return DumbPath(start, target, map);
        }

        // Try to find path
        var path = astarGrid.GetPointPath(start, target);
        
        if (path == null || path.Length < 2)
        {
            GD.Print("No direct path found, trying alternatives...");
            
            // Try to find closest reachable point near target
            Vector2I bestAlternative = Vector2I.Zero;
            float bestDistance = float.MaxValue;
            
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    var altTarget = target + new Vector2I(dx, dy);
                    if (region.HasPoint(altTarget) && !astarGrid.IsPointSolid(altTarget))
                    {
                        var altPath = astarGrid.GetPointPath(start, altTarget);
                        if (altPath != null && altPath.Length >= 2)
                        {
                            float dist = target.DistanceTo(altTarget);
                            if (dist < bestDistance)
                            {
                                bestDistance = dist;
                                bestAlternative = altTarget;
                                path = altPath;
                            }
                        }
                    }
                }
            }
            
            if (path == null || path.Length < 2)
            {
                GD.Print("No alternative paths found");
                return DumbPath(start, target, map);
            }
            
            GD.Print($"Found alternative path via {bestAlternative}");
        }

        var nextStep = new Vector2I((int)path[1].X, (int)path[1].Y);
        var direction = (nextStep - start).Sign();
        GD.Print($"Path found! Next step: {nextStep}, Direction: {direction}");
        GD.Print($"=== Path Finding End ===");
        return direction;
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
