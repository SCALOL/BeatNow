using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class MenuManager : MonoBehaviour
{
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] float fadeDuration = 1f; // Duration of the fade effect

    public UnityEvent PauseEvent;

    public void StartGame()
    {
        StartCoroutine(FadeOutAndLoadScene());
    }

    //IEnumerator Fading Alha Canvas Group from 1 to 0 then excute GotoSampleScene
    private IEnumerator FadeOutAndLoadScene()
    {
        
        float elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = 0f; // Ensure it's fully transparent
        GoToSampleScene();
    }

    //Go to SampleScene
    public void GoToSampleScene()
    {
        if (Time.timeScale != 1f)
        {
            Time.timeScale = 1f;        
        }
        UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene");
    }
    //Exit the game
    public void ExitGame()
    {
        Application.Quit();
    }

    public void GoToMainMenu()
    {
        if (Time.timeScale != 1f)
        {
            Time.timeScale = 1f;
        }
        UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
    }

    public void OnPause()
    { 
        PauseEvent?.Invoke();
    }
}
