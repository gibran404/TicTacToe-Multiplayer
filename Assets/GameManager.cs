using UnityEngine;
using UnityEngine.UI;
using PlayFab;
using PlayFab.ClientModels;
using System;
using System.Collections;
using TMPro;

[Serializable]
public class DataWrapper
{
    public string boardState;
    public string turn;
    public string turnCount;
    public string winner;           // "X", "O", "draw", or "".
    public string playerX;          // Assigned player for X.
    public string playerO;          // Assigned player for O.
    public string playerXlastActive;// Last active timestamp for player X.
    public string playerOlastActive;// Last active timestamp for player O.
}

[Serializable]
public class MatchStateResponse
{
    public string message;
    public DataWrapper data;
}

[Serializable]
public class HeartbeatData
{
    public float pingDifference;
}

[Serializable]
public class HeartbeatResponse
{
    public string message;
    public HeartbeatData data;
}

public class GameManager : MonoBehaviour
{
    // UI references
    public Text turnText;         // Displays turn or result.
    public GameObject RestartButton;
    public float pollInterval = 3f;       // Polling interval in seconds.
    public float heartbeatInterval = 5f;  // Heartbeat interval in seconds.

    // Board data
    private Button[,] board = new Button[3, 3];  // Board cell buttons.
    private string[] boardState = new string[9]; // "X", "O", or "-" for each cell.

    // Game state
    private bool isGameOver = false;
    private int turnCount = 0;
    private bool isMovePending = false;  // Prevents simultaneous update/polling.
    private string currentTurn = "X";    // "X" or "O" indicates whose turn.

    // Match info (set via Matchmaker)
    private string matchId => Matchmaker.MatchId;
    private string myRole => Matchmaker.MyPlayerRole;

    void Start()
    {
        InitializeBoard();
        InitializeMatchStateFromCloud();
        StartCoroutine(PollGameStateRoutine());
        StartCoroutine(HeartbeatRoutine());
    }

    void Update()
    {
        RestartButton.SetActive(isGameOver);
    }

    // Initialize board by finding buttons named "0,0", "0,1", etc.
    void InitializeBoard()
    {
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                string cellName = $"{row},{col}";
                GameObject cellObj = GameObject.Find(cellName);
                if (cellObj != null)
                {
                    Button btn = cellObj.GetComponent<Button>();
                    board[row, col] = btn;
                    int index = row * 3 + col;
                    btn.onClick.AddListener(() => OnCellClicked(index));
                }
            }
        }
        UpdateTurnText();
    }

    // Reset local board state.
    void ResetBoardState()
    {
        for (int i = 0; i < boardState.Length; i++)
            boardState[i] = "-";
        turnCount = 0;
        isGameOver = false;
        currentTurn = "X";
        UpdateBoardUI();
        UpdateTurnText();
    }

    // Fetch match state from cloud on startup.
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
                MatchStateResponse response = JsonUtility.FromJson<MatchStateResponse>(result.FunctionResult.ToString());
                if (response != null && response.data != null)
                {
                    if (!string.IsNullOrEmpty(response.data.winner))
                    {
                        Debug.Log("Previous game ended. Resetting board.");
                        RestartGame();
                    }
                    else if (!string.IsNullOrEmpty(response.data.boardState))
                    {
                        LoadState(response.data.boardState, response.data.turn, response.data.turnCount);
                    }
                    else
                    {
                        ResetBoardState();
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

    // Load local state from cloud data.
    void LoadState(string boardStr, string turnStr, string turnCountStr)
    {
        for (int i = 0; i < boardStr.Length && i < boardState.Length; i++)
            boardState[i] = boardStr[i].ToString();
        currentTurn = turnStr;
        int.TryParse(turnCountStr, out turnCount);
        Debug.Log($"Loaded state: {boardStr}, Turn: {turnStr}, TurnCount: {turnCount}");
        UpdateBoardUI();
        UpdateTurnText();
    }

    // Called when a board cell is clicked.
    void OnCellClicked(int index)
    {
        if (isGameOver || isMovePending) return;
        if (currentTurn != myRole)
        {
            Debug.Log("Not your turn!");
            return;
        }
        if (boardState[index] != "-") return;

        boardState[index] = myRole;
        turnCount++;
        UpdateBoardUI();

        if (CheckWin())
        {
            isGameOver = true;
            turnText.text = "You Win!";
        }
        else if (turnCount >= 9)
        {
            isGameOver = true;
            turnText.text = "It's a Draw!";
        }
        else
        {
            currentTurn = (myRole == "X") ? "O" : "X";
            UpdateTurnText();
        }

        isMovePending = true;
        UpdateGameStateCloud();
    }

    // Convert board state array to string.
    string BoardStateToString() => string.Join("", boardState);

    // Update board UI visuals.
    void UpdateBoardUI()
    {
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                int index = row * 3 + col;
                Transform xImg = board[row, col].transform.Find("X");
                Transform oImg = board[row, col].transform.Find("O");
                if (xImg != null && oImg != null)
                {
                    xImg.gameObject.SetActive(boardState[index] == "X");
                    oImg.gameObject.SetActive(boardState[index] == "O");
                }
            }
        }
    }

    // Update turn text UI.
    void UpdateTurnText()
    {
        if (!isGameOver)
            turnText.text = (currentTurn == myRole) ? "Your Turn!" : "Opponent's Turn";
    }

    // Check win condition locally for a 3x3 board.
    bool CheckWin()
    {
        for (int i = 0; i < 3; i++)
        {
            int start = i * 3;
            if (boardState[start] != "-" &&
                boardState[start] == boardState[start + 1] &&
                boardState[start + 1] == boardState[start + 2])
                return true;
        }
        for (int i = 0; i < 3; i++)
        {
            if (boardState[i] != "-" &&
                boardState[i] == boardState[i + 3] &&
                boardState[i + 3] == boardState[i + 6])
                return true;
        }
        if (boardState[0] != "-" &&
            boardState[0] == boardState[4] &&
            boardState[4] == boardState[8])
            return true;
        if (boardState[2] != "-" &&
            boardState[2] == boardState[4] &&
            boardState[4] == boardState[6])
            return true;
        return false;
    }

    // Poll the server for match state updates.
    IEnumerator PollGameStateRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(pollInterval);
            if ((isMovePending || currentTurn == myRole) && !isGameOver)
            {
                // Debug.Log("Skipping poll (move pending or my turn).");
                continue;
            }
            FetchGameStateCloud();
        }
    }

    // Heartbeat routine: sends a heartbeat every heartbeatInterval seconds.
    IEnumerator HeartbeatRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(heartbeatInterval);
            SendHeartbeat();
        }
    }

    // Send heartbeat to the server and process the ping difference.
    void SendHeartbeat()
    {
        if (string.IsNullOrEmpty(matchId))
        {
            Debug.LogError("Match ID is missing, cannot send heartbeat.");
            return;
        }
        var request = new ExecuteCloudScriptRequest
        {
            FunctionName = "heartbeat",
            FunctionParameter = new { matchId = matchId, role = myRole },
            GeneratePlayStreamEvent = false
        };
        PlayFabClientAPI.ExecuteCloudScript(request, result =>
        {
            if (result.FunctionResult != null)
            {
                HeartbeatResponse hbResponse = JsonUtility.FromJson<HeartbeatResponse>(result.FunctionResult.ToString());
                if (hbResponse != null && hbResponse.data != null)
                {
                    float pingDiff = hbResponse.data.pingDifference;
                    Debug.Log("Ping difference between players: " + pingDiff + " seconds");
                    if (pingDiff > 10)
                    {
                        Debug.Log("Opponent offline");
                    }
                    else
                    {

                    }
                }
            }
            else
            {
                Debug.LogError("Heartbeat: No result returned.");
            }
        }, error =>
        {
            Debug.LogError("Error sending heartbeat: " + error.GenerateErrorReport());
        });
    }

    // Fetch match state from the server.
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
            if (result.FunctionResult == null)
            {
                Debug.LogError("No match state data returned.");
                return;
            }
            MatchStateResponse response = JsonUtility.FromJson<MatchStateResponse>(result.FunctionResult.ToString());
            if (response?.data != null)
            {
                string newBoard = response.data.boardState;
                string newTurn = response.data.turn;
                string newWinner = response.data.winner;
                int newTurnCount = 0;
                int.TryParse(response.data.turnCount, out newTurnCount);
                // Debug.Log($"Fetched state: Board: {newBoard}, Winner: {newWinner}, Turn: {newTurn}, TurnCount: {newTurnCount}");
                
                if (!string.IsNullOrEmpty(newWinner))
                {
                    isGameOver = true;
                    turnText.text = (newWinner == myRole) ? "You Win!" : (newWinner == "draw" ? "Draw!" : "You Lose!");
                    LoadState(newBoard, newTurn, response.data.turnCount);
                    return;
                }
                else
                {
                    isGameOver = false;
                    turnText.text = (newTurn == myRole) ? "Your Turn!" : "Opponent's Turn";
                }
                if (!string.IsNullOrEmpty(newBoard) && newBoard != BoardStateToString())
                {
                    LoadState(newBoard, newTurn, response.data.turnCount);
                }
            }
        }, error =>
        {
            Debug.LogError("Error fetching state: " + error.GenerateErrorReport());
        });
    }

    // Update the match state on the server.
    void UpdateGameStateCloud()
    {
        if (string.IsNullOrEmpty(matchId))
        {
            Debug.LogError("Match ID is missing.");
            return;
        }
        var param = new System.Collections.Generic.Dictionary<string, object>
        {
            { "matchId", matchId },
            { "boardState", BoardStateToString() },
            { "turn", currentTurn },
            { "turnCount", turnCount }
        };
        if (turnCount == 0)
        {
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

    // Restart game: reset local state and call cloud reset.
    public void RestartGame()
    {
        Debug.Log("Restarting game.");
        ResetBoardState();
        isGameOver = false;
        if (string.IsNullOrEmpty(matchId))
        {
            Debug.LogError("Match ID is missing.");
            return;
        }
        string playerX = (myRole == "X") ? PlayFabSettings.staticPlayer.EntityId : Matchmaker.OpponentId;
        string playerO = (myRole == "X") ? Matchmaker.OpponentId : PlayFabSettings.staticPlayer.EntityId;
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
