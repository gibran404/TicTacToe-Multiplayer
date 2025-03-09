using UnityEngine;
using UnityEngine.UI;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.MultiplayerModels;
using System.Collections.Generic;
using TMPro;

// Alias the EntityKey from MultiplayerModels to avoid ambiguity.
using MultiplayerEntityKey = PlayFab.MultiplayerModels.EntityKey;

public class Matchmaker : MonoBehaviour
{
    public TMP_Text matchmakingStatus;
    public GameObject gamePanel;
    public GameObject introPanel;

    public static string MatchId;       // Shared group ID (computed from player IDs)
    public static string MyPlayerRole;  // "X" or "O"
    public static string OpponentId;    // Opponent's ID

    private string ticketId;
    private bool isMatchmaking = false;

    // Start the matchmaking process.
    public void StartMatchmaking()
    {
        // CancelAllTickets();
        matchmakingStatus.text = "Searching for a match...";
        isMatchmaking = true;

        var request = new CreateMatchmakingTicketRequest
        {
            Creator = new MatchmakingPlayer
            {
                Entity = new MultiplayerEntityKey
                {
                    Id = PlayFabSettings.staticPlayer.EntityId,
                    Type = PlayFabSettings.staticPlayer.EntityType
                }
            },
            GiveUpAfterSeconds = 60,
            QueueName = "TicTacToeQueue"
        };

        PlayFabMultiplayerAPI.CreateMatchmakingTicket(request, OnTicketCreated, OnMatchmakingFailed);
    }

    public void LeaveMatchmaking()
    {
        CancelAllTickets();
        isMatchmaking = false;
        matchmakingStatus.text = "";
    }

    // Called when a matchmaking ticket is successfully created.
    void OnTicketCreated(CreateMatchmakingTicketResult result)
    {
        ticketId = result.TicketId;
        InvokeRepeating(nameof(CheckMatchmakingStatus), 5f, 5f);
    }

    // Poll the ticket status.
    void CheckMatchmakingStatus()
    {
        if (!isMatchmaking) return;

        var request = new GetMatchmakingTicketRequest
        {
            TicketId = ticketId,
            QueueName = "TicTacToeQueue"
        };

        PlayFabMultiplayerAPI.GetMatchmakingTicket(request, OnMatchStatusChecked, OnMatchmakingFailed);
    }

    void OnMatchStatusChecked(GetMatchmakingTicketResult result)
    {
        if (result.Status == "Matched")
        {
            isMatchmaking = false;
            CancelInvoke(nameof(CheckMatchmakingStatus));

            var matchRequest = new GetMatchRequest
            {
                MatchId = result.MatchId,
                QueueName = "TicTacToeQueue"
            };

            PlayFabMultiplayerAPI.GetMatch(matchRequest, OnMatchFound, OnMatchmakingFailed);
        }
    }

    // Called when a match is found.
    void OnMatchFound(GetMatchResult result)
    {
        Debug.Log($"Match Found: {JsonUtility.ToJson(result)}");

        if (result.Members != null && result.Members.Count >= 2)
        {
            string player1 = result.Members[0].Entity.Id;
            string player2 = result.Members[1].Entity.Id;
            // Compute shared group ID by sorting player IDs.
            string sharedGroupId = (player1.CompareTo(player2) <= 0)
                ? player1 + "_" + player2
                : player2 + "_" + player1;
            MatchId = sharedGroupId;

            // Assign roles.
            if (player1 == PlayFabSettings.staticPlayer.EntityId)
            {
                MyPlayerRole = "X";
                OpponentId = player2;
                matchmakingStatus.text = $"You are X";
            }
            else
            {
                MyPlayerRole = "O";
                OpponentId = player1;
                matchmakingStatus.text = $"You are O";
            }
            Debug.Log("SharedGroupId: " + MatchId);
            Invoke("EnableGamePanel", 1f);
        }
        else
        {
            matchmakingStatus.text = "Error: Match missing players.";
        }
    }

    // Called when matchmaking fails.
    void OnMatchmakingFailed(PlayFabError error)
    {
        matchmakingStatus.text = "Matchmaking Failed: " + error.GenerateErrorReport();
    }

    // Removes the player from the match and returns to the lobby.
    public void ReturnToLobby()
    {
        Debug.Log("ReturnToLobby called. Removing player from match...");
        LeaveMatchmaking();

        var request = new ExecuteCloudScriptRequest
        {
            FunctionName = "leaveMatch", // CloudScript function that removes player from the shared group.
            FunctionParameter = new { matchId = MatchId, playerId = PlayFabSettings.staticPlayer.EntityId },
            GeneratePlayStreamEvent = false
        };

        PlayFabClientAPI.ExecuteCloudScript(request, result =>
        {
            Debug.Log("Player successfully removed from match on server.");
            // Clear match-related static variables.
            MatchId = "";
            MyPlayerRole = "";
            OpponentId = "";
            matchmakingStatus.text = "";
            // Disable game panel and enable intro panel.
            if (gamePanel != null) gamePanel.SetActive(false);
            if (introPanel != null) introPanel.SetActive(true);
        },
        error =>
        {
            Debug.LogError("Error leaving match: " + error.GenerateErrorReport());
        });
    }

    // Cancels any active matchmaking tickets.
    void CancelAllTickets()
    {
        var request = new ListMatchmakingTicketsForPlayerRequest
        {
            Entity = new MultiplayerEntityKey
            {
                Id = PlayFabSettings.staticPlayer.EntityId,
                Type = PlayFabSettings.staticPlayer.EntityType
            },
            QueueName = "TicTacToeQueue"
        };

        PlayFabMultiplayerAPI.ListMatchmakingTicketsForPlayer(request, result =>
        {
            foreach (var ticket in result.TicketIds)
            {
                PlayFabMultiplayerAPI.CancelMatchmakingTicket(new CancelMatchmakingTicketRequest
                {
                    TicketId = ticket,
                    QueueName = "TicTacToeQueue"
                },
                success => Debug.Log($"Canceled ticket: {ticket}"),
                error => Debug.LogError($"Error canceling ticket: {error.GenerateErrorReport()}"));
            }
        },
        error => Debug.LogError($"Error listing tickets: {error.GenerateErrorReport()}"));
    }

    void EnableGamePanel()
    {
        gamePanel.SetActive(true);
        introPanel.SetActive(false);
    }
}
