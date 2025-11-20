using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// PlayerData: Holds persistent data for the current run and across sessions.
/// </summary>
[System.Serializable]
public class PlayerData
{
    // ==================== PERSISTENT DATA (Saved Between Sessions) ====================
    
    [Header("Persistent Statistics")]
    public int highScore = 0;
    public int totalRuns = 0;
    public int totalVictories = 0;
    public float totalPlayTime = 0f;
    public int highestRoundReached = 0;
    public int lifetimeCoins = 0;           // Total coins ever earned
    
    [Header("Unlockables")]
    public List<string> unlockedItems = new List<string>();
    public List<string> unlockedBoards = new List<string>();
    public string currentBoard = "default";

    // ==================== RUN DATA (Resets Each Run) ====================
    
    [Header("Current Run Data")]
    public int coins = 0;                   // Coins available to spend this run
    public List<string> itemsOwnedThisRun = new List<string>();  // Items bought from store
    public Dictionary<string, bool> activeItems = new Dictionary<string, bool>();
    
    [Header("Run Statistics")]
    public int bestScoreThisRun = 0;
    public int totalTricksThisRun = 0;
    public int totalCombosThisRun = 0;

    // ==================== CONSTRUCTOR ====================
    
    public PlayerData()
    {
        // Initialize with defaults
        ResetRunData();
    }

    // ==================== RUN MANAGEMENT ====================
    
    /// <summary>
    /// Reset data that should start fresh each run
    /// </summary>
    public void ResetRunData()
    {
        coins = 0;
        itemsOwnedThisRun.Clear();
        activeItems.Clear();
        bestScoreThisRun = 0;
        totalTricksThisRun = 0;
        totalCombosThisRun = 0;
    }

    /// <summary>
    /// Add an item purchased during this run
    /// </summary>
    public void AddItemToRun(string itemId)
    {
        if (!itemsOwnedThisRun.Contains(itemId))
        {
            itemsOwnedThisRun.Add(itemId);
        }
    }

    /// <summary>
    /// Check if player owns an item this run
    /// </summary>
    public bool HasItemThisRun(string itemId)
    {
        return itemsOwnedThisRun.Contains(itemId);
    }

    /// <summary>
    /// Activate/deactivate an item effect
    /// </summary>
    public void SetItemActive(string itemId, bool active)
    {
        activeItems[itemId] = active;
    }

    /// <summary>
    /// Check if an item effect is currently active
    /// </summary>
    public bool IsItemActive(string itemId)
    {
        return activeItems.ContainsKey(itemId) && activeItems[itemId];
    }

    // ==================== PERSISTENT UNLOCKS ====================
    
    /// <summary>
    /// Unlock an item permanently (persists across runs)
    /// </summary>
    public void UnlockItem(string itemId)
    {
        if (!unlockedItems.Contains(itemId))
        {
            unlockedItems.Add(itemId);
            SaveToPlayerPrefs();
        }
    }

    /// <summary>
    /// Check if an item is permanently unlocked
    /// </summary>
    public bool IsItemUnlocked(string itemId)
    {
        return unlockedItems.Contains(itemId);
    }

    /// <summary>
    /// Unlock a board permanently
    /// </summary>
    public void UnlockBoard(string boardId)
    {
        if (!unlockedBoards.Contains(boardId))
        {
            unlockedBoards.Add(boardId);
            SaveToPlayerPrefs();
        }
    }

    /// <summary>
    /// Check if a board is unlocked
    /// </summary>
    public bool IsBoardUnlocked(string boardId)
    {
        return unlockedBoards.Contains(boardId) || boardId == "default";
    }

    /// <summary>
    /// Set the active board
    /// </summary>
    public void SetCurrentBoard(string boardId)
    {
        if (IsBoardUnlocked(boardId))
        {
            currentBoard = boardId;
            SaveToPlayerPrefs();
        }
    }

    // ==================== STATISTICS TRACKING ====================
    
    /// <summary>
    /// Update high score if current score is higher
    /// </summary>
    public void UpdateHighScore(int score)
    {
        if (score > highScore)
        {
            highScore = score;
            SaveToPlayerPrefs();
        }

        if (score > bestScoreThisRun)
        {
            bestScoreThisRun = score;
        }
    }

    /// <summary>
    /// Add to lifetime coins
    /// </summary>
    public void AddLifetimeCoins(int amount)
    {
        lifetimeCoins += amount;
    }

    // ==================== SAVE/LOAD SYSTEM ====================
    
    /// <summary>
    /// Save persistent data to PlayerPrefs
    /// </summary>
    public void SaveToPlayerPrefs()
    {
        // Statistics
        PlayerPrefs.SetInt("HighScore", highScore);
        PlayerPrefs.SetInt("TotalRuns", totalRuns);
        PlayerPrefs.SetInt("TotalVictories", totalVictories);
        PlayerPrefs.SetFloat("TotalPlayTime", totalPlayTime);
        PlayerPrefs.SetInt("HighestRound", highestRoundReached);
        PlayerPrefs.SetInt("LifetimeCoins", lifetimeCoins);

        // Current board
        PlayerPrefs.SetString("CurrentBoard", currentBoard);

        // Unlocked items (stored as comma-separated string)
        PlayerPrefs.SetString("UnlockedItems", string.Join(",", unlockedItems));
        PlayerPrefs.SetString("UnlockedBoards", string.Join(",", unlockedBoards));

        PlayerPrefs.Save();
        Debug.Log("PlayerData: Saved to PlayerPrefs");
    }

    /// <summary>
    /// Load persistent data from PlayerPrefs
    /// </summary>
    public void LoadFromPlayerPrefs()
    {
        // Statistics
        highScore = PlayerPrefs.GetInt("HighScore", 0);
        totalRuns = PlayerPrefs.GetInt("TotalRuns", 0);
        totalVictories = PlayerPrefs.GetInt("TotalVictories", 0);
        totalPlayTime = PlayerPrefs.GetFloat("TotalPlayTime", 0f);
        highestRoundReached = PlayerPrefs.GetInt("HighestRound", 0);
        lifetimeCoins = PlayerPrefs.GetInt("LifetimeCoins", 0);

        // Current board
        currentBoard = PlayerPrefs.GetString("CurrentBoard", "default");

        // Unlocked items
        string unlockedItemsStr = PlayerPrefs.GetString("UnlockedItems", "");
        if (!string.IsNullOrEmpty(unlockedItemsStr))
        {
            unlockedItems = new List<string>(unlockedItemsStr.Split(','));
        }

        string unlockedBoardsStr = PlayerPrefs.GetString("UnlockedBoards", "");
        if (!string.IsNullOrEmpty(unlockedBoardsStr))
        {
            unlockedBoards = new List<string>(unlockedBoardsStr.Split(','));
        }

        Debug.Log($"PlayerData: Loaded from PlayerPrefs - High Score: {highScore}, Total Runs: {totalRuns}");
    }

    /// <summary>
    /// Delete all saved data (for testing or reset button)
    /// </summary>
    public void ClearAllData()
    {
        PlayerPrefs.DeleteAll();
        
        // Reset to defaults
        highScore = 0;
        totalRuns = 0;
        totalVictories = 0;
        totalPlayTime = 0f;
        highestRoundReached = 0;
        lifetimeCoins = 0;
        unlockedItems.Clear();
        unlockedBoards.Clear();
        currentBoard = "default";
        ResetRunData();

        Debug.Log("PlayerData: All data cleared");
    }

    // ==================== DEBUG & UTILITY ====================
    
    /// <summary>
    /// Get a formatted statistics summary
    /// </summary>
    public string GetStatsSummary()
    {
        return $"=== Player Statistics ===\n" +
               $"High Score: {highScore}\n" +
               $"Total Runs: {totalRuns}\n" +
               $"Victories: {totalVictories}\n" +
               $"Highest Round: {highestRoundReached}\n" +
               $"Play Time: {FormatTime(totalPlayTime)}\n" +
               $"Lifetime Coins: {lifetimeCoins}\n" +
               $"Unlocked Items: {unlockedItems.Count}";
    }

    private string FormatTime(float timeInSeconds)
    {
        int hours = Mathf.FloorToInt(timeInSeconds / 3600f);
        int minutes = Mathf.FloorToInt((timeInSeconds % 3600f) / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
        
        if (hours > 0)
            return $"{hours}h {minutes}m {seconds}s";
        else
            return $"{minutes}m {seconds}s";
    }
}