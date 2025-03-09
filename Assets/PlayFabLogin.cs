using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using TMPro;

public class PlayFabLogin : MonoBehaviour
{
    public TMP_Text statusText;
    public GameObject playButton;

    void Start()
    {
        statusText.text = "Logging in...";
        Login();
    }

    void Login()
    {
        var request = new LoginWithCustomIDRequest
        {
            CustomId = SystemInfo.deviceUniqueIdentifier,
            CreateAccount = true
        };

        PlayFabClientAPI.LoginWithCustomID(request, OnLoginSuccess, OnLoginFailure);
    }

    void OnLoginSuccess(LoginResult result)
    {
        statusText.text = "Logged in!";
        Invoke("ClearStatusText", 1f);
    }

    void OnLoginFailure(PlayFabError error)
    {
        statusText.text = "Login Failed: " + error.GenerateErrorReport();
    }

    void ClearStatusText()
    {
        if (statusText.text == "Logged in!")
            statusText.text = "";
        playButton.SetActive(true);

    }
}
