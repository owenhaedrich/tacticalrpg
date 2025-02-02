using Godot;
using System.Linq;
using System.Collections.Generic;

public partial class TurnManager : TileMapLayer // TileMapLayer is a custom class that extends TileMap
{
    #region Constants and Types
    // Atlas coordinates
    private static class Tiles
    {
        public static readonly Vector2I Ground = new(1, 0);
        public static readonly Vector2I Player = new(0, 0);
        public static readonly Vector2I Movement = new(1, 1);
        public static readonly Vector2I Enemy = new(1, 2);
		public static readonly Vector2I Dead = new(2, 1);
        public static readonly Vector2I Wall = new(0, 1);
        public static readonly Vector2I AbilityTarget = new(0, 2);
    }

    // State enums
    private enum TurnState { PLAYER, ENEMY }
    private enum ActionState { MOVE, ABILITY }
    #endregion

    #region State Variables
    // Core state
    private List<Vector4I> map = new();
    private TurnState turnState = TurnState.PLAYER;
    private ActionState currentAction = ActionState.MOVE;
    private int activePartyMember = 0;
    private int activeEnemy = 0;
    private Dictionary<Vector2I, int> validMoves = new();
    private Dictionary<Vector2I, int> validTargets = new();

    // Characters
    private Character[] party = {
        Character.Domli(new Vector2I(3, 3)),
        Character.Zash(new Vector2I(5, 5))
    };
    private Character[] enemies = {
        Character.Dog(new Vector2I(15, 11)),
        Character.Dog(new Vector2I(17, 11))
    };

    // Computed property for all characters
    private Character[] allCharacters => party.Concat(enemies).ToArray();

    private float enemyActionTimer = 0f;
    private const float ENEMY_ACTION_DELAY = 0.1f; // Delay between enemy actions
    #endregion
 
    #region Core Game Loop
    public override void _Ready()
    {
        LoadMap();
        UpdateMap();
        FindTargets(party[activePartyMember].location);
    }

    public override void _Input(InputEvent @event)
    {
        if (turnState == TurnState.PLAYER)
            HandlePlayerInput(@event);
    }

    public override void _Process(double delta)
    {
        if (turnState == TurnState.ENEMY)
        {
            enemyActionTimer += (float)delta;
            if (enemyActionTimer >= ENEMY_ACTION_DELAY)
            {
                enemyActionTimer = 0f;
                UpdateEnemies();
            }
        }
    }
    #endregion

    #region Input Handling
    private void HandlePlayerInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
            HandleKeyPress(keyEvent.Keycode);
        else if (@event is InputEventMouseButton click && click.IsReleased())
            HandleMouseClick(LocalToMap(GetLocalMousePosition()));
    }

    private void HandleKeyPress(Key key)
    {
        switch (key)
        {
            case Key.Key1: SetAction(ActionState.MOVE); break;
            case Key.Key2: SetAction(ActionState.ABILITY); break;
            case Key.Enter: NextPlayerTurn(); break;
        }
    }

    private void HandleMouseClick(Vector2I selectedCell)
    {
        bool actionTaken = currentAction == ActionState.MOVE
            ? MovePlayer(selectedCell)
            : UseAbility(selectedCell);

        if (actionTaken)
        {
            UpdateMap();
            CheckAndAdvanceTurn();
        }
    }

    private void CheckAndAdvanceTurn()
    {
        if (party[activePartyMember].endurance <= 0)
        {
            NextPlayerTurn();
        }
        else
        {
            FindTargets(party[activePartyMember].location);
        }
    }
    #endregion

    #region Map Management
    private void LoadMap()
    {
        foreach (Vector2I cell in GetUsedCells())
        {
            if (cell.X >= 0 && cell.Y >= 0)
            {
                Vector2I atlas = GetCellAtlasCoords(cell);
                map.Add(new Vector4I(cell.X, cell.Y, atlas.X, atlas.Y));
            }
        }
    }

    public void UpdateMap()
    {
        //Draw map
        foreach (Vector4I cell in map)
        {
            if (cell.X >= 0 && cell.Y >= 0)  // Validate cell position
            {
                Vector2I cellPosition = new Vector2I(cell.X, cell.Y);
                Vector2I cellAtlasCoords = new Vector2I(cell.Z, cell.W);
                SetCell(cellPosition, 1, cellAtlasCoords);
            }
            SetCell(new Vector2I(0,0), 1, Tiles.Wall);
        }

        //Draw Party Members and Enemies
        foreach (Character partyMember in party)
            SetCell(partyMember.location, 1, partyMember.isDead ? Tiles.Dead : Tiles.Player);
        foreach (Character enemy in enemies)
            SetCell(enemy.location, 1, enemy.isDead ? Tiles.Dead : Tiles.Enemy);
    }

    private void FindTargets(Vector2I position)
    {
        UpdateMap();
        Dictionary<Vector2I, int> validCells = FloodFill(
            position,
            currentAction == ActionState.MOVE 
                ? party[activePartyMember].endurance 
                : party[activePartyMember].currentAbility.range,
            currentAction == ActionState.MOVE
        );

        if (currentAction == ActionState.MOVE)
        {
            validMoves = validCells;
            foreach (var pos in validCells.Keys)
                SetCell(pos, 1, Tiles.Movement);
            // Redraw party members with death state
            foreach (Character member in party)
                SetCell(member.location, 1, member.isDead ? Tiles.Dead : Tiles.Player);
        }
        else
        {
            validTargets = validCells;
            foreach (var pos in validCells.Keys)
                SetCell(pos, 1, Tiles.AbilityTarget);
        }
    }

    private Dictionary<Vector2I, int> FloodFill(Vector2I start, int range, bool checkGround)
    {
        Queue<Vector2I> queue = new();
        HashSet<Vector2I> visited = new();
        Dictionary<Vector2I, int> cells = new() { [start] = 0 };
        
        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            Vector2I current = queue.Dequeue();
            int distance = cells[current];

            if (distance < range)
            {
                foreach (Vector2I next in GetSurroundingCells(current))
                {
                    if (!visited.Contains(next) && 
                        (!checkGround || GetCellAtlasCoords(next) == Tiles.Ground ||
                         party.Any(p => p.location == next) ||
                         allCharacters.Any(c => c.location == next && c.isDead)))
                    {
                        visited.Add(next);
                        queue.Enqueue(next);
                        cells[next] = distance + 1;
                    }
                }
            }
        }
        return cells;
    }
    #endregion

    #region Action Handling
    private void SetAction(ActionState newAction)
    {
        currentAction = newAction;
        FindTargets(party[activePartyMember].location);
    }

    public bool MovePlayer(Vector2I selectedCell)
    {
        if (validMoves.ContainsKey(selectedCell) && 
            !allCharacters.Any(c => c.location == selectedCell && !c.isDead))
        {
            int moveCost = validMoves[selectedCell];
            party[activePartyMember].location = selectedCell;
            party[activePartyMember].endurance -= moveCost;
            return true;
        }
        return false;
    }

    private bool UseAbility(Vector2I selectedCell)
    {
        if (validTargets.ContainsKey(selectedCell))
        {
            return UseAbility(party[activePartyMember], selectedCell);
        }
        return false;
    }

    private bool UseAbility(Character attacker, Vector2I targetPosition)
    {
        Ability currentAbility = attacker.currentAbility;
        
        if (attacker.endurance >= currentAbility.cost)
        {
            Character target = allCharacters.SingleOrDefault(c => c.location == targetPosition && !c.isDead);

            if (target != null)
            {
                attacker.endurance -= currentAbility.cost;
                target.TakeHit(currentAbility.power);
                
                // Print status after ability use
                GD.Print($"[{attacker.currentAbility.name}] {attacker.name} -> {target.name}");
                GD.Print($"Attacker: {attacker.name} (HP: {attacker.health}, EP: {attacker.endurance})");
                GD.Print($"Target: {target.name} (HP: {target.health}, EP: {target.endurance})");
                GD.Print("------------------------");
                return true;
            }
        }
        return false;
    }
    #endregion

    #region Turn Management
    public void UpdateEnemies()
    {
        UpdateMap();
        while (activeEnemy < enemies.Length)
        {
            Character enemy = enemies[activeEnemy];
            if (!enemy.isDead && enemy.endurance > 0)
            {
                EnemyAI.EnemyAction action = EnemyAI.GetAction(enemy.location, party, enemy.currentAbility.range, this, enemy);
                
                if (action.useAbility)
                {
                    UseAbility(enemy, action.targetPosition);
                }
                else
                {
                    enemy.location += action.moveDirection;
                    enemy.endurance--;
                    UpdateMap();
                }
                
                // Only increment activeEnemy if this enemy is out of endurance
                if (enemy.endurance <= 0)
                {
                    activeEnemy++;
                }
                return;
            }
            activeEnemy++;
        }

        // All enemies have acted, reset for player turn
        activeEnemy = 0;
        turnState = TurnState.PLAYER;
        
        // Reset endurance for all living characters
        foreach (Character member in party.Where(p => !p.isDead))
            member.endurance = member.maxEndurance;
        foreach (Character enemy in enemies.Where(e => !e.isDead))
            enemy.endurance = enemy.maxEndurance;

        // Skip dead player's turns
        while (party[activePartyMember].isDead && activePartyMember < party.Length - 1)
            activePartyMember++;
            
        FindTargets(party[activePartyMember].location);
    }

    private void NextPlayerTurn()
    {
        do {
            activePartyMember = (activePartyMember + 1) % party.Length;
            if (activePartyMember == 0) {
                turnState = TurnState.ENEMY;
                UpdateEnemies();
                return;
            }
        } while (party[activePartyMember].isDead);

        FindTargets(party[activePartyMember].location);
    }
    #endregion
}
