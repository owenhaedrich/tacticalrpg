using Godot;

public partial class GameManager : Node
{
    private int currentLevel = 0;
    private TurnManager activeLevel;
    private Node levelContainer;
    private CharacterSpriteManager spriteManager;
    private const int MAX_LEVELS = 2; // Update this based on number of level scenes

    public override void _Ready()
    {
        levelContainer = GetNode("Levels");
        spriteManager = GetNode<CharacterSpriteManager>("Characters");
        LoadCurrentLevel();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey eventKey && eventKey.Pressed)
        {
            if (eventKey.Keycode == Key.Bracketleft)
                SwitchToLevel(currentLevel - 1);
            else if (eventKey.Keycode == Key.Bracketright)
                SwitchToLevel(currentLevel + 1);
        }
    }

    private void SwitchToLevel(int newLevel)
    {
        if (newLevel < 0)
            newLevel = MAX_LEVELS - 1;
        else if (newLevel >= MAX_LEVELS)
            newLevel = 0;

        if (activeLevel != null)
        {
            activeLevel.QueueFree();
        }

        currentLevel = newLevel;
        LoadCurrentLevel();
    }

    public void NextLevel()
    {
        SwitchToLevel(currentLevel + 1);
    }

    private void LoadCurrentLevel()
    {
        var levelScene = GD.Load<PackedScene>($"res://Levels/level_{currentLevel}.tscn");
        activeLevel = levelScene.Instantiate<TurnManager>();
        levelContainer.AddChild(activeLevel);                
        var levelData = Levels.GetLevel(currentLevel);
        activeLevel.SetCharacters(levelData.Party, levelData.Enemies);
        
        spriteManager.Initialize(activeLevel);
        activeLevel.Initialize(spriteManager);
    }
}
