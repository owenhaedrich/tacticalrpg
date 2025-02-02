using Godot;

public partial class GameManager : Node
{
    private int currentLevel = 0;
    private TurnManager activeTurnManager;

    public override void _Ready()
    {
        activeTurnManager = GetNode("Levels").GetChild<TurnManager>(currentLevel);
    }

    public void NextLevel()
    {
        GetNode("Levels").GetChild<TurnManager>(currentLevel).QueueFree();
        currentLevel++;
        activeTurnManager = GetNode("Levels").GetChild<TurnManager>(currentLevel);
    }
}
