using Godot;
using System.Linq;
using System.Collections.Generic;

public partial class TurnManager : TileMapLayer
{
	enum TurnState
	{
		PLAYER,
		ENEMY
	}

    enum ActionMode
    {
        MOVE,
        ABILITY
    }
	
	// Player, Enemies, Map Locations
	List<Vector4I> map = new List<Vector4I>(); // Dynamic map
	Character[] party = {
		new Character(new Vector2I(1, 1), 100, 3),
		new Character(new Vector2I(3, 3), 100, 3)
	};
	Character[] enemies = {
		new Character(new Vector2I(17, 10), 80, 3),
		new Character(new Vector2I(13, 10), 80, 3)
	};
	
	// Turn Management
	TurnState turnState = TurnState.PLAYER;
	int activePartyMember = 0; 

    // Action Management
    ActionMode currentAction = ActionMode.MOVE;
    private static readonly Vector2I TILE_ABILITY_TARGET = new Vector2I(0, 2);
    private Dictionary<Vector2I, int> validTargets = new();

    // Atlas coordinate constants
    private static readonly Vector2I TILE_GROUND = new Vector2I(1, 0);
    private static readonly Vector2I TILE_PLAYER = new Vector2I(0, 0);
    private static readonly Vector2I TILE_MOVEMENT = new Vector2I(1, 1);
    private static readonly Vector2I TILE_ENEMY = new Vector2I(1, 2);
    private static readonly Vector2I TILE_WALL = new Vector2I(0, 1);

    // Valid moves set
    private Dictionary<Vector2I, int> validMoves = new();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{ 
		Vector2I[] usedCells = GetUsedCells().ToArray();
		foreach (Vector2I cell in usedCells)
		{
			Vector2I cellData = GetCellAtlasCoords(cell);
			// Only add cells that have valid coordinates
			if (cell.X >= 0 && cell.Y >= 0)
			{
				map.Add(new Vector4I(cell.X, cell.Y, cellData.X, cellData.Y));
			}
		}
		UpdateMap();
		FindTargets(party[activePartyMember].location);
	}

	public override void _Input(InputEvent @event)
	{
		if (turnState == TurnState.PLAYER)
        {
            if (@event is InputEventKey keyEvent && keyEvent.Pressed)
            {
                if (keyEvent.Keycode == Key.Key1)
                {
                    currentAction = ActionMode.MOVE;
                    FindTargets(party[activePartyMember].location);
                }
                else if (keyEvent.Keycode == Key.Key2)
                {
                    currentAction = ActionMode.ABILITY;
                    FindTargets(party[activePartyMember].location);
                }
            }
            else if (@event is InputEventMouseButton click && click.IsReleased())
            {
                Vector2I selectedCell = LocalToMap(click.Position);
                bool actionTaken = false;

                if (currentAction == ActionMode.MOVE)
                {
                    actionTaken = MovePlayer(selectedCell);
                }
                else if (currentAction == ActionMode.ABILITY)
                {
                    actionTaken = UseAbility(selectedCell);
                }

                if (actionTaken)
                {
                    UpdateMap();
                    if (party[activePartyMember].endurance <= 0)
                    {
                        NextPlayerTurn();
                    }
                    else
                    {
                        FindTargets(party[activePartyMember].location);
                    }
                }
            }
        }
		else if (turnState == TurnState.ENEMY)
		{
			turnState = TurnState.PLAYER;
			
			// Reset endurance for all party members when player turn starts
			foreach (Character member in party)
			{
				member.endurance = member.maxEndurance;
			}
			 FindTargets(party[activePartyMember].location);
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
			SetCell(new Vector2I(0,0), 1, TILE_WALL);
		}

		//Draw Party Members and Enemies
		foreach (Character partyMember in party)
			SetCell(partyMember.location, 1, TILE_PLAYER);
		foreach (Character enemy in enemies)
			SetCell(enemy.location, 1, TILE_ENEMY);
	}

	private void FindTargets(Vector2I position)
	{
	    UpdateMap();
	    Queue<Vector2I> queue = new();
	    HashSet<Vector2I> visited = new();
	    Dictionary<Vector2I, int> validCells = new();
	    int range;

	    queue.Enqueue(position);
	    visited.Add(position);
	    validCells[position] = 0;

	    switch (currentAction)
	    {
	        case ActionMode.MOVE:
	            range = party[activePartyMember].endurance;
	            while (queue.Count > 0)
	            {
	                Vector2I currentPos = queue.Dequeue();
	                int distance = validCells[currentPos];

	                if (distance < range)
	                {
	                    foreach (Vector2I neighborPos in GetSurroundingCells(currentPos))
	                    {
	                        if (!visited.Contains(neighborPos) && 
	                            (GetCellAtlasCoords(neighborPos) == TILE_GROUND || 
	                             party.Any(p => p.location == neighborPos)))
	                        {
	                            visited.Add(neighborPos);
	                            queue.Enqueue(neighborPos);
	                            validCells[neighborPos] = distance + 1;
	                            SetCell(neighborPos, 1, TILE_MOVEMENT);
	                        }
	                    }
	                }
	            }
	            validMoves = validCells;
	            
	            // Redraw party members on top of movement tiles
	            foreach (Character partyMember in party)
	                SetCell(partyMember.location, 1, TILE_PLAYER);
	            break;

	        case ActionMode.ABILITY:
	            range = 2; // Ability range
	            while (queue.Count > 0)
	            {
	                Vector2I currentPos = queue.Dequeue();
	                int distance = validCells[currentPos];

	                if (distance < range)
	                {
	                    foreach (Vector2I neighborPos in GetSurroundingCells(currentPos))
	                    {
	                        if (!visited.Contains(neighborPos))
	                        {
	                            visited.Add(neighborPos);
	                            queue.Enqueue(neighborPos);
	                            validCells[neighborPos] = distance + 1;
	                            SetCell(neighborPos, 1, TILE_ABILITY_TARGET);
	                        }
	                    }
	                }
	            }
	            validTargets = validCells;
	            break;
	    }
	}

    private bool UseAbility(Vector2I selectedCell)
    {
        if (validTargets.ContainsKey(selectedCell))
        {
            // For now, just consume all remaining endurance
            party[activePartyMember].endurance = 0;
            // TODO: Implement actual ability effects
            return true;
        }
        return false;
    }

	public bool MovePlayer(Vector2I selectedCell)
	{
	    if (validMoves.ContainsKey(selectedCell) && 
	        !party.Any(p => p.location == selectedCell) && 
	        !enemies.Any(e => e.location == selectedCell))
	    {
	        int moveCost = validMoves[selectedCell];
	        party[activePartyMember].location = selectedCell;
	        party[activePartyMember].endurance -= moveCost;
	        return true;
	    }
	    return false;
	}
	
	public void UpdateEnemies()
	{
	    foreach (Character enemy in enemies)
	    {
	        Vector2I targetPos = EnemyAI.FindClosestTarget(enemy.location, party);
	        Vector2I moveDir = EnemyAI.GetMoveDirection(enemy.location, targetPos, this);
	        enemy.location += moveDir;
			UpdateMap();
	    }
	}

    private void NextPlayerTurn()
    {
        if (activePartyMember < (party.Length - 1))
        {
            activePartyMember++;
            FindTargets(party[activePartyMember].location);
        }
        else
        {
            activePartyMember = 0;
            turnState = TurnState.ENEMY;
            UpdateEnemies();
        }
    }
}
