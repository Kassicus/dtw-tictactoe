using Godot;
using System;
using System.Text.Json;

/// <summary>
/// Data structure representing a saved game state.
/// </summary>
public class GameSaveData
{
    public GameMode GameMode { get; set; }
    public Player CurrentPlayer { get; set; }
    public GameState State { get; set; }
    public Player GameWinner { get; set; }

    // Small board winners (flattened 3x3 = 9 elements)
    public Player[] SmallBoardWinners { get; set; } = new Player[9];

    // Cell occupancy (flattened 9 boards x 9 cells = 81 elements)
    // Index = boardX * 27 + boardY * 9 + cellX * 3 + cellY
    public Player[] CellOccupancy { get; set; } = new Player[81];

    public long SaveTimestamp { get; set; }
}

/// <summary>
/// Handles saving and loading game state.
/// </summary>
public static class SaveManager
{
    private const string SavePath = "user://savegame.json";

    /// <summary>
    /// Save the current game state to a file.
    /// </summary>
    public static bool SaveGame(BoardController boardController)
    {
        if (boardController == null)
        {
            GD.PrintErr("SaveManager: BoardController is null, cannot save.");
            return false;
        }

        var gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            GD.PrintErr("SaveManager: GameManager is null, cannot save.");
            return false;
        }

        var saveData = new GameSaveData
        {
            GameMode = GameManager.CurrentGameMode,
            CurrentPlayer = gameManager.CurrentPlayer,
            State = gameManager.State,
            GameWinner = gameManager.GameWinner,
            SaveTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        // Save small board winners and cell occupancy
        for (int bx = 0; bx < 3; bx++)
        {
            for (int by = 0; by < 3; by++)
            {
                var board = boardController.SmallBoards[bx, by];
                int boardIndex = bx * 3 + by;

                saveData.SmallBoardWinners[boardIndex] = board.Winner;

                // Save each cell in this board
                for (int cx = 0; cx < 3; cx++)
                {
                    for (int cy = 0; cy < 3; cy++)
                    {
                        var cell = board.Cells[cx, cy];
                        int cellIndex = bx * 27 + by * 9 + cx * 3 + cy;
                        saveData.CellOccupancy[cellIndex] = cell.OccupiedBy;
                    }
                }
            }
        }

        // Serialize to JSON
        try
        {
            var json = JsonSerializer.Serialize(saveData, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PrintErr($"SaveManager: Could not open file for writing: {FileAccess.GetOpenError()}");
                return false;
            }

            file.StoreString(json);
            GD.Print("SaveManager: Game saved successfully.");
            return true;
        }
        catch (Exception e)
        {
            GD.PrintErr($"SaveManager: Error saving game: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Load game state from a file.
    /// </summary>
    public static GameSaveData LoadGame()
    {
        if (!FileAccess.FileExists(SavePath))
        {
            GD.Print("SaveManager: No save file found.");
            return null;
        }

        try
        {
            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr($"SaveManager: Could not open file for reading: {FileAccess.GetOpenError()}");
                return null;
            }

            var json = file.GetAsText();
            var saveData = JsonSerializer.Deserialize<GameSaveData>(json);

            GD.Print("SaveManager: Game loaded successfully.");
            return saveData;
        }
        catch (Exception e)
        {
            GD.PrintErr($"SaveManager: Error loading game: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Check if a save file exists.
    /// </summary>
    public static bool SaveExists()
    {
        return FileAccess.FileExists(SavePath);
    }

    /// <summary>
    /// Delete the save file.
    /// </summary>
    public static void DeleteSave()
    {
        if (FileAccess.FileExists(SavePath))
        {
            DirAccess.RemoveAbsolute(SavePath);
            GD.Print("SaveManager: Save file deleted.");
        }
    }
}
