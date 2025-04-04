using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI Elements")]
    public Button playButton;
    public Button exitButton;
    
    [Header("Animation Settings")]
    public float fadeInDuration = 1.0f;
    public float fadeOutDuration = 0.5f;
    public float buttonHoverScale = 1.1f;
    public float buttonAnimationSpeed = 0.1f;
    
    [Header("Scene Settings")]
    public string gameSceneName = "GameScene";
    
    private void Start()
    {
        // Set up button listeners
        playButton.onClick.AddListener(OnPlayClicked);
        exitButton.onClick.AddListener(OnExitClicked);
        
        // Set up button hover animations
        SetupButtonAnimations(playButton);
        SetupButtonAnimations(exitButton);
        
        // Start with canvas invisible and fade in
        StartCoroutine(FadeIn());
    }
    
    private void SetupButtonAnimations(Button button)
    {
        // Add event triggers for pointer enter/exit events
        var eventTrigger = button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        
        // Pointer enter event
        var enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
        enterEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener((data) => { OnButtonHoverEnter(button.transform); });
        eventTrigger.triggers.Add(enterEntry);
        
        // Pointer exit event
        var exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
        exitEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
        exitEntry.callback.AddListener((data) => { OnButtonHoverExit(button.transform); });
        eventTrigger.triggers.Add(exitEntry);
    }
    
    private void OnButtonHoverEnter(Transform buttonTransform)
    {
        // Scale up button on hover
        StartCoroutine(ScaleButton(buttonTransform, new Vector3(buttonHoverScale, buttonHoverScale, 1f)));
    }
    
    private void OnButtonHoverExit(Transform buttonTransform)
    {
        // Scale back to original size
        StartCoroutine(ScaleButton(buttonTransform, Vector3.one));
    }
    
    private IEnumerator ScaleButton(Transform buttonTransform, Vector3 targetScale)
    {
        Vector3 startScale = buttonTransform.localScale;
        float time = 0f;
        
        while (time < buttonAnimationSpeed)
        {
            buttonTransform.localScale = Vector3.Lerp(startScale, targetScale, time / buttonAnimationSpeed);
            time += Time.deltaTime;
            yield return null;
        }
        
        buttonTransform.localScale = targetScale;
    }
    
    private IEnumerator FadeIn()
    {
        float time = 0f;
        
        while (time < fadeInDuration)
        {
            time += Time.deltaTime;
            yield return null;
        }
        
    }
    
    private IEnumerator FadeOut()
    {
        float time = 0f;
        
        while (time < fadeOutDuration)
        {
            time += Time.deltaTime;
            yield return null;
        }
        
    }
    
    public void OnPlayClicked()
    {
        StartCoroutine(PlayGame());
    }
    
    private IEnumerator PlayGame()
    {
        // Fade out the menu
        yield return StartCoroutine(FadeOut());
        
        // Load the game scene
        SceneManager.LoadScene(gameSceneName);
    }
    
    public void OnExitClicked()
    {
        StartCoroutine(ExitGame());
    }
    
    private IEnumerator ExitGame()
    {
        // Fade out the menu
        yield return StartCoroutine(FadeOut());
        
        // Quit the application
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}