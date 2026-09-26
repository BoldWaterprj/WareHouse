using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Main menu. Scene names are configured in the Inspector so they can be
/// changed without touching code (keeps the project data-driven).
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Scenes (edit in Inspector, no recompile needed)")]
    [SerializeField] private string debugSceneName = "SampleScene";
    [SerializeField] private string levelSelectSceneName = "LevelSelect";
    [SerializeField] private string levelEditorSceneName = "LevelEditor";
    [SerializeField] private string instructorSceneName = "Instructor";

    [Header("UI")]
    [SerializeField] private Text statusText;

    private void Start()
    {
        if (statusText != null)
            statusText.text = "";
    }

    /// <summary>Start -> open the list of exported, playable levels.</summary>
    public void StartGame()
    {
        if (string.IsNullOrEmpty(levelSelectSceneName))
        {
            LoadScene(debugSceneName, "debug level");
            return;
        }
        LoadScene(levelSelectSceneName, "level select");
    }

    public void OpenLevelEditor()
    {
        if (string.IsNullOrEmpty(levelEditorSceneName))
        {
            ShowStatus("Level Editor: not implemented yet.");
            return;
        }
        LoadScene(levelEditorSceneName, "level editor");
    }

    public void OpenInstructor()
    {
        if (string.IsNullOrEmpty(instructorSceneName))
        {
            ShowStatus("Instructor: not implemented yet.");
            return;
        }
        LoadScene(instructorSceneName, "instructor screen");
    }

    private void LoadScene(string sceneName, string label)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning($"MainMenu: no scene configured for {label}.");
            ShowStatus($"No scene configured for {label}.");
            return;
        }

        Debug.Log($"MainMenu: loading {label} -> '{sceneName}'");
        SceneManager.LoadScene(sceneName);
    }

    private void ShowStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
}
