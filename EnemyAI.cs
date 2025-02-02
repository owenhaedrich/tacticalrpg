using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class EnemyAI
{
    private static readonly Vector2I TILE_GROUND = new Vector2I(1, 0);
    private const float CAUTIOUS_DISTANCE = 2f;

    public enum PathfindingStrategy { SmartPath, FlankPath, CirclePath, CautiousPath }

    private static class MovementUtils
    {
        public static readonly Vector2I[] Cardinals = { Vector2I.Right, Vector2I.Up, Vector2I.Left, Vector2I.Down };

        public static Vector2I[] GetDirections(Vector2I from, Vector2I to, bool includeCardinals = true)
        {
            var toTarget = (to - from).Sign();
            var dirs = new List<Vector2I> { 
                toTarget, 
                new(-toTarget.Y, toTarget.X), // clockwise
                new(toTarget.Y, -toTarget.X)  // counter-clockwise
            };
            if (includeCardinals) dirs.AddRange(Cardinals);
            return dirs.Distinct().ToArray();
        }

        public static bool IsValidMove(Vector2I pos, TileMapLayer map, HashSet<Vector2I> occupied = null) =>
            map.GetCellAtlasCoords(pos) == TILE_GROUND && 
            (occupied?.Contains(pos) != true);
    }

    public struct EnemyAction
    {
        public Vector2I moveDirection;
        public bool useAbility;
        public Vector2I targetPosition;

        public EnemyAction(Vector2I move, bool ability = false, Vector2I target = default) =>
            (moveDirection, useAbility, targetPosition) = (move, ability, target);
    }

    public static EnemyAction GetAction(Vector2I enemyPos, Character[] targets, int abilityRange, TileMapLayer map, Character enemy)
    {
        var livingTargets = targets.Where(t => !t.isDead).ToArray();
        if (livingTargets.Length == 0) return new EnemyAction(Vector2I.Zero);

        var targetPos = livingTargets.MinBy(t => enemyPos.DistanceTo(t.location)).location;
        var occupiedPositions = targets.Select(t => t.location).ToHashSet();

        // Check for ability use
        var targetInRange = livingTargets.FirstOrDefault(t => enemyPos.DistanceTo(t.location) <= abilityRange);
        if (targetInRange != null) return new EnemyAction(Vector2I.Zero, true, targetInRange.location);

        // Get movement direction
        Vector2I moveDir = enemy.pathfinding switch
        {
            PathfindingStrategy.FlankPath => GetFlankMove(enemyPos, targetPos, map, occupiedPositions),
            PathfindingStrategy.CirclePath => GetCircularMove(enemyPos, targetPos, map, occupiedPositions),
            PathfindingStrategy.CautiousPath => GetCautiousMove(enemyPos, targetPos, map, occupiedPositions),
            _ => GetSmartMove(enemyPos, targetPos, map, occupiedPositions)
        };

        return new EnemyAction(moveDir);
    }

    private static Vector2I GetFlankMove(Vector2I start, Vector2I target, TileMapLayer map, HashSet<Vector2I> occupied)
    {
        // Try to get behind target
        var behindTarget = target + (target - start);
        
        if (!MovementUtils.IsValidMove(behindTarget, map, occupied))
        {
            // Try multiple side positions at different distances
            var baseDirection = (target - start).Sign();
            var sideDirections = new[] {
                new Vector2I(-baseDirection.Y, baseDirection.X),  // right side
                new Vector2I(baseDirection.Y, -baseDirection.X)   // left side
            };

            foreach (var side in sideDirections)
            {
                // Try positions at increasing distances from the target
                for (int distance = 1; distance <= 3; distance++)
                {
                    var sidePos = target + (side * distance);
                    if (MovementUtils.IsValidMove(sidePos, map, occupied))
                        return GetSmartMove(start, sidePos, map, occupied, true);
                }
            }
        }

        return GetSmartMove(start, target, map, occupied, true);
    }

    private static Vector2I GetCircularMove(Vector2I start, Vector2I target, TileMapLayer map, HashSet<Vector2I> occupied)
    {
        var distanceToTarget = start.DistanceTo(target);
        
        // Try to maintain a circular pattern around the target
        var clockwiseDir = new Vector2I(-(target - start).Y, (target - start).X).Sign();
        var counterClockwiseDir = new Vector2I((target - start).Y, -(target - start).X).Sign();
        
        // If too far, try to move closer while circling
        if (distanceToTarget > 2)
        {
            var inwardDir = (target - start).Sign();
            var possibleMoves = new[] 
            {
                start + clockwiseDir + inwardDir,
                start + counterClockwiseDir + inwardDir,
                start + inwardDir
            };

            foreach (var pos in possibleMoves)
            {
                if (MovementUtils.IsValidMove(pos, map, occupied))
                {
                    return (pos - start).Sign();
                }
            }
        }
        
        // Try circular movement
        if (MovementUtils.IsValidMove(start + clockwiseDir, map, occupied))
        {
            return clockwiseDir;
        }
        if (MovementUtils.IsValidMove(start + counterClockwiseDir, map, occupied))
        {
            return counterClockwiseDir;
        }

        // Fallback to smart path if circular movement is blocked
        return GetSmartMove(start, target, map, occupied);
    }

    private static Vector2I GetCautiousMove(Vector2I start, Vector2I target, TileMapLayer map, HashSet<Vector2I> occupied)
    {
        var currentDistance = start.DistanceTo(target);
        
        // If we're at ideal distance, stay put
        if (Mathf.Abs(currentDistance - CAUTIOUS_DISTANCE) < 0.1f)
        {
            return Vector2I.Zero;
        }

        // If we're too close, move away
        if (currentDistance < CAUTIOUS_DISTANCE)
        {
            var awayDir = (start - target).Sign();
            if (MovementUtils.IsValidMove(start + awayDir, map, occupied))
            {
                return awayDir;
            }
            
            // Try moving sideways if we can't move directly away
            var sideDirections = new[] {
                new Vector2I(-awayDir.Y, awayDir.X),
                new Vector2I(awayDir.Y, -awayDir.X)
            };

            foreach (var dir in sideDirections)
            {
                if (MovementUtils.IsValidMove(start + dir, map, occupied))
                {
                    return dir;
                }
            }
        }

        // If we're too far, use smart pathing to get closer
        if (currentDistance > CAUTIOUS_DISTANCE)
        {
            return GetSmartMove(start, target, map, occupied);
        }

        return Vector2I.Zero;
    }

    private static Vector2I GetSmartMove(Vector2I start, Vector2I target, TileMapLayer map, HashSet<Vector2I> occupied, bool allowDirectFallback = true)
    {
        var astar = new AStarGrid2D();
        astar.Region = map.GetUsedRect();
        astar.CellSize = Vector2.One;
        astar.DiagonalMode = AStarGrid2D.DiagonalModeEnum.Never;
        astar.Update();

        // First, mark all cells as passable
        foreach (var cell in map.GetUsedCells())
        {
            astar.SetPointSolid(cell, false);
        }

        // Then mark walls and occupied spaces
        foreach (var cell in map.GetUsedCells())
        {
            bool isWall = map.GetCellAtlasCoords(cell) != TILE_GROUND;
            bool isBlocked = occupied.Contains(cell) && cell != target && cell != start;
            
            if (isWall || isBlocked)
            {
                astar.SetPointSolid(cell, true);
            }
        }

        // Ensure start and target are not marked as solid
        astar.SetPointSolid(start, false);
        astar.SetPointSolid(target, false);

        var path = astar.GetPointPath(start, target);
        
        // Debug path finding
        if (path == null || path.Length < 2)
        {
            GD.Print($"No path found from {start} to {target}");
            return Vector2I.Zero;
        }

        var nextPos = new Vector2I((int)path[1].X, (int)path[1].Y);
        if (MovementUtils.IsValidMove(nextPos, map, occupied))
        {
            return (nextPos - start).Sign();
        }

        // If direct path is blocked, try to move closer
        foreach (var dir in MovementUtils.Cardinals
            .OrderBy(d => (start + d).DistanceTo(target)))
        {
            var altPos = start + dir;
            if (MovementUtils.IsValidMove(altPos, map, occupied))
            {
                return dir;
            }
        }

        return Vector2I.Zero;
    }
}
