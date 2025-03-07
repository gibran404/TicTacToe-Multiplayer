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
    
    public Text turnText;                             // UI text for turn or game result.
    public float pollInterval = 3f;                   // Interval for polling server.
    
    private bool isGameOver = false;
    private int turnCount = 0;
    private bool isMovePending = false;              // Flag to block polling while waiting for an update.

    // Values from matchmaking.
    private string matchId => Matchmaker.MatchId;
    private string myRole => Matchmaker.MyPlayerRole;  // "X" or "O"
    
    // Current turn ("X" or "O").
    private string currentTurn = "X";

    void Start()
    {
        InitializeBoard();
        // Initialize state from the cloud.
        InitializeMatchStateFromCloud();
        StartCoroutine(PollGameState());
    }

    // Checks the cloud for previous match state.
    void InitializeMatchStateFromCloud()
    {
        if (string.IsNullOrEmpty(matchId))
        {
            Debug.LogError("Match ID is missing.");
            ResetBoardState(); // Local default if matchId not set.
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
                    // If a previous game ended, reset the board.
                    if (!string.IsNullOrEmpty(fetchedWinner))
                    {
                        Debug.Log("Previous game ended. Resetting board.");
                        RestartGame();
                    }
                    else
                    {
                        // Continue from previous state.
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
                            Debug.Log("Loaded previous state: " + fetchedBoard + ", Turn: " + fetchedTurn + ", TurnCount: " + fetchedTurnCount);
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
            Debug.LogError("Error fetching match state: " + error.GenerateErrorReport());
            ResetBoardState();
        });
    }

    void InitializeBoard()
    {
        // Assumes cells are named "0,0", "0,1", ..., "2,2".
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
            boardState[i] = "-"; // "-" indicates empty.
        turnCount = 0;
        isGameOver = false;
        currentTurn = "X"; // X starts.
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
            return; // Ignore if cell occupied.

        // Update local state.
        boardState[index] = myRole;
        turnCount++;
        UpdateBoardUI();

        // Check win condition locally.
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

        // Switch turn.
        currentTurn = (myRole == "X") ? "O" : "X";
        UpdateTurnText();
        isMovePending = true;
        UpdateGameStateCloud();
    }

    // Convert board state array to string.
    string BoardStateToString()
    {
        return string.Join("", boardState);
    }

    // Update board UI.
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

    // Update turn text UI.
    void UpdateTurnText()
    {
        if (isGameOver) return;
        turnText.text = (currentTurn == myRole) ? "Your Turn!" : "Opponent's Turn";
    }

    // Local win-check.
    bool CheckWin()
    {
        // Check rows.
        for (int i = 0; i < 3; i++)
        {
            int start = i * 3;
            if (boardState[start] != "-" && boardState[start] == boardState[start + 1] && boardState[start + 1] == boardState[start + 2])
                return true;
        }
        // Check columns.
        for (int i = 0; i < 3; i++)
        {
            if (boardState[i] != "-" && boardState[i] == boardState[i + 3] && boardState[i + 3] == boardState[i + 6])
                return true;
        }
        // Check diagonals.
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
                Debug.LogError("No game state data returned.");
            }
        }, error =>
        {
            Debug.LogError("Error fetching state: " + error.GenerateErrorReport());
        });
    }

    // Send current state (including turnCount) to the server.
    void UpdateGameStateCloud()
    {
        if (string.IsNullOrEmpty(matchId))
        {
            Debug.LogError("Match ID is missing.");
            return;
        }
        var request = new ExecuteCloudScriptRequest
        {
            FunctionName = "updateMatchState",
            FunctionParameter = new
            {
                matchId = matchId,
                boardState = BoardStateToString(),
                turn = currentTurn,
                turnCount = turnCount
            },
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

    // Restart the game: reset local state and call the server reset.
    public void RestartGame()
    {
        ResetBoardState();
        if (string.IsNullOrEmpty(matchId))
        {
            Debug.LogError("Match ID is missing.");
            return;
        }
        var request = new ExecuteCloudScriptRequest
        {
            FunctionName = "resetMatchState",
            FunctionParameter = new { matchId = matchId },
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
}

[System.Serializable]
public class MatchStateResponse
{
    public string message;
    public DataWrapper data;
}
