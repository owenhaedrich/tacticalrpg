using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class EnemyAI
{
    private const int DISTANCE_CAUTIOUS = 2;
    private const int DISTANCE_FLANK = 3;
    private const int POSITION_HISTORY_LENGTH = 4;

    private static readonly Dictionary<Character, bool> inDiveAttack = new Dictionary<Character, bool>();
    private static readonly Dictionary<Character, Queue<Vector2I>> positionHistory = 
        new Dictionary<Character, Queue<Vector2I>>();

    // Add a dictionary to track visited positions during the current turn
    private static readonly Dictionary<Character, HashSet<Vector2I>> visitedPositions = new Dictionary<Character, HashSet<Vector2I>>();

    public enum PathfindingStrategy { SmartPath, FlankPath, CirclePath, CautiousPath }

    public readonly struct EnemyAction
    {
        public Vector2I movePosition { get; }  // Changed from moveDirection
        public bool useAbility { get; }
        public Vector2I targetPosition { get; }
        public bool endTurn { get; }

        public static readonly EnemyAction None = new(Vector2I.Zero);
        public static readonly EnemyAction EndTurn = new(Vector2I.Zero, end: true);

        public EnemyAction(Vector2I move, bool ability = false, Vector2I target = default, bool end = false) =>
            (movePosition, useAbility, targetPosition, endTurn) = (move, ability, target, end);
    }

    public static EnemyAction GetAction(
        Vector2I enemyPos, 
        Character[] targets, 
        Dictionary<Vector2I, int> validTargets,
        TileMapLayer map, 
        Character enemy)
    {
        if (!positionHistory.ContainsKey(enemy))
            positionHistory[enemy] = new Queue<Vector2I>();

        var history = positionHistory[enemy];
        history.Enqueue(enemyPos);
        while (history.Count > POSITION_HISTORY_LENGTH)
            history.Dequeue();

        if (!visitedPositions.ContainsKey(enemy))
            visitedPositions[enemy] = new HashSet<Vector2I>();
        else
            visitedPositions[enemy].Clear(); // Clear visited positions at the start of each turn

        var livingTargets = targets.Where(t => !t.isDead).ToArray();
        if (!livingTargets.Any() || enemy.endurance <= 0) 
            return EnemyAction.EndTurn;

        // Try abilities first
        if (TryUseAbility(enemyPos, livingTargets, enemy, out var abilityAction))
            return abilityAction;

        // Movement logic
        var targetPos = livingTargets.MinBy(t => enemyPos.DistanceTo(t.location)).location;
        return GetMovementAction(enemyPos, targetPos, map, enemy, history);
    }

    private static bool TryUseAbility(Vector2I pos, Character[] targets, Character user, out EnemyAction action)
    {
        action = EnemyAction.None;
        foreach (var ability in user.abilities)
        {
            if (user.endurance >= ability.cost)
            {
                var inRange = targets.Where(t => MovementUtils.GetManhattanDistance(pos, t.location) <= ability.range);
                if (inRange.Any())
                {
                    var target = inRange.MinBy(t => pos.DistanceTo(t.location));
                    user.currentAbility = ability;
                    action = new EnemyAction(Vector2I.Zero, true, target.location);
                    return true;
                }
            }
        }
        return false;
    }

    private static EnemyAction GetMovementAction(Vector2I start, Vector2I target, TileMapLayer map, 
        Character enemy, Queue<Vector2I> history)
    {
        // Try to jump first
        if (TryJump(start, target, map, history, out Vector2I jumpPosition))
        {
            if (!WouldCauseOscillation(jumpPosition, history))
                return new EnemyAction(jumpPosition);
        }

        Vector2I move = enemy.pathfinding switch
        {
            PathfindingStrategy.FlankPath or PathfindingStrategy.CirclePath => 
                GetFlankMove(start, target, map, enemy, history),
            PathfindingStrategy.CautiousPath => 
                GetCautiousMove(start, target, map, history),
            _ => GetSmartMove(start, target, map, history)
        };

        if (move != Vector2I.Zero && !WouldCauseOscillation(move, history) && MovementUtils.IsValidMove(move, map))
        {
            return new EnemyAction(move);
        }

        return EnemyAction.EndTurn;
    }

    private static bool IsGoodFlankPosition(Vector2I pos, Vector2I target)
    {
        var distance = MovementUtils.GetManhattanDistance(pos, target);
        return distance >= DISTANCE_FLANK;
    }

    private static Vector2I GetFlankMove(Vector2I start, Vector2I target, TileMapLayer map, Character enemy, Queue<Vector2I> history)
    {
        if (!inDiveAttack.ContainsKey(enemy))
            inDiveAttack[enemy] = false;

        var currentDistance = MovementUtils.GetManhattanDistance(start, target);
        var visitedSet = visitedPositions[enemy];

        // If we're at a good flanking position, handle attack sequence
        if (IsGoodFlankPosition(start, target))
        {
            if (currentDistance > DISTANCE_FLANK)
            {
                var moveTowards = GetSmartMove(start, target, map, history);
                // Validate movement cost
                if (!visitedSet.Contains(moveTowards) && 
                    MovementUtils.HasEnoughEndurance(MovementUtils.GetManhattanDistance(start, moveTowards)))
                {
                    visitedSet.Add(moveTowards);
                    return moveTowards;
                }
            }
            else if (!inDiveAttack[enemy])
            {
                inDiveAttack[enemy] = true;
                var diveMove = start + MovementUtils.DirectionTo(start, target);
                // Validate movement cost
                if (MovementUtils.IsValidMove(diveMove, map) && 
                    !visitedSet.Contains(diveMove) && 
                    MovementUtils.HasEnoughEndurance(MovementUtils.GetManhattanDistance(start, diveMove)))
                {
                    visitedSet.Add(diveMove);
                    return diveMove;
                }
            }
            inDiveAttack[enemy] = false;
            return Vector2I.Zero;
        }

        inDiveAttack[enemy] = false;

        // Calculate potential flanking directions
        var baseDirection = MovementUtils.DirectionTo(start, target);
        var sideDirections = new[] {
            new Vector2I(-baseDirection.Y, baseDirection.X),
            new Vector2I(baseDirection.Y, -baseDirection.X)
        };

        // Try multiple flanking positions with randomization
        var random = new Random();
        for (int distance = 3; distance >= 1; distance--)
        {
            foreach (var side in sideDirections.OrderBy(x => random.Next()))
            {
                var flankPos = target + (side * distance);
                if (MovementUtils.IsValidMove(flankPos, map))
                {
                    var moveToFlank = GetSmartMove(start, flankPos, map, history);
                    // Validate movement cost
                    if (moveToFlank != Vector2I.Zero && 
                        !visitedSet.Contains(moveToFlank) && 
                        MovementUtils.HasEnoughEndurance(MovementUtils.GetManhattanDistance(start, moveToFlank)))
                    {
                        visitedSet.Add(moveToFlank);
                        return moveToFlank;
                    }
                }
            }
        }

        // Fallback to direct approach
        var directMove = GetSmartMove(start, target, map, history);
        // Validate movement cost
        if (!visitedSet.Contains(directMove) && 
            MovementUtils.HasEnoughEndurance(MovementUtils.GetManhattanDistance(start, directMove)))
        {
            visitedSet.Add(directMove);
            return directMove;
        }

        return Vector2I.Zero;
    }

    private static Vector2I GetCautiousMove(Vector2I start, Vector2I target, TileMapLayer map, Queue<Vector2I> history)
    {
        var currentDistance = MovementUtils.GetManhattanDistance(start, target);

        // Exactly at ideal distance
        if (currentDistance == DISTANCE_CAUTIOUS)
            return Vector2I.Zero;

        // Too close, move away
        if (currentDistance < DISTANCE_CAUTIOUS)
        {
            var awayDir = MovementUtils.DirectionTo(target, start);
            Vector2I newPos = start + awayDir;
            if (MovementUtils.IsValidMove(newPos, map))
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
                newPos = start + dir;
                if (MovementUtils.IsValidMove(newPos, map))
                {
                    return dir;
                }
            }
            return Vector2I.Zero; // If we can't move without looping, end the turn.
        }

        // Too far, move closer
        if (currentDistance > DISTANCE_CAUTIOUS)
        {
            return GetSmartMove(start, target, map, history);
        }

        return Vector2I.Zero;
    }

    private static bool WouldCauseOscillation(Vector2I newPos, Queue<Vector2I> history)
    {
        if (history.Count < 2) return false;

        // Check if we're moving to a position we were at recently
        return history.Contains(newPos);
    }

    private static bool TryJump(Vector2I start, Vector2I target, TileMapLayer map, Queue<Vector2I> history, out Vector2I jumpPosition)
    {
        jumpPosition = Vector2I.Zero;

        // Movement cost for a jump would be 2 (since we move 2 squares)
        // Let's make sure we have enough endurance first
        if (!MovementUtils.HasEnoughEndurance(2))
            return false;

        var occupied = MovementUtils.UpdateOccupiedPositions();
        foreach (var dir in MovementUtils.Cardinals)
        {
            var blocked = start + dir;
            var landing = blocked + dir;

            if (occupied.Contains(blocked) && MovementUtils.IsValidMove(landing, map))
            {
                var currentDistance = MovementUtils.GetManhattanDistance(start, target);
                var newDistance = MovementUtils.GetManhattanDistance(landing, target);

                if (newDistance < currentDistance && !WouldCauseOscillation(landing, history))
                {
                    GD.Print($"Jumping from {start} to {landing}");
                    jumpPosition = landing;
                    return true;
                }
            }
        }
        return false;
    }

    private static Vector2I GetSmartMove(Vector2I start, Vector2I target, TileMapLayer map, Queue<Vector2I> history)
    {
        // First attempt: Ignore occupied spaces
        var path = CalculatePath(start, target, map, includeOccupied: false);
        
        // If no valid path or next step is blocked, try including occupied spaces
        if (path == null || path.Length < 2 || 
            !MovementUtils.IsValidMove(new Vector2I((int)path[1].X, (int)path[1].Y), map))
        {
            path = CalculatePath(start, target, map, includeOccupied: true);
        }

        if (path == null || path.Length < 2)
        {
            GD.Print($"No path found from {start} to {target}");
            return Vector2I.Zero;
        }

        return new Vector2I((int)path[1].X, (int)path[1].Y);
    }

    private static Vector2[] CalculatePath(Vector2I start, Vector2I target, TileMapLayer map, bool includeOccupied)
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

        // Mark walls
        foreach (var cell in map.GetUsedCells())
        {
            bool isWall = map.GetCellAtlasCoords(cell) == Tiles.Wall;
            if (isWall)
                astar.SetPointSolid(cell, true);
        }

        // If requested, mark occupied spaces as solid
        if (includeOccupied)
        {
            var occupied = MovementUtils.UpdateOccupiedPositions();
            foreach (var pos in occupied)
            {
                if (pos != start && pos != target)
                    astar.SetPointSolid(pos, true);
            }
        }
        
        // Ensure start and target are not marked as solid
        astar.SetPointSolid(start, false);
        astar.SetPointSolid(target, false);

        var path = astar.GetPointPath(start, target);
        
        // Validate that the first step is only one square away
        if (path != null && path.Length >= 2)
        {
            var firstStep = new Vector2I((int)path[1].X, (int)path[1].Y);
            if (MovementUtils.GetManhattanDistance(start, firstStep) > 1)
                return null;
        }
        
        return path;
    }

}
