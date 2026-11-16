using Godot;
using System;
using System.Collections.Generic;

public partial class SnakeGame : Node2D
{
    // Board settings. The default board is 20 x 20 cells, with each cell 32 pixels wide.
    [Export] public int GridWidth { get; set; } = 20;
    [Export] public int GridHeight { get; set; } = 20;
    [Export] public int CellSize { get; set; } = 32;

    // The snake moves once per MoveInterval seconds. A smaller value means faster movement.
    [Export] public float MoveInterval { get; set; } = 0.14f;
    [Export] public float MinimumMoveInterval { get; set; } = 0.07f;
    [Export] public float SpeedUpPerFruit { get; set; } = 0.003f;
    [Export] public Texture2D HeadTexture { get; set; }
    [Export] public Texture2D BodyTexture { get; set; }
    [Export] public Texture2D TailTexture { get; set; }
    [Export] public Texture2D FruitTexture1 { get; set; }
    [Export] public Texture2D FruitTexture2 { get; set; }
    [Export] public Texture2D FruitTexture3 { get; set; }

    // Use this to rotate the head and tail manually
    [Export] public float HeadSpriteRotationOffsetDegrees { get; set; } = 0.0f;
    [Export] public float TailSpriteRotationOffsetDegrees { get; set; } = 0.0f;
    [Export] public AudioStream EatSound { get; set; }

    private const int SideMargin = 16;
    private const int HeaderHeight = 56;
    private const int PointsPerFruit = 1;

    private readonly List<Vector2I> _snake = new();
    private readonly List<Sprite2D> _bodySprites = new();
    private readonly RandomNumberGenerator _random = new();

    private Vector2I _direction = Vector2I.Right;
    private Vector2I _queuedDirection = Vector2I.Right;
    private bool _hasQueuedDirection;
    private Vector2I _fruitCell;
    private bool _gameOver;
    private int _score;
    private float _timeUntilMove;
    private float _currentMoveInterval;
    private Sprite2D _headSprite;
    private Sprite2D _tailSprite;
    private Sprite2D _fruitSprite;
    private AudioStreamPlayer _eatSoundPlayer;
    private CanvasLayer _uiLayer;
    private Label _scoreLabel;
    private Label _controlsLabel;
    private Label _gameMessageLabel;
    private Label _restartMessageLabel;

    private Vector2 BoardOrigin => new(SideMargin, HeaderHeight);
    private int BoardPixelWidth => GridWidth * CellSize;
    private int BoardPixelHeight => GridHeight * CellSize;
    private int ViewportPixelWidth => BoardPixelWidth + SideMargin * 2;
    private int ViewportPixelHeight => BoardPixelHeight + HeaderHeight + SideMargin;

    // The Ready function restricts the grid, sets some values, and starts the game.
    public override void _Ready()
    {
        GridWidth = Math.Max(5, GridWidth);
        GridHeight = Math.Max(5, GridHeight);
        CellSize = Math.Max(8, CellSize);
        MoveInterval = Math.Max(0.04f, MoveInterval);
        MinimumMoveInterval = Math.Clamp(MinimumMoveInterval, 0.04f, MoveInterval);
        SpeedUpPerFruit = Math.Max(0.0f, SpeedUpPerFruit);

        _random.Randomize();
        _currentMoveInterval = MoveInterval;

        CreateSprites();
        CreateAudioPlayer();
        CreateUserInterface();
        StartNewGame();

        QueueRedraw();
    }

    // The Process function handles the time intervals for movement.
    public override void _Process(double delta)
    {
        if (_gameOver)
        {
            return;
        }

        _timeUntilMove -= (float)delta;

        if (_timeUntilMove <= 0.0f)
        {
            MoveSnake();
            _timeUntilMove = _currentMoveInterval;
        }
    }

    // The Input function handles the inputs from the user, such as the WASD keys and arrow keys for movement, as well as enter and space to restart.
    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
        {
            return;
        }

        if (_gameOver)
        {
            if (keyEvent.Keycode == Key.Enter || keyEvent.Keycode == Key.Space)
            {
                StartNewGame();
            }

            return;
        }

        // Only accept one turn between movement steps. This prevents quick key presses
        // from accidentally allowing the snake to reverse direction.
        if (_hasQueuedDirection)
        {
            return;
        }

        Vector2I requestedDirection = keyEvent.Keycode switch
        {
            Key.Up or Key.W => Vector2I.Up,
            Key.Down or Key.S => Vector2I.Down,
            Key.Left or Key.A => Vector2I.Left,
            Key.Right or Key.D => Vector2I.Right,
            _ => Vector2I.Zero
        };

        if (requestedDirection == Vector2I.Zero || requestedDirection == -_direction)
        {
            return;
        }

        _queuedDirection = requestedDirection;
        _hasQueuedDirection = true;
    }

    // The Draw function draws board and substitutes any missing sprites with simple shapes. This allows the game to work with no additional assets.
    public override void _Draw()
    {
        // Background and board are drawn in code so no background scene nodes are needed.
        DrawRect(
            new Rect2(Vector2.Zero, new Vector2(ViewportPixelWidth, ViewportPixelHeight)),
            new Color(0.075f, 0.085f, 0.105f),
            true);

        Rect2 boardRect = new(BoardOrigin, new Vector2(BoardPixelWidth, BoardPixelHeight));
        DrawRect(boardRect, new Color(0.12f, 0.15f, 0.18f), true);

        Color gridColor = new(0.19f, 0.22f, 0.25f);

        for (int x = 0; x <= GridWidth; x++)
        {
            float pixelX = BoardOrigin.X + x * CellSize;
            DrawLine(
                new Vector2(pixelX, BoardOrigin.Y),
                new Vector2(pixelX, BoardOrigin.Y + BoardPixelHeight),
                gridColor,
                1.0f);
        }

        for (int y = 0; y <= GridHeight; y++)
        {
            float pixelY = BoardOrigin.Y + y * CellSize;
            DrawLine(
                new Vector2(BoardOrigin.X, pixelY),
                new Vector2(BoardOrigin.X + BoardPixelWidth, pixelY),
                gridColor,
                1.0f);
        }

        DrawRect(boardRect, new Color(0.42f, 0.48f, 0.52f), false, 2.0f);

        // These simple shapes let the game run before any art has been assigned.
        if (FruitTexture1 == null && FruitTexture2 == null && FruitTexture3 == null)
        {
            DrawCircle(CellToPixel(_fruitCell), CellSize * 0.34f, new Color(0.90f, 0.24f, 0.27f));
        }

        if (BodyTexture == null)
        {
            for (int i = 1; i < _snake.Count - 1; i++)
            {
                DrawCircle(CellToPixel(_snake[i]), CellSize * 0.37f, new Color(0.28f, 0.72f, 0.43f));
            }
        }

        if (TailTexture == null && BodyTexture == null && _snake.Count > 1)
        {
            DrawCircle(CellToPixel(_snake[^1]), CellSize * 0.37f, new Color(0.28f, 0.72f, 0.43f));
        }

        if (HeadTexture == null && _snake.Count > 0)
        {
            DrawCircle(CellToPixel(_snake[0]), CellSize * 0.40f, new Color(0.48f, 0.90f, 0.58f));
        }
    }

    // The CreateSprites function allows the sprites and sounds to be set in the Godot editor and controls messages, labels, and in-game UI.
    private void CreateSprites()
    {
        _headSprite = new Sprite2D
        {
            Name = "SnakeHead",
            ZIndex = 2
        };
        AddChild(_headSprite);
        SetSpriteTexture(_headSprite, HeadTexture);

        _tailSprite = new Sprite2D
        {
            Name = "SnakeTail",
            ZIndex = 1
        };

        AddChild(_tailSprite);
        SetSpriteTexture(_tailSprite, TailTexture ?? BodyTexture);
        _tailSprite.Visible = TailTexture != null || BodyTexture != null;

        _fruitSprite = new Sprite2D
        {
            Name = "Fruit",
            ZIndex = 1
        };
        AddChild(_fruitSprite);
        _fruitSprite.Visible = false;
    }

    private void CreateAudioPlayer()
    {
        _eatSoundPlayer = new AudioStreamPlayer
        {
            Name = "EatSoundPlayer",
            Stream = EatSound
        };
        AddChild(_eatSoundPlayer);
    }

    private void CreateUserInterface()
    {
        _uiLayer = new CanvasLayer
        {
            Name = "UI"
        };
        AddChild(_uiLayer);

        _scoreLabel = new Label
        {
            Name = "ScoreLabel",
            Position = new Vector2(SideMargin, 10),
            Text = "Score: 0"
        };
        _scoreLabel.AddThemeFontSizeOverride("font_size", 22);
        _scoreLabel.AddThemeColorOverride("font_color", new Color(0.94f, 0.96f, 0.97f));
        _uiLayer.AddChild(_scoreLabel);

        _controlsLabel = new Label
        {
            Name = "ControlsLabel",
            Position = new Vector2(190, 14),
            Text = "Move: WASD / Arrow Keys"
        };
        _controlsLabel.AddThemeFontSizeOverride("font_size", 15);
        _controlsLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.77f, 0.80f));
        _uiLayer.AddChild(_controlsLabel);

        _gameMessageLabel = new Label
        {
            Name = "GameMessageLabel",
            Position = new Vector2(SideMargin, HeaderHeight + BoardPixelHeight / 2.0f - 34),
            Size = new Vector2(BoardPixelWidth, 36),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Text = "GAME OVER",
            Visible = false
        };
        _gameMessageLabel.AddThemeFontSizeOverride("font_size", 30);
        _gameMessageLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.92f, 0.92f));
        _gameMessageLabel.AddThemeColorOverride("font_outline_color", new Color(0.05f, 0.05f, 0.06f));
        _gameMessageLabel.AddThemeConstantOverride("outline_size", 5);
        _uiLayer.AddChild(_gameMessageLabel);

        _restartMessageLabel = new Label
        {
            Name = "RestartMessageLabel",
            Position = new Vector2(SideMargin, HeaderHeight + BoardPixelHeight / 2.0f + 8),
            Size = new Vector2(BoardPixelWidth, 28),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Text = "Press Enter or Space to restart",
            Visible = false
        };
        _restartMessageLabel.AddThemeFontSizeOverride("font_size", 16);
        _restartMessageLabel.AddThemeColorOverride("font_color", new Color(0.94f, 0.96f, 0.97f));
        _uiLayer.AddChild(_restartMessageLabel);
    }

    // The StartNewGame function resets all variable values for the next game instance.
    private void StartNewGame()
    {
        _snake.Clear();

        // Start in the middle, facing right, with a length of three cells.
        Vector2I start = new(GridWidth / 2, GridHeight / 2);
        _snake.Add(start);
        _snake.Add(start + Vector2I.Left);
        _snake.Add(start + Vector2I.Left * 2);

        _direction = Vector2I.Right;
        _queuedDirection = Vector2I.Right;
        _hasQueuedDirection = false;
        _gameOver = false;
        _score = 0;
        _currentMoveInterval = MoveInterval;
        _timeUntilMove = _currentMoveInterval;

        _gameMessageLabel.Visible = false;
        _restartMessageLabel.Visible = false;
        UpdateScoreLabel();
        UpdateSnakeVisuals();
        SpawnFruit();
        QueueRedraw();
    }

    // The MoveSnake function progresses the snake, increases the length of the snake, and handles win/loss scenarios.
    private void MoveSnake()
    {
        if (_hasQueuedDirection)
        {
            _direction = _queuedDirection;
            _hasQueuedDirection = false;
        }

        Vector2I nextHead = _snake[0] + _direction;

        // Hitting the outside edge ends the game.
        if (nextHead.X < 0 || nextHead.X >= GridWidth ||
            nextHead.Y < 0 || nextHead.Y >= GridHeight)
        {
            EndGame("GAME OVER");
            return;
        }

        bool ateFruit = nextHead == _fruitCell;

        // If the snake is not growing, its last cell moves away this step. That means moving into the current tail cell is allowed in that case.
        int cellsToCheck = ateFruit ? _snake.Count : _snake.Count - 1;

        for (int i = 0; i < cellsToCheck; i++)
        {
            if (_snake[i] == nextHead)
            {
                EndGame("GAME OVER");
                return;
            }
        }

        _snake.Insert(0, nextHead);

        if (ateFruit)
        {
            _score += PointsPerFruit;
            _currentMoveInterval = Mathf.Max(
                MinimumMoveInterval,
                _currentMoveInterval - SpeedUpPerFruit);

            UpdateScoreLabel();

            if (_eatSoundPlayer.Stream != null)
            {
                _eatSoundPlayer.Play();
            }

            if (_snake.Count >= GridWidth * GridHeight)
            {
                UpdateSnakeVisuals();
                EndGame("YOU WIN!");
                return;
            }

            SpawnFruit();
        }
        else
        {
            _snake.RemoveAt(_snake.Count - 1);
        }

        UpdateSnakeVisuals();
        QueueRedraw();
    }

    // The SpawnFruit function selects a cell on the grid where the snake isn't, obtains a random fruit, and spawns the fruit there.
    private void SpawnFruit()
    {
        // Build a list of empty cells so fruit can never appear inside the snake.
        List<Vector2I> emptyCells = new();

        for (int y = 0; y < GridHeight; y++)
        {
            for (int x = 0; x < GridWidth; x++)
            {
                Vector2I cell = new(x, y);

                if (!_snake.Contains(cell))
                {
                    emptyCells.Add(cell);
                }
            }
        }

        if (emptyCells.Count == 0)
        {
            _fruitSprite.Visible = false;
            EndGame("YOU WIN!");
            return;
        }

        _fruitCell = emptyCells[_random.RandiRange(0, emptyCells.Count - 1)];
        _fruitSprite.Position = CellToPixel(_fruitCell);

        Texture2D chosenFruitTexture = ChooseRandomFruitTexture();
        SetSpriteTexture(_fruitSprite, chosenFruitTexture);
        _fruitSprite.Visible = chosenFruitTexture != null;

        QueueRedraw();
    }

    // The ChooseFruit function selects a random fruit for the SpawnFruit function to use.
    private Texture2D ChooseRandomFruitTexture()
    {
        List<Texture2D> availableTextures = new();

        if (FruitTexture1 != null) availableTextures.Add(FruitTexture1);
        if (FruitTexture2 != null) availableTextures.Add(FruitTexture2);
        if (FruitTexture3 != null) availableTextures.Add(FruitTexture3);

        if (availableTextures.Count == 0)
        {
            return null;
        }

        return availableTextures[_random.RandiRange(0, availableTextures.Count - 1)];
    }

    // The UpdateSnakeVisuals function determines the correct sprites for the snake based on the state of the body, which constantly changes.
    private void UpdateSnakeVisuals()
    {
        // The head and tail have their own sprites. Only the cells between them use BodyTexture.
        int bodyCellCount = Math.Max(0, _snake.Count - 2);

        // Create enough Sprite2D nodes for the middle body cells.
        while (_bodySprites.Count < bodyCellCount)
        {
            Sprite2D bodySprite = new()
            {
                Name = $"SnakeBody{_bodySprites.Count + 1}",
                ZIndex = 1
            };

            AddChild(bodySprite);
            SetSpriteTexture(bodySprite, BodyTexture);
            bodySprite.Visible = BodyTexture != null;
            _bodySprites.Add(bodySprite);
        }

        // Reuse existing sprites instead of constantly deleting and recreating nodes.
        for (int i = 0; i < _bodySprites.Count; i++)
        {
            bool shouldBeVisible = i < bodyCellCount && BodyTexture != null;
            _bodySprites[i].Visible = shouldBeVisible;

            if (i < bodyCellCount)
            {
                _bodySprites[i].Position = CellToPixel(_snake[i + 1]);
            }
        }

        // Position the tail at the final snake cell and rotate it away from the body.
        if (_snake.Count > 1)
        {
            Vector2I tailCell = _snake[^1];
            Vector2I cellBeforeTail = _snake[^2];
            Vector2I tailDirection = tailCell - cellBeforeTail;

            _tailSprite.Position = CellToPixel(tailCell);
            _tailSprite.Rotation =
                Mathf.Atan2(tailDirection.Y, tailDirection.X) +
                Mathf.DegToRad(TailSpriteRotationOffsetDegrees);

            _tailSprite.Visible = TailTexture != null || BodyTexture != null;
        }
        else
        {
            _tailSprite.Visible = false;
        }

        _headSprite.Position = CellToPixel(_snake[0]);
        _headSprite.Rotation =
            Mathf.Atan2(_direction.Y, _direction.X) +
            Mathf.DegToRad(HeadSpriteRotationOffsetDegrees);

        _headSprite.Visible = HeadTexture != null;
    }

    // The SetSpriteTexture function implements the changes in the sprites as decided by the UpdateSnakeVisuals function.
    private void SetSpriteTexture(Sprite2D sprite, Texture2D texture)
    {
        sprite.Texture = texture;

        if (texture == null)
        {
            sprite.Scale = Vector2.One;
            return;
        }

        Vector2 textureSize = texture.GetSize();

        if (textureSize.X <= 0.0f || textureSize.Y <= 0.0f)
        {
            sprite.Scale = Vector2.One;
            return;
        }

        // Fit the whole image inside one grid cell without distorting its proportions.
        float scale = Math.Min(
            CellSize / textureSize.X,
            CellSize / textureSize.Y);
        sprite.Scale = new Vector2(scale, scale);
    }

    // The CellToPixel function converts the cell size to a value that other functions can use.
    private Vector2 CellToPixel(Vector2I cell)
    {
        return BoardOrigin + new Vector2(
            (cell.X + 0.5f) * CellSize,
            (cell.Y + 0.5f) * CellSize);
    }

    // The UpdateScoreLabel changes the displayed score every time it's called based the currect score value.
    private void UpdateScoreLabel()
    {
        _scoreLabel.Text = $"Score: {_score}";
    }

    // The EndGame function stops the game, displays the game over message, and allows for the restart.
    private void EndGame(string message)
    {
        _gameOver = true;
        _gameMessageLabel.Text = message;
        _gameMessageLabel.Visible = true;
        _restartMessageLabel.Visible = true;
        QueueRedraw();
    }
}
