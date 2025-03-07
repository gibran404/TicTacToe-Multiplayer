using UnityEngine;
using UnityEngine.UI;
using PlayFab;
using PlayFab.ClientModels;
using System.Collections;
using TMPro;

public class GameManager : MonoBehaviour
{
    public Button[,] board = new Button[3, 3];       // Board cell buttons.
    private string[] boardState = new string[9];       // "X", "O", or "-" for each cell.
    
    public Text turnText;                             // Displays turn or result.
    public float pollInterval = 3f;                   // Poll interval.
    
    private bool isGameOver = false;
    private int turnCount = 0;
    private bool isMovePending = false;              // Prevents polling during pending update.

    // From matchmaking.
    private string matchId => Matchmaker.MatchId;
    private string myRole => Matchmaker.MyPlayerRole;  // "X" or "O"

    // Current turn ("X" or "O").
    private string currentTurn = "X";

    void Start()
    {
        InitializeBoard();
        // On startup, initialize match state from the cloud.
        InitializeMatchStateFromCloud();
        StartCoroutine(PollGameState());
    }

    // Check cloud for previous state.
    void InitializeMatchStateFromCloud()
    {
        if (string.IsNullOrEmpty(matchId))
        {
            Debug.LogError("Match ID is missing.");
            ResetBoardState();
            return;
        }
        var request = new ExecuteCloudScriptRequest
        {
            FunctionName = "getMatchState",
            FunctionParameter = new { matchId = matchId },
            GeneratePlayStreamEvent = false
        };
        PlayFabClientAPI.ExecuteCloudScript(request, result =>
        {
            if (result.FunctionResult != null)
            {
                var json = result.FunctionResult.ToString();
                MatchStateResponse response = JsonUtility.FromJson<MatchStateResponse>(json);
                if (response != null && response.data != null)
                {
                    string fetchedWinner = response.data.winner;
                    // If previous game ended, reset.
                    if (!string.IsNullOrEmpty(fetchedWinner))
                    {
                        Debug.Log("Previous game ended. Resetting board.");
                        RestartGame();
                    }
                    else
                    {
                        // Continue with previous state.
                        string fetchedBoard = response.data.boardState;
                        string fetchedTurn = response.data.turn;
                        string turnCountStr = response.data.turnCount;
                        int fetchedTurnCount = 0;
                        int.TryParse(turnCountStr, out fetchedTurnCount);
                        if (!string.IsNullOrEmpty(fetchedBoard))
                        {
                            for (int i = 0; i < fetchedBoard.Length && i < boardState.Length; i++)
                            {
                                boardState[i] = fetchedBoard[i].ToString();
                            }
                            currentTurn = fetchedTurn;
                            turnCount = fetchedTurnCount;
                            Debug.Log("Loaded state: " + fetchedBoard + ", Turn: " + fetchedTurn + ", TurnCount: " + fetchedTurnCount);
                            UpdateBoardUI();
                            UpdateTurnText();
                        }
                        else
                        {
                            ResetBoardState();
                        }
                    }
                }
            }
            else
            {
                Debug.LogError("No match state data returned.");
                ResetBoardState();
            }
        }, error =>
        {
            Debug.LogError("Error fetching state: " + error.GenerateErrorReport());
            ResetBoardState();
        });
    }

    void InitializeBoard()
    {
        // Assumes cells named "0,0", "0,1", …, "2,2".
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                string cellName = $"{row},{col}";
                GameObject cellObject = GameObject.Find(cellName);
                if (cellObject)
                {
                    Button cellButton = cellObject.GetComponent<Button>();
                    board[row, col] = cellButton;
                    int index = row * 3 + col;
                    cellButton.onClick.AddListener(() => OnCellClicked(index));
                }
            }
        }
        UpdateTurnText();
    }

    void ResetBoardState()
    {
        for (int i = 0; i < boardState.Length; i++)
            boardState[i] = "-"; // "-" means empty.
        turnCount = 0;
        isGameOver = false;
        currentTurn = "X"; // X always starts.
        UpdateBoardUI();
        UpdateTurnText();
    }

    // Called when a cell is clicked.
    void OnCellClicked(int index)
    {
        if (isGameOver || isMovePending) return;
        if (currentTurn != myRole)
        {
            Debug.Log("Not your turn!");
            return;
        }
        if (boardState[index] != "-")
            return; // Already occupied.

        boardState[index] = myRole;
        turnCount++;
        UpdateBoardUI();

        // Check local win.
        if (CheckWin())
        {
            isGameOver = true;
            turnText.text = "You Win!";
            isMovePending = true;
            UpdateGameStateCloud();
            return;
        }
        else if (turnCount >= 9)
        {
            isGameOver = true;
            turnText.text = "It's a Draw!";
            isMovePending = true;
            UpdateGameStateCloud();
            return;
        }

        currentTurn = (myRole == "X") ? "O" : "X";
        UpdateTurnText();
        isMovePending = true;
        UpdateGameStateCloud();
    }

    // Converts board state array to a single string.
    string BoardStateToString()
    {
        return string.Join("", boardState);
    }

    // Updates board UI.
    void UpdateBoardUI()
    {
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                int index = row * 3 + col;
                Transform xImage = board[row, col].transform.Find("X");
                Transform oImage = board[row, col].transform.Find("O");
                if (xImage != null && oImage != null)
                {
                    xImage.gameObject.SetActive(boardState[index] == "X");
                    oImage.gameObject.SetActive(boardState[index] == "O");
                }
            }
        }
    }

    // Update turn text.
    void UpdateTurnText()
    {
        if (isGameOver) return;
        turnText.text = (currentTurn == myRole) ? "Your Turn!" : "Opponent's Turn";
    }

    // Local win-check.
    bool CheckWin()
    {
        // Rows.
        for (int i = 0; i < 3; i++)
        {
            int start = i * 3;
            if (boardState[start] != "-" && boardState[start] == boardState[start + 1] && boardState[start + 1] == boardState[start + 2])
                return true;
        }
        // Columns.
        for (int i = 0; i < 3; i++)
        {
            if (boardState[i] != "-" && boardState[i] == boardState[i + 3] && boardState[i + 3] == boardState[i + 6])
                return true;
        }
        // Diagonals.
        if (boardState[0] != "-" && boardState[0] == boardState[4] && boardState[4] == boardState[8])
            return true;
        if (boardState[2] != "-" && boardState[2] == boardState[4] && boardState[4] == boardState[6])
            return true;
        return false;
    }

    // Poll the server for updates.
    IEnumerator PollGameState()
    {
        while (true)
        {
            yield return new WaitForSeconds(pollInterval);
            if (isMovePending || currentTurn == myRole || isGameOver)
            {
                Debug.Log("Skipping poll (move pending, my turn, or game over).");
                continue;
            }
            FetchGameStateCloud();
        }
    }

    // Fetch match state from CloudScript.
    void FetchGameStateCloud()
    {
        if (string.IsNullOrEmpty(matchId))
        {
            Debug.LogError("Match ID is missing.");
            return;
        }
        var request = new ExecuteCloudScriptRequest
        {
            FunctionName = "getMatchState",
            FunctionParameter = new { matchId = matchId },
            GeneratePlayStreamEvent = false
        };

        PlayFabClientAPI.ExecuteCloudScript(request, result =>
        {
            if (result.FunctionResult != null)
            {
                var json = result.FunctionResult.ToString();
                MatchStateResponse response = JsonUtility.FromJson<MatchStateResponse>(json);
                if (response != null && response.data != null)
                {
                    string newBoard = response.data.boardState;
                    string newTurn = response.data.turn;
                    string newWinner = response.data.winner;
                    string turnCountStr = response.data.turnCount;
                    int newTurnCount = 0;
                    int.TryParse(turnCountStr, out newTurnCount);
                    
                    Debug.Log("Fetched state: Board: " + newBoard + " | Winner: " + newWinner + " | Turn: " + newTurn + " | TurnCount: " + newTurnCount);
                    
                    if (!string.IsNullOrEmpty(newWinner))
                    {
                        isGameOver = true;
                        turnText.text = (newWinner == myRole) ? "You Win!" : (newWinner == "draw" ? "Draw!" : "You Lose!");
                        for (int i = 0; i < newBoard.Length && i < boardState.Length; i++)
                        {
                            boardState[i] = newBoard[i].ToString();
                        }
                        currentTurn = newTurn;
                        turnCount = newTurnCount;
                        UpdateBoardUI();
                        return;
                    }
                    
                    if (!string.IsNullOrEmpty(newBoard) && newBoard != BoardStateToString())
                    {
                        for (int i = 0; i < newBoard.Length && i < boardState.Length; i++)
                        {
                            boardState[i] = newBoard[i].ToString();
                        }
                        currentTurn = newTurn;
                        turnCount = newTurnCount;
                        UpdateBoardUI();
                        UpdateTurnText();
                    }
                }
            }
            else
            {
                Debug.LogError("No match state data returned.");
            }
        }, error =>
        {
            Debug.LogError("Error fetching state: " + error.GenerateErrorReport());
        });
    }

    // Send current state to server (including turnCount). If turnCount is zero (reset), include player assignments.
    void UpdateGameStateCloud()
    {
        if (string.IsNullOrEmpty(matchId))
        {
            Debug.LogError("Match ID is missing.");
            return;
        }
        
        // For a reset, include playerX and playerO.
        object extra = null;
        if (turnCount == 0)
        {
            string playerX, playerO;
            if (myRole == "X")
            {
                playerX = PlayFabSettings.staticPlayer.EntityId;
                playerO = Matchmaker.OpponentId;
            }
            else
            {
                playerO = PlayFabSettings.staticPlayer.EntityId;
                playerX = Matchmaker.OpponentId;
            }
            extra = new { playerX = playerX, playerO = playerO };
        }
        
        // Build parameters.
        var param = new System.Collections.Generic.Dictionary<string, object>();
        param["matchId"] = matchId;
        param["boardState"] = BoardStateToString();
        param["turn"] = currentTurn;
        param["turnCount"] = turnCount;
        if (extra != null)
        {
            // Merge extra parameters.
            var extraDict = extra as System.Collections.Generic.Dictionary<string, object>;
            // Alternatively, simply add them manually:
            param["playerX"] = (myRole == "X") ? PlayFabSettings.staticPlayer.EntityId : Matchmaker.OpponentId;
            param["playerO"] = (myRole == "X") ? Matchmaker.OpponentId : PlayFabSettings.staticPlayer.EntityId;
        }
        
        var request = new ExecuteCloudScriptRequest
        {
            FunctionName = "updateMatchState",
            FunctionParameter = param,
            GeneratePlayStreamEvent = true
        };

        PlayFabClientAPI.ExecuteCloudScript(request, result =>
        {
            Debug.Log("Updated state on server. Board: " + BoardStateToString());
            isMovePending = false;
        }, error =>
        {
            Debug.LogError("Error updating state: " + error.GenerateErrorReport());
            isMovePending = false;
        });
    }

    // Restart game: reset local state and call the server reset function.
    public void RestartGame()
    {
        ResetBoardState();
        if (string.IsNullOrEmpty(matchId))
        {
            Debug.LogError("Match ID is missing.");
            return;
        }
        // Determine player assignments from Matchmaker.
        string playerX, playerO;
        if (myRole == "X")
        {
            playerX = PlayFabSettings.staticPlayer.EntityId;
            playerO = Matchmaker.OpponentId;
        }
        else
        {
            playerO = PlayFabSettings.staticPlayer.EntityId;
            playerX = Matchmaker.OpponentId;
        }
        var request = new ExecuteCloudScriptRequest
        {
            FunctionName = "resetMatchState",
            FunctionParameter = new { matchId = matchId, playerX = playerX, playerO = playerO },
            GeneratePlayStreamEvent = true
        };
        PlayFabClientAPI.ExecuteCloudScript(request, result =>
        {
            Debug.Log("Match state reset on server.");
        }, error =>
        {
            Debug.LogError("Error resetting state: " + error.GenerateErrorReport());
        });
    }
}

// Data model for JSON parsing.
[System.Serializable]
public class DataWrapper
{
    public string boardState;
    public string turn;
    public string turnCount;
    public string winner;  // "X", "O", "draw", or "".
    public string playerX; // Player ID assigned as X.
    public string playerO; // Player ID assigned as O.
}

[System.Serializable]
public class MatchStateResponse
{
    public string message;
    public DataWrapper data;
}
