using UnityEngine;
using UnityEngine.UI;
using PlayFab;
using PlayFab.ClientModels;
using System.Collections;
using TMPro;

public class GameManager : MonoBehaviour
{
    public Button[,] board = new Button[3, 3]; // Button references for cells.
    private string[] boardState = new string[9]; // Store "X", "O", or " " for each cell.
    
    public Text turnText;  // UI text to show whose turn it is.
    public float pollInterval = 3f; // Seconds between polling the server.
    
    private bool isGameOver = false;
    private int turnCount = 0;

    // These variables come from matchmaking.
    private string matchId => Matchmaker.MatchId;  
    private string myRole => Matchmaker.MyPlayerRole;  // "X" or "O"
    
    // The current turn is stored as a string: "X" or "O".
    private string currentTurn = "X";

    void Start()
    {
        InitializeBoard();
        ResetBoardState();
        // Start polling for game state updates.
        StartCoroutine(PollGameState());

        for (int i = 0; i < boardState.Length; i++)
            boardState[i] = "-";
    }

    void InitializeBoard()
    {
        // Assumes that your board cells are named "0,0", "0,1", …, "2,2".
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
                    int index = row * 3 + col;  // Convert 2D position to 1D index.
                    cellButton.onClick.AddListener(() => OnCellClicked(index));
                }
            }
        }
        UpdateTurnText();
    }

    void ResetBoardState()
    {
        for (int i = 0; i < boardState.Length; i++)
            boardState[i] = "-"; // Initialize all cells as empty.
        turnCount = 0;
        isGameOver = false;
        currentTurn = "X"; // Let X always start.
        UpdateBoardUI();
    }

    // Called when a cell is clicked.
    void OnCellClicked(int index)
    {
        if (isGameOver) return;

        // Only allow the move if it's this player's turn.
        if (currentTurn != myRole)
        {
            Debug.Log("Not your turn!");
            return;
        }

        if (boardState[index] != "-")
            return; // Ignore if cell is not empty.

        // Update local state.
        boardState[index] = myRole;
        turnCount++;

        // Update local turn: switch turn.
        currentTurn = (myRole == "X") ? "O" : "X";
        UpdateBoardUI();
        UpdateTurnText();

        // Save the updated state to PlayFab using CloudScript.
        UpdateGameStateCloud();

        // Check for win/draw.
        if (CheckWin())
        {
            if (currentTurn == myRole) // since checking after switching turn
                turnText.text = "You Lose!";
            else
                turnText.text = $"You Win!";
            isGameOver = true;
            return;
        }
        if (turnCount >= 9)
        {
            isGameOver = true;
            turnText.text = "It's a draw!";
            return;
        }
    }

    // Convert the board state array to a single string.
    string BoardStateToString()
    {
        Debug.Log("boardstate to string: " + string.Join("", boardState));
        return string.Join("", boardState);
    }

    // Update the UI based on boardState.
    void UpdateBoardUI()
    {
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                int index = row * 3 + col;
                
                // Assume each button has child GameObjects named "X" and "O" for the symbols.
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

    void UpdateTurnText()
    {
        turnText.text = $"Turn: {currentTurn}";
    }

    bool CheckWin()
    {
        // Check rows, columns, and diagonals
        for (int i = 0; i < 3; i++)
        {
            if (boardState[i * 3] != "-" && boardState[i * 3] == boardState[i * 3 + 1] && boardState[i * 3 + 1] == boardState[i * 3 + 2]) // horizontal
                return true;
            if (boardState[i] != "-" && boardState[i] == boardState[i + 3] && boardState[i + 3] == boardState[i + 6]) // vertical
                return true;
        }
        if (boardState[0] != "-" && boardState[0] == boardState[4] && boardState[4] == boardState[8]) // diagonal
            return true;
        if (boardState[2] != "-" && boardState[2] == boardState[4] && boardState[4] == boardState[6]) // diagonal
            return true;

        return false;
    }

    // bool CheckWin()
    // {
    //     // Check rows, columns, and diagonals
    //     for (int i = 0; i < 3; i++)
    //     {
    //         if (boardState[i, 0] != "-" && boardState[i, 0] == boardState[i, 1] && boardState[i, 1] == boardState[i, 2]) // horizontal
    //             return true;
    //         if (boardState[0, i] != null && boardState[0, i] == boardState[1, i] && boardState[1, i] == boardState[2, i]) // vertical
    //             return true;
    //     }
    //     if (boardState[0, 0] != null && boardState[0, 0] == boardState[1, 1] && boardState[1, 1] == boardState[2, 2]) // diagonal
    //         return true;
    //     if (boardState[0, 2] != null && boardState[0, 2] == boardState[1, 1] && boardState[1, 1] == boardState[2, 0]) // diagonal
    //         return true;

    //     return false;
    // }

    // Update the game state on PlayFab via CloudScript.
    void UpdateGameStateCloud()
    {
        Debug.Log("Updating game state...");
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
            //print the board state
            Debug.Log("Board state: " + BoardStateToString());
        }, error =>
        {
            Debug.LogError("Error updating game state: " + error.GenerateErrorReport());
        });
    }

    // Poll the game state from PlayFab at intervals.
    IEnumerator PollGameState()
    {
        while (true)
        {
            yield return new WaitForSeconds(pollInterval);
            FetchGameStateCloud();
        }
    }

    // Fetch the current game state from CloudScript.
    void FetchGameStateCloud()
    {
        if (currentTurn == myRole)
        {
            Debug.Log("Skipping fetch game state because it's own turn.");
            return;
        }
        Debug.Log("Fetching game state...");
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
            {                // Parse the returned JSON. Expecting an object with Data.boardState and Data.turn.
                var json = result.FunctionResult.ToString();
                // You can use a JSON library or simple parsing; here we use JsonUtility for simplicity.
                MatchStateResponse response = JsonUtility.FromJson<MatchStateResponse>(json);
                if (response != null && response.data != null)
                {
                    string newBoard = response.data.boardState;
                    string newTurn = response.data.turn;
                    // Print the fetched board state.
                    Debug.Log("Fetched board state: " + newBoard);
                    // If the cloud state differs from local state, update local state.
                    if (!string.IsNullOrEmpty(newBoard) && newBoard != BoardStateToString())
                    {
                        // Update local boardState.
                        for (int i = 0; i < newBoard.Length && i < boardState.Length; i++)
                        {
                            boardState[i] = newBoard[i].ToString();
                        } 
                        currentTurn = newTurn;
                        Debug.Log("boardstate: " + boardState);
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

    // Helper class to parse CloudScript response.
    [System.Serializable]
    public class MatchStateResponse
    {
        public string message;
        public DataWrapper data;
    }

    [System.Serializable]
    public class DataWrapper
    {
        public string boardState;
        public string turn;
    }

    // Public method to restart the game.
    public void RestartGame()
    {
        ResetBoardState();
        UpdateGameStateCloud();
    }
}


[System.Serializable]
public class MatchStateResponse
{
    public string message;
    public DataWrapper data;
}

[System.Serializable]
public class DataWrapper
{
    public string boardState;
    public string turn;
}
