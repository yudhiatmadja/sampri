using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

public class ChatUI : MonoBehaviour
{
    public TMP_InputField inputField;
    public TMP_Text outputText;
    private OllamaChat ollamaChat;
    public Button sendButton;

    void Start()
    {
        ollamaChat = Object.FindAnyObjectByType<OllamaChat>();
        outputText.gameObject.SetActive(true);
        outputText.color = Color.black;
        sendButton.onClick.AddListener(SendMessage);
    }

    void UpdateChat(string message)
    {
        StartCoroutine(UpdateUIText(message));
    }

    IEnumerator UpdateUIText(string message)
    {
        yield return new WaitForEndOfFrame();
        outputText.text = message;
    }

    public void HandleFinalResponse(string response)
    {

        UpdateChat(response);
    }

    public void SendMessage()
    {
        string userInput = inputField.text;
        if (!string.IsNullOrEmpty(userInput))
        {
            ollamaChat.SendMessageToOllama(userInput);
        }
    }
}