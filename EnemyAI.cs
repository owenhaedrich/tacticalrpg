using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class EnemyAI
{
    private const int DISTANCE_CAUTIOUS = 2;
    private const int DISTANCE_FLANK = 3;

    private static readonly Dictionary<Character, bool> inDiveAttack = new Dictionary<Character, bool>();
    private static readonly Dictionary<Character, Queue<Vector2I>> positionHistory = 
        new Dictionary<Character, Queue<Vector2I>>();
    private const int POSITION_HISTORY_LENGTH = 4;

    public enum PathfindingStrategy { SmartPath, FlankPath, CirclePath, CautiousPath }

    private static class MovementUtils
    {
        public static readonly Vector2I[] Cardinals = { Vector2I.Right, Vector2I.Up, Vector2I.Left, Vector2I.Down };
        public static readonly Vector2I[] Diagonals = {
            new Vector2I(-1, 1), new Vector2I(1, 1),
            new Vector2I(-1, -1), new Vector2I(1, -1)
        };

        public static bool IsValidMove(Vector2I pos, TileMapLayer map, HashSet<Vector2I> occupied = null) =>
            map.GetCellAtlasCoords(pos) == Tiles.Ground && 
            (occupied?.Contains(pos) != true);

        public static bool IsAtDistance(Vector2I pos, Vector2I target, int distance) =>
            GetManhattanDistance(pos, target) == distance;

        public static int GetManhattanDistance(Vector2I a, Vector2I b) =>
            Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);

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

    public static EnemyAction GetAction(
        Vector2I enemyPos, 
        Character[] targets, 
        Dictionary<Vector2I, int> validTargets,
        TileMapLayer map, 
        Character enemy)
    {
        // Initialize position history for new enemies
        if (!positionHistory.ContainsKey(enemy))
            positionHistory[enemy] = new Queue<Vector2I>();

        // Add current position to history
        var history = positionHistory[enemy];
        history.Enqueue(enemyPos);
        while (history.Count > POSITION_HISTORY_LENGTH)
            history.Dequeue();

        var livingTargets = targets.Where(t => !t.isDead).ToArray();
        if (!livingTargets.Any()) return EnemyAction.None;

        if (!inDiveAttack.ContainsKey(enemy))
            inDiveAttack[enemy] = false;

        if (enemy.endurance <= 0)
            return EnemyAction.EndTurn;

        // Check each ability for valid targets
        foreach (var ability in enemy.abilities)
        {
            if (enemy.endurance >= ability.cost)
            {
                var targetsInRange = livingTargets
                    .Where(t => MovementUtils.GetManhattanDistance(enemyPos, t.location) <= ability.range)
                    .ToArray();

                if (targetsInRange.Any())
                {
                    var closestTarget = targetsInRange.MinBy(t => enemyPos.DistanceTo(t.location));
                    enemy.currentAbility = ability;
                    return new EnemyAction(Vector2I.Zero, true, closestTarget.location);
                }
            }
        }

        // Rest of movement logic remains the same
        var targetPos = livingTargets.MinBy(t => enemyPos.DistanceTo(t.location)).location;
        var occupiedPositions = livingTargets.Select(t => t.location).ToHashSet();

        // Get movement based on strategy
        return enemy.pathfinding switch
        {
            PathfindingStrategy.FlankPath or PathfindingStrategy.CirclePath => 
                new EnemyAction(GetFlankMove(enemyPos, targetPos, map, occupiedPositions, enemy)),
            PathfindingStrategy.CautiousPath => 
                new EnemyAction(GetCautiousMove(enemyPos, targetPos, map, occupiedPositions)),
            _ => GetSmartMove(enemyPos, targetPos, map, occupiedPositions)
        };
    }

    private static bool IsGoodFlankPosition(Vector2I pos, Vector2I target)
    {
        var distance = MovementUtils.GetManhattanDistance(pos, target);
        return distance >= DISTANCE_FLANK;
    }

    private static Vector2I GetFlankMove(Vector2I start, Vector2I target, TileMapLayer map, HashSet<Vector2I> occupied, Character enemy)
    {
        var currentDistance = MovementUtils.GetManhattanDistance(start, target);

        // If we're at a good flanking position, consider attacking
        if (IsGoodFlankPosition(start, target))
        {
            // Always try to move closer when beyond ideal distance
            if (currentDistance > DISTANCE_FLANK)
            {
                var moveAction = GetSmartMove(start, target, map, occupied);
                if (!moveAction.endTurn)
                {
                    inDiveAttack[enemy] = true;
                    return moveAction.moveDirection;
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
                inDiveAttack[enemy] = false;  // Reset after dive
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
                    if (!moveToFlank.endTurn)
                        return moveToFlank.moveDirection;
                }
            }
        }

        // If flanking fails, try direct approach
        return GetSmartMove(start, target, map, occupied).moveDirection;
    }

    private static Vector2I GetCautiousMove(Vector2I start, Vector2I target, TileMapLayer map, HashSet<Vector2I> occupied)
    {
        var currentDistance = MovementUtils.GetManhattanDistance(start, target);
        
        // Exactly at ideal distance
        if (currentDistance == DISTANCE_CAUTIOUS)
            return Vector2I.Zero;

        // Too close, move away
        if (currentDistance < DISTANCE_CAUTIOUS)
        {
            var awayDir = MovementUtils.DirectionTo(target, start);
            if (MovementUtils.IsValidMove(start + awayDir, map, occupied))
                return awayDir;
            
            // Try moving sideways if we can't move directly away
            var sideDirections = new[] {
                new Vector2I(-awayDir.Y, awayDir.X),
                new Vector2I(awayDir.Y, -awayDir.X)
            };

            foreach (var dir in sideDirections)
            {
                if (MovementUtils.IsValidMove(start + dir, map, occupied))
                    return dir;
            }
        }

        // Too far, move closer
        if (currentDistance > DISTANCE_CAUTIOUS)
            return GetSmartMove(start, target, map, occupied).moveDirection;

        return Vector2I.Zero;
    }

    private static bool WouldCauseOscillation(Vector2I newPos, Queue<Vector2I> history)
    {
        if (history.Count < 2) return false;

        // Check if we're moving to a position we were at recently
        return history.Contains(newPos);
    }

    private static EnemyAction GetSmartMove(Vector2I start, Vector2I target, TileMapLayer map, HashSet<Vector2I> occupied)
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
            bool isWall = map.GetCellAtlasCoords(cell) == Tiles.Wall;
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
            return EnemyAction.EndTurn;
        }

        var nextPos = new Vector2I((int)path[1].X, (int)path[1].Y);
        
        // Get the history for the current enemy
        var history = positionHistory.Values.LastOrDefault() ?? new Queue<Vector2I>();
        
        if (MovementUtils.IsValidMove(nextPos, map, occupied) && !WouldCauseOscillation(nextPos, history))
        {
            return new EnemyAction(MovementUtils.DirectionTo(start, nextPos));
        }

        // If direct path would cause oscillation or is blocked, try alternative directions
        foreach (var dir in MovementUtils.Cardinals
            .OrderBy(d => (start + d).DistanceTo(target)))
        {
            var altPos = start + dir;
            if (MovementUtils.IsValidMove(altPos, map, occupied) && !WouldCauseOscillation(altPos, history))
            {
                return new EnemyAction(dir);
            }
        }

        // If all moves would cause oscillation, end the turn
        return EnemyAction.EndTurn;
    }
}
