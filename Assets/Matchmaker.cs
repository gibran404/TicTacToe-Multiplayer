using UnityEngine;
using PlayFab;
using PlayFab.MultiplayerModels;
using TMPro;
using System.Collections.Generic;

public class Matchmaker : MonoBehaviour
{
    public TMP_Text matchmakingStatus;
    public static string MatchId;       // Store the match ID globally.
    public static string MyPlayerRole;  // "X" or "O" assigned based on matchmaking.
    private string ticketId;
    private bool isMatchmaking = false;
    public GameObject gamePanel;
    public GameObject introPanel;


    public void StartMatchmaking()
    {
        // incase previous ticket is still up
        CancelAllTickets();

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
            GiveUpAfterSeconds = 60,
            QueueName = "TicTacToeQueue"
        };

        PlayFabMultiplayerAPI.CreateMatchmakingTicket(request, OnTicketCreated, OnMatchmakingFailed);
    }

    public void LeaveMatchMaking()
    {
        CancelAllTickets();
        isMatchmaking = false;
        matchmakingStatus.text = "left matchmaking";
    }

    void OnTicketCreated(CreateMatchmakingTicketResult result)
    {
        ticketId = result.TicketId;
        InvokeRepeating(nameof(CheckMatchmakingStatus), 5f, 5f);
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

        // For this example, we assume that the match result includes a match ID and a list of members.
        // Assign roles based on team assignment or member order.
        // Here we use a simple rule: first member gets "X", second gets "O".
        if (result.Members != null && result.Members.Count >= 2)
        {
            MatchId = result.MatchId; // Use match ID as SharedGroupId.
            string player1 = result.Members[0].Entity.Id;
            string player2 = result.Members[1].Entity.Id;

            matchmakingStatus.alignment = TMPro.TextAlignmentOptions.BottomLeft;
            matchmakingStatus.fontSize = 20;
            // Assume the local player is the creator (or determine based on your logic).
            if (player1 == PlayFabSettings.staticPlayer.EntityId)
            {
                MyPlayerRole = "X";
                matchmakingStatus.text = $"You are X\n({player2}) is O\nMatchId: {MatchId}";
            }
            else
            {
                MyPlayerRole = "O";
                matchmakingStatus.text = $"You are O\n({player1}) is X\nMatchId: {MatchId}";
            }
            Debug.Log("MatchId: " + MatchId);
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
        matchmakingStatus.text = "Matchmaking Failed: " + error.GenerateErrorReport();
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
