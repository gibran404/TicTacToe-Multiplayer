using UnityEngine;
using PlayFab;
using PlayFab.MultiplayerModels;
using TMPro;
using System.Collections.Generic;

public class Matchmaker : MonoBehaviour
{
    public TMP_Text matchmakingStatus;
    public static string MatchId;       // Shared group ID.
    public static string MyPlayerRole;  // "X" or "O"
    public static string OpponentId;    // Opponent's ID.
    private string ticketId;
    private bool isMatchmaking = false;
    public GameObject gamePanel;
    public GameObject introPanel;

    public void StartMatchmaking()
    {
        // CancelAllTickets();
        matchmakingStatus.text = "Searching for a match...";
        isMatchmaking = true;

        var request = new CreateMatchmakingTicketRequest
        {
            Creator = new MatchmakingPlayer
            {
                Entity = new EntityKey
                {
                    Id = PlayFabSettings.staticPlayer.EntityId,
                    Type = PlayFabSettings.staticPlayer.EntityType
                }
            },
            GiveUpAfterSeconds = 69,
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

    void OnTicketCreated(CreateMatchmakingTicketResult result)
    {
        ticketId = result.TicketId;
        InvokeRepeating(nameof(CheckMatchmakingStatus), 2f, 2f);
    }

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

    void OnMatchFound(GetMatchResult result)
    {
        Debug.Log($"Match Found: {JsonUtility.ToJson(result)}");

        if (result.Members != null && result.Members.Count >= 2)
        {
            // Compute shared group ID by concatenating sorted player IDs.
            string player1 = result.Members[0].Entity.Id;
            string player2 = result.Members[1].Entity.Id;
            string sharedGroupId = (player1.CompareTo(player2) <= 0) 
                ? player1 + "_" + player2 
                : player2 + "_" + player1;
            MatchId = sharedGroupId;
            
            // Determine assignment.
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

    void EnableGamePanel()
    {
        gamePanel.SetActive(true);
        introPanel.SetActive(false);
    }

    void OnMatchmakingFailed(PlayFabError error)
    {
        matchmakingStatus.text = "Matchmaking Failed\nStop Spamming Buttons!";
    }

    void CancelAllTickets()
    {
        var request = new ListMatchmakingTicketsForPlayerRequest
        {
            Entity = new EntityKey
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
        }, error => Debug.LogError($"Error listing tickets: {error.GenerateErrorReport()}"));
    }
}
