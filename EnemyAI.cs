using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class EnemyAI
{
    private static readonly Vector2I TILE_GROUND = new Vector2I(1, 0);
    private const float DISTANCE_CAUTIOUS = 2f;
    private const float DISTANCE_FLANK = 2f;
    private const float DISTANCE_TOLERANCE = 0.1f;

    private static readonly Dictionary<Character, bool> inDiveAttack = new Dictionary<Character, bool>();

    public enum PathfindingStrategy { SmartPath, FlankPath, CirclePath, CautiousPath }

    private static class MovementUtils
    {
        public static readonly Vector2I[] Cardinals = { Vector2I.Right, Vector2I.Up, Vector2I.Left, Vector2I.Down };
        public static readonly Vector2I[] Diagonals = {
            new Vector2I(-1, 1), new Vector2I(1, 1),
            new Vector2I(-1, -1), new Vector2I(1, -1)
        };

        public static bool IsValidMove(Vector2I pos, TileMapLayer map, HashSet<Vector2I> occupied = null) =>
            map.GetCellAtlasCoords(pos) == TILE_GROUND && 
            (occupied?.Contains(pos) != true);

        public static bool IsAtDistance(Vector2I pos, Vector2I target, float distance) =>
            Mathf.Abs(pos.DistanceTo(target) - distance) < DISTANCE_TOLERANCE;

        public static Vector2I DirectionTo(Vector2I from, Vector2I to) =>
            (to - from).Sign();
    }

    public readonly struct EnemyAction
    {
        public Vector2I moveDirection { get; }
        public bool useAbility { get; }
        public Vector2I targetPosition { get; }
        public bool endTurn { get; }

        public static readonly EnemyAction None = new(Vector2I.Zero);
        public static readonly EnemyAction EndTurn = new(Vector2I.Zero, end: true);

        public EnemyAction(Vector2I move, bool ability = false, Vector2I target = default, bool end = false) =>
            (moveDirection, useAbility, targetPosition, endTurn) = (move, ability, target, end);
    }

    public static EnemyAction GetAction(Vector2I enemyPos, Character[] targets, int abilityRange, TileMapLayer map, Character enemy)
    {
        var livingTargets = targets.Where(t => !t.isDead).ToArray();
        if (!livingTargets.Any()) return EnemyAction.None;

        if (!inDiveAttack.ContainsKey(enemy))
            inDiveAttack[enemy] = false;

        if (enemy.endurance <= 0)
            return EnemyAction.EndTurn;

        var targetPos = livingTargets.MinBy(t => enemyPos.DistanceTo(t.location)).location;
        var occupiedPositions = livingTargets.Select(t => t.location).ToHashSet();

        // Try to use ability if target in range and we have enough endurance
        var targetInRange = livingTargets.FirstOrDefault(t => enemyPos.DistanceTo(t.location) <= abilityRange);
        if (targetInRange != null)
        {
            if (enemy.endurance >= enemy.currentAbility.cost)
                return new EnemyAction(Vector2I.Zero, true, targetInRange.location);

            // Retreat if we can't use ability
            var retreatDir = MovementUtils.DirectionTo(targetInRange.location, enemyPos);
            if (MovementUtils.IsValidMove(enemyPos + retreatDir, map, occupiedPositions))
                return new EnemyAction(retreatDir);
        }

        // Get movement based on strategy
        Vector2I moveDir = enemy.pathfinding switch
        {
            PathfindingStrategy.FlankPath or PathfindingStrategy.CirclePath => 
                GetFlankMove(enemyPos, targetPos, map, occupiedPositions, enemy),
            PathfindingStrategy.CautiousPath => 
                GetCautiousMove(enemyPos, targetPos, map, occupiedPositions),
            _ => GetSmartMove(enemyPos, targetPos, map, occupiedPositions)
        };

        return moveDir == Vector2I.Zero && inDiveAttack[enemy] 
            ? EnemyAction.EndTurn 
            : new EnemyAction(moveDir);
    }

    private static bool IsGoodFlankPosition(Vector2I pos, Vector2I target)
    {
        var distance = pos.DistanceTo(target);
        // Consider any position at or beyond ideal distance as a good flank position
        return distance >= DISTANCE_FLANK;
    }

    private static Vector2I GetFlankMove(Vector2I start, Vector2I target, TileMapLayer map, HashSet<Vector2I> occupied, Character enemy)
    {
        var currentDistance = start.DistanceTo(target);

        // If we're at a good flanking position, consider attacking
        if (IsGoodFlankPosition(start, target))
        {
            // Always try to move closer when beyond ideal distance
            if (currentDistance > DISTANCE_FLANK)
            {
                var moveToTarget = GetSmartMove(start, target, map, occupied);
                if (moveToTarget != Vector2I.Zero)
                {
                    inDiveAttack[enemy] = true;
                    return moveToTarget;
                }
            }
            // At ideal distance, start dive attack sequence
            else if (!inDiveAttack[enemy])
            {
                inDiveAttack[enemy] = true;
                return MovementUtils.DirectionTo(start, target);
            }
            else
            {
                return Vector2I.Zero;  // Signal retreat
            }
        }

        // Reset dive attack state if we're not in position
        inDiveAttack[enemy] = false;

        var baseDirection = MovementUtils.DirectionTo(start, target);
        var sideDirections = new[] {
            new Vector2I(-baseDirection.Y, baseDirection.X),  // right side
            new Vector2I(baseDirection.Y, -baseDirection.X)   // left side
        };

        // Try flanking positions at different distances
        for (int distance = 2; distance >= 1; distance--)
        {
            foreach (var side in sideDirections)
            {
                var flankPos = target + (side * distance);
                if (MovementUtils.IsValidMove(flankPos, map, occupied))
                {
                    var moveToFlank = GetSmartMove(start, flankPos, map, occupied);
                    if (moveToFlank != Vector2I.Zero)
                        return moveToFlank;
                }
            }
        }

        // If flanking fails, try direct approach
        return GetSmartMove(start, target, map, occupied);
    }

    private static Vector2I GetCautiousMove(Vector2I start, Vector2I target, TileMapLayer map, HashSet<Vector2I> occupied)
    {
        var currentDistance = start.DistanceTo(target);
        
        // If we're at ideal distance, stay put
        if (MovementUtils.IsAtDistance(start, target, DISTANCE_CAUTIOUS))
        {
            return Vector2I.Zero;
        }

        // If we're too close, move away
        if (currentDistance < DISTANCE_CAUTIOUS)
        {
            var awayDir = MovementUtils.DirectionTo(target, start);
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
        if (currentDistance > DISTANCE_CAUTIOUS)
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
            return MovementUtils.DirectionTo(start, nextPos);
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
