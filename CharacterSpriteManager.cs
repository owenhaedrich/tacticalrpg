using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class CharacterSpriteManager : Node2D
{
    private Dictionary<Character, Node2D> characterSprites = new();
    private Dictionary<Character, Vector2> targetPositions = new();
    private TurnManager turnManager;
    private readonly Vector2 TILE_SIZE = new(32, 32);
    private const float MOVEMENT_SPEED = 200.0f; // pixels per second

    public void Initialize(TurnManager turnManagerReference)
    {
        turnManager = turnManagerReference;
        CreateAllSprites();  // Create sprites after setting turnManager
    }

    private void CreateAllSprites()
    {
        // Clear existing sprites
        foreach (var sprite in characterSprites.Values)
            sprite.QueueFree();
        characterSprites.Clear();

        // Create sprites for all characters
        foreach (var character in turnManager.party)
        {
            var spriteContainer = new Node2D();
            AddChild(spriteContainer);
            var sprite = GD.Load<PackedScene>($"res://Characters/Heroes/{character.name}.tscn").Instantiate();
            spriteContainer.AddChild(sprite);
            characterSprites[character] = spriteContainer;
            UpdateSpritePosition(character);
            SetSpriteState(character, character.isDead);
        }
        foreach (var character in turnManager.enemies)
        {
            var spriteContainer = new Node2D();
            AddChild(spriteContainer);
            var sprite = GD.Load<PackedScene>($"res://Characters/Enemies/{character.name}.tscn").Instantiate();
            spriteContainer.AddChild(sprite);
            characterSprites[character] = spriteContainer;
            UpdateSpritePosition(character);
            SetSpriteState(character, character.isDead);
        }
    }

    public override void _Process(double delta)
    {
        foreach (var character in characterSprites.Keys.ToList())
        {
            if (characterSprites.TryGetValue(character, out Node2D sprite) && 
                targetPositions.TryGetValue(character, out Vector2 targetPos))
            {
                float step = (float)delta * MOVEMENT_SPEED;
                sprite.Position = sprite.Position.MoveToward(targetPos, step);
            }
        }
    }

    public void UpdateSpritePosition(Character character)
    {
        if (characterSprites.TryGetValue(character, out Node2D sprite))
        {
            Vector2 worldPosition = turnManager.MapToLocal(character.location);
            targetPositions[character] = worldPosition;
        }
    }

    public void SetSpriteState(Character character, bool isDead)
    {
        if (characterSprites.TryGetValue(character, out Node2D sprite))
        {
            // Adjust sprite appearance based on state
            sprite.Modulate = isDead ? new Color(1, 1, 1, 0.5f) : new Color(1, 1, 1, 1);
        }
    }
}
