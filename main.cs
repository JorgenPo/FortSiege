using Godot;
using System;
using System.Collections.Generic;

public enum GameState
{
    ONGOING_GAME,
    LEVEL_CLEARED,
    GAME_OVER,
    NO_MORE_SHOTS
}

public enum FireState
{
    READY,
    SET_TRAJECTORY
}

public class main : Node2D
{
    // Declare member variables here. Examples:
    // private int a = 2;
    // private string b = "text";

    [Signal]
    public delegate void LevelChanged(Level level);

    private int currentLevel;
    private Level currentLevelObject;
    private bool gameStarted = false;

    private Node2D currentLevelNode;

    private bool levelCleared;
    private bool levelsCleared;

    private Weapon weapon;
    private EnemyCounterPanel counterPanel;
    private TimerWidget timer;
    private ShotCounter shotCounter;
    private Label levelDoneLabel;
    private Node2D fireButton;
    private Background background;
    private CoinSpawner coinSpawner;
    private CoinCounter coinCounter;
    private LevelSlider fireLevelSlider;

    [Export]
    private PackedScene menuScene;
    private Node2D menuNode;
    private Header header;

    private float timeSinceLastShot;
    private bool fired;
    private GameState state;
    private FireState fireState;

    ~main() {
        GD.Print("Enter destructor, clearing up...");
        StorageManager.Save();
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        GD.Print("Main: Init");

        // erase all cache
        //StorageManager.Clear();

        StorageManager.Init();
        Data.Init();
        UIManager.Init();

        counterPanel = GetNode<EnemyCounterPanel>("EnemyCounterPanel");
        fireButton = GetNode<Node2D>("FireButton");
        coinSpawner = GetNode<CoinSpawner>("CoinSpawner");
        coinCounter = GetNode<CoinCounter>("CoinCounter");
        fireLevelSlider = GetNode<LevelSlider>("LevelSlider");

        coinSpawner.counter = coinCounter;
    }

    private void ExitFromWeaponShop()
    {
        GetNode<Node2D>("Menu").Visible = true;
        menuNode.Visible = false;
    }
    
    public void StartGame() {
        currentLevel = 1;
        gameStarted = true;
        fired = false;
        levelsCleared = true;
        state = GameState.ONGOING_GAME;

        var menu = GetNode<Node2D>("Menu");
        menu.Visible = false;
        menuNode.Visible = false;

        fireLevelSlider.Visible = true;

        var weaponActivator = GetNode<PropertyBasedActivator>("Weapons");
        weaponActivator.Activate();

        weapon = weaponActivator.activeChild as Weapon;
        weapon.Reset();
        weapon.Visible = true;
        weapon.SetEnabled(true);
        weapon.Connect("Fired", this, "BallFired");
        weapon.Connect("ForceChanged", fireLevelSlider, "SetLevel");
        fireLevelSlider.Connect("LevelChanged", weapon, "SetForce");
        
        ResetFireState();

        timer = GetNode<TimerWidget>("TimerWidget");
        timer.Reset(60);

        shotCounter = GetNode<ShotCounter>("ShotCounter");

        levelDoneLabel = GetNode<Label>("LevelDone");

        background = GetNode<Background>("Backgound");
        background.Init();

        coinCounter.SetCount(0);

        GD.Print("Current coin count: " + coinCounter.count);
        
        LoadCurrentLevel();
    }

    private void ResetFireState()
    {
        if (weapon.info.controlTrajectory)
        {
            fireState = FireState.READY;
            GetNode<Label>("FireButton/Label").Text = "Set";
        }
        else 
        {
            fireState = FireState.SET_TRAJECTORY;
            GetNode<Label>("FireButton/Label").Text = "Fire";
        }

        fireLevelSlider.SetLevel(0.0f);
        fireButton.Visible = true;
    }

    private void BallFired()
    {
        GD.Print("BallFired");
        shotCounter.ShotPerformed();

        ResetFireState();
    }

    private void EnemyDied(Enemy enemy)
    {
        coinSpawner.SpawnCoins(enemy.coinReward, enemy.GlobalPosition);
    }

    public void NoMoreShots() {
        GD.Print("Main: No more shots");

        timeSinceLastShot = 0;
        state = GameState.NO_MORE_SHOTS;

        fireButton.Visible = false;
        //GameOver("Oh no, no more shots left!");
    }

    private void LoadCurrentLevel() {
        GD.Print("Loading level: Level" + currentLevel.ToString());

        // clean up objects
        coinSpawner.ClearCoins();
        weapon.ClearProjectiles();
        //

        var packedLevel = ResourceLoader.Load<PackedScene>("res://Level" + currentLevel.ToString() + ".tscn");
        var sceneObject = packedLevel.Instance<Node2D>();

        var instancePosition = GetNode<Position2D>("BuildingPosition");

        sceneObject.Position = instancePosition.Position;

        AddChildBelowNode(weapon, sceneObject);

        currentLevelObject = sceneObject as Level;
        currentLevelObject.Connect("AllEnemiesAreDead", this, "AllEnemiesAreDead");
        currentLevelObject.Connect("AllPrefabsAreStill", this, "AllPrefabsAreStill");
        currentLevelObject.Connect("LevelEnemyDied", this, "EnemyDied");
        currentLevelObject.Connect("CoinCollected", this, "CoinCollected");
        currentLevelObject.Connect("ChestDestroyed", coinSpawner, "SpawnCoins");

        timer.Reset(60);
        counterPanel.Init(currentLevelObject.getEnemies());
        shotCounter.Init(weapon.info);
        
        currentLevelNode = sceneObject;

        EmitSignal("LevelChanged", currentLevelObject);
    }

    private void CoinCollected(int count)
    {
        coinCounter.SetCount(coinCounter.count + count);
    }

    private void AllPrefabsAreStill()
    {
        GD.Print("All prefabs stay still");
    }

    public void FireButtonPressed() {
        if (fireState == FireState.READY)
        {
            GD.Print("Set trajectory button pressed");
            fireState = FireState.SET_TRAJECTORY;
            weapon.currentState = FireState.SET_TRAJECTORY;
            GetNode<Label>("FireButton/Label").Text = "Fire";
            return;
        }

        GD.Print("Fire button pressed");
        fired = true;
        weapon.Fire();
        ResetFireState();
    }

    public override void _UnhandledInput(InputEvent @event)
	{
		if (!(@event is InputEventScreenTouch touchEvent))
		{
            return;
		}
	}

    public void Restart() {
        GD.Print("Restart Level");
        
        coinCounter.SetCount(0);
        currentLevelObject.Visible = false;
        currentLevelObject.QueueFree();
        weapon.Reset();
        levelCleared = false;
        levelsCleared = false;

        state = GameState.ONGOING_GAME;
        LoadCurrentLevel();

        ResetFireState();
        
        levelDoneLabel.Visible = false;
        GetNode<Node2D>("RestartButton").Visible = false;
    }

    private void LevelOver(String message, bool gameover = false) {
        var label = GetNode<Label>("LevelDone");
        label.Text = message;
        label.Visible = true;
        gameStarted = false;
        fireButton.Visible = false;

        GetNode<Label>("RestartButton/HBoxContainer/MarginContainer/Label").Text = gameover ? "Restart" : "Next Level";
        GetNode<Node2D>("RestartButton").Visible = true;

        if (!gameover)
        {
            levelCleared = true;
        }

        timer.Stop();
        state = GameState.GAME_OVER;

        weapon.SetEnabled(false);
    }

    private void ChangeLevel() {
        levelCleared = false;
        currentLevel++;
        if (currentLevel > 4)
        {
            currentLevel = 1;
        }
        state = GameState.ONGOING_GAME;

        currentLevelNode.QueueFree();

        GetNode<Label>("LevelDone").Visible = false;
        ResetFireState();

        counterPanel.Reset();
        coinCounter.SetCount(0);
        weapon.Reset();      
        timer.Reset(60);
        background.Reset();
        fireLevelSlider.SetLevel(0);

        LoadCurrentLevel();
    }

    public void OnMenuGameStartedSignal()
    {
        if (menuNode == null) {
            menuNode =  menuScene.Instance<WeaponShop>();
            header = menuNode.GetNode<Header>("VBoxContainer/Header");  

            header.Connect("BackActionFired", this, "ExitFromWeaponShop");
            menuNode.Connect("ScreenDone", this, "StartGame");

            AddChildBelowNode(GetNode<Node2D>("CoinCounter"), menuNode);

            menuNode.GlobalPosition = new Vector2(0, 0);

            GD.Print("MENU Y" + menuNode.GlobalPosition.y);
        }

        menuNode.Visible = true;
        GetNode<Node2D>("Menu").Visible = false;
    }

    public void AllEnemiesAreDead() {
        levelCleared = true;
        var newCountCount = StorageManager.GetInt(PropertyKeys.COIN_COUNT) + coinCounter.count;
        StorageManager.StoreValue(PropertyKeys.COIN_COUNT, newCountCount);
        StorageManager.Save();
        LevelOver("Level cleared!");
    }

    public void onTimeout() {
        GD.Print("Time's up");

        LevelOver("Time's up", true);
    }

 // Called every frame. 'delta' is the elapsed time since the previous frame.
 public override void _Process(float delta)
 {
     timeSinceLastShot += delta;

     if (state != GameState.NO_MORE_SHOTS) {
         return;
     }

     if (timeSinceLastShot >= 3.5f)
     {
         var movingPrefabs = currentLevelObject.activePrefabs;
         if (movingPrefabs == 0)
         {
             GD.Print("No more shots and every object is still. Game over");
             LevelOver("No more shots!", true);
         }
     }
 }

 public void OnLevelOverButtonPressed()
 {
     GD.Print("Level cleared button presed");
     
     if (levelCleared)
     {
         GD.Print("Next Level");
         ChangeLevel(); 
     } else
     {
         GD.Print("Restart");
         Restart();
     }

     GetNode<Node2D>("RestartButton").Visible = false;
 }
}
