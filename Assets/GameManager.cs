using UnityEngine;
using UnityEngine.UI;
using PlayFab;
using PlayFab.ClientModels;
using System.Collections;
using TMPro;

public class GameManager : MonoBehaviour
{
    public Button[,] board = new Button[3, 3];       // Button references for board cells.
    private string[] boardState = new string[9];       // Stores "X", "O", or "-" for each cell.
    
    public Text turnText;                             // UI text to show current turn or result.
    public float pollInterval = 3f;                   // Seconds between server polling.
    
    private bool isGameOver = false;
    private int turnCount = 0;

    // Values from matchmaking.
    private string matchId => Matchmaker.MatchId;
    private string myRole => Matchmaker.MyPlayerRole;  // "X" or "O"
    
    // Current turn stored as "X" or "O".
    private string currentTurn = "X";

    void Start()
    {
        InitializeBoard();
        ResetBoardState();
        // Begin polling server for match state updates.
        StartCoroutine(PollGameState());
    }

    void InitializeBoard()
    {
        // Assumes board cells are named "0,0", "0,1", …, "2,2".
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
                    int index = row * 3 + col; // Convert 2D position to 1D index.
                    cellButton.onClick.AddListener(() => OnCellClicked(index));
                }
            }
        }
        UpdateTurnText();
    }

    void ResetBoardState()
    {
        for (int i = 0; i < boardState.Length; i++)
            boardState[i] = "-"; // "-" indicates an empty cell.
        turnCount = 0;
        isGameOver = false;
        currentTurn = "X"; // X always starts.
        UpdateBoardUI();
        UpdateTurnText();
    }

    // Called when a board cell is clicked.
    void OnCellClicked(int index)
    {
        if (isGameOver) return;

        // Allow move only if it's this player's turn.
        if (currentTurn != myRole)
        {
            Debug.Log("Not your turn!");
            return;
        }

        // If cell is already occupied, ignore.
        if (boardState[index] != "-")
            return;

        // Update local board state.
        boardState[index] = myRole;
        turnCount++;

        // Switch turn locally.
        currentTurn = (myRole == "X") ? "O" : "X";
        UpdateBoardUI();
        UpdateTurnText();

        // Update the shared game state on the server.
        UpdateGameStateCloud();
    }

    // Convert local board state array to a single string.
    string BoardStateToString()
    {
        return string.Join("", boardState);
    }

    // Update the board UI based on boardState.
    void UpdateBoardUI()
    {
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                int index = row * 3 + col;
                // Assumes each button has child GameObjects named "X" and "O".
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
        if (isGameOver)
            return;
        turnText.text = (currentTurn == myRole) ? "Your Turn!" : "Opponent's Turn";
    }

    // Poll the server for match state updates at regular intervals.
    IEnumerator PollGameState()
    {
        while (true)
        {
            yield return new WaitForSeconds(pollInterval);
            FetchGameStateCloud();
        }
    }

    // Fetch current match state from CloudScript.
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
                    Debug.Log("Fetched board state: " + newBoard + " | Winner: " + newWinner);

                    // If a winner is found, update UI accordingly.
                    if (!string.IsNullOrEmpty(newWinner))
                    {
                        isGameOver = true;
                        turnText.text = (newWinner == myRole) ? "You Win!" : "You Lose!";
                        // Update local board state.
                        for (int i = 0; i < newBoard.Length && i < boardState.Length; i++)
                        {
                            boardState[i] = newBoard[i].ToString();
                        }
                        currentTurn = newTurn;
                        UpdateBoardUI();
                        return;
                    }

                    // Otherwise, update local board state if it differs.
                    if (!string.IsNullOrEmpty(newBoard) && newBoard != BoardStateToString())
                    {
                        for (int i = 0; i < newBoard.Length && i < boardState.Length; i++)
                        {
                            boardState[i] = newBoard[i].ToString();
                        }
                        currentTurn = newTurn;
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
            Debug.LogError("Error fetching game state: " + error.GenerateErrorReport());
        });
    }

    // Update the game state on the server via CloudScript.
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
                turn = currentTurn
            },
            GeneratePlayStreamEvent = true
        };

        PlayFabClientAPI.ExecuteCloudScript(request, result =>
        {
            Debug.Log("Game state updated via CloudScript.");
            Debug.Log("Board state: " + BoardStateToString());
        }, error =>
        {
            Debug.LogError("Error updating game state: " + error.GenerateErrorReport());
        });
    }

    // Public method to restart the game.
    public void RestartGame()
    {
        ResetBoardState();
        UpdateGameStateCloud();
    }
}

[System.Serializable]
public class DataWrapper
{
    public string boardState;
    public string turn;
    public string winner;  // Winner ("X" or "O") if determined.
}

[System.Serializable]
public class MatchStateResponse
{
    public string message;
    public DataWrapper data;
}
