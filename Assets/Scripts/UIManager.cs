using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("Menu Panel")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;

    [Header("Settings Panel")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Button settingsBackButton;

    [Header("Animation Settings")]
    [SerializeField] private float fadeSpeed = 0.3f;
    [SerializeField] private float buttonAnimationDelay = 0.08f;
    [SerializeField] private float buttonSlideDistance = 50f;

    private bool isMenuOpen = false;
    private bool isSettingsOpen = false;
    private CanvasGroup menuCanvasGroup;
    private CanvasGroup settingsCanvasGroup;
    private List<Button> menuButtons = new List<Button>();
    private Dictionary<Button, Vector2> originalButtonPositions = new Dictionary<Button, Vector2>();

    void Start()
    {
        // Get or add canvas group for fade animations
        menuCanvasGroup = menuPanel.GetComponent<CanvasGroup>();
        if (menuCanvasGroup == null)
            menuCanvasGroup = menuPanel.AddComponent<CanvasGroup>();

        settingsCanvasGroup = settingsPanel.GetComponent<CanvasGroup>();
        if (settingsCanvasGroup == null)
            settingsCanvasGroup = settingsPanel.AddComponent<CanvasGroup>();

        // Set up buttons
        menuButtons.Add(resumeButton);
        menuButtons.Add(settingsButton);
        menuButtons.Add(exitButton);

        // Store original positions for animation
        foreach (Button button in menuButtons)
        {
            if (button != null)
            {
                RectTransform rt = button.GetComponent<RectTransform>();
                originalButtonPositions[button] = rt.anchoredPosition;

                // Add hover effects
                AddButtonHoverEffects(button);
            }
        }

        // Add button listeners
        resumeButton.onClick.AddListener(ResumeGame);
        settingsButton.onClick.AddListener(OpenSettings);
        exitButton.onClick.AddListener(ExitGame);
        settingsBackButton.onClick.AddListener(() => CloseSettings(true, true));

        // Initialize volume slider
        if (musicVolumeSlider != null)
        {
            // Set initial value from MusicManager
            if (MusicManager.Instance != null)
            {
                musicVolumeSlider.value = GetMusicVolume();
            }

            // Add listener for slider changes
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }

        // Close panels initially without showing menu
        CloseSettings(false, false);
        CloseMenu(false);
    }

    void Update()
    {
        // Toggle menu when Escape key is pressed
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isSettingsOpen)
                CloseSettings(true, true);
            else
                ToggleMenu();
        }
    }

    private void ToggleMenu()
    {
        if (isMenuOpen)
            CloseMenu(true);
        else
            OpenMenu();
    }

    private void OpenMenu()
    {
        // Show menu
        menuPanel.SetActive(true);

        // Show cursor
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Pause game
        Time.timeScale = 0f;

        // Animate menu opening
        StartCoroutine(FadeInMenu());

        isMenuOpen = true;
    }

    private void CloseMenu(bool animate)
    {
        if (animate)
        {
            // Animate menu closing
            StartCoroutine(FadeOutMenu());
        }
        else
        {
            // Immediately close menu
            menuPanel.SetActive(false);
            menuCanvasGroup.alpha = 0;
        }

        // Hide cursor if settings are also not open
        if (!isSettingsOpen)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            // Resume game
            Time.timeScale = 1f;
        }

        isMenuOpen = false;
    }

    private IEnumerator FadeInMenu()
    {
        menuCanvasGroup.alpha = 0;

        // Reset button positions for animation
        foreach (Button button in menuButtons)
        {
            if (button == null) continue;

            RectTransform rt = button.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(originalButtonPositions[button].x - buttonSlideDistance,
                                              originalButtonPositions[button].y);
            button.gameObject.SetActive(false);
        }

        // Fade in panel
        float timer = 0;
        while (timer < fadeSpeed)
        {
            timer += Time.unscaledDeltaTime;
            menuCanvasGroup.alpha = Mathf.Lerp(0, 1, timer / fadeSpeed);
            yield return null;
        }
        menuCanvasGroup.alpha = 1;

        // Animate buttons one by one
        for (int i = 0; i < menuButtons.Count; i++)
        {
            Button button = menuButtons[i];
            if (button == null) continue;

            RectTransform rt = button.GetComponent<RectTransform>();
            button.gameObject.SetActive(true);

            // Slide in animation
            timer = 0;
            Vector2 startPos = rt.anchoredPosition;
            Vector2 targetPos = originalButtonPositions[button];

            while (timer < fadeSpeed)
            {
                timer += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0, 1, timer / fadeSpeed);
                rt.anchoredPosition = Vector2.Lerp(startPos, targetPos, progress);
                yield return null;
            }
            rt.anchoredPosition = targetPos;

            // Wait briefly before animating next button
            yield return new WaitForSecondsRealtime(buttonAnimationDelay);
        }
    }

    private IEnumerator FadeOutMenu()
    {
        float timer = 0;
        float startAlpha = menuCanvasGroup.alpha;

        while (timer < fadeSpeed * 0.7f)  // Fade out slightly faster
        {
            timer += Time.unscaledDeltaTime;
            menuCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0, timer / (fadeSpeed * 0.7f));
            yield return null;
        }

        menuCanvasGroup.alpha = 0;
        menuPanel.SetActive(false);
    }

    private void OpenSettings()
    {
        // Set settings as open before closing menu to maintain cursor visibility
        isSettingsOpen = true;
        
        // Hide menu if it's open
        if (isMenuOpen)
        {
            CloseMenu(true);
        }

        // Show settings panel
        settingsPanel.SetActive(true);

        // Make sure cursor is visible
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Keep game paused
        Time.timeScale = 0f;

        // Animate settings panel opening
        StartCoroutine(FadeInSettings());
    }

    private void CloseSettings(bool animate, bool openMenuAfter = true)
    {
        if (animate)
        {
            // Animate settings closing
            StartCoroutine(FadeOutSettings());
        }
        else
        {
            // Immediately close settings
            settingsPanel.SetActive(false);
            settingsCanvasGroup.alpha = 0;
        }
        
        isSettingsOpen = false;
        
        // Go back to menu only if requested
        if (openMenuAfter)
        {
            OpenMenu();
        }
    }

    private IEnumerator FadeInSettings()
    {
        settingsCanvasGroup.alpha = 0;

        // Fade in panel
        float timer = 0;
        while (timer < fadeSpeed)
        {
            timer += Time.unscaledDeltaTime;
            settingsCanvasGroup.alpha = Mathf.Lerp(0, 1, timer / fadeSpeed);
            yield return null;
        }
        settingsCanvasGroup.alpha = 1;
    }

    private IEnumerator FadeOutSettings()
    {
        float timer = 0;
        float startAlpha = settingsCanvasGroup.alpha;

        while (timer < fadeSpeed * 0.7f)  // Fade out slightly faster
        {
            timer += Time.unscaledDeltaTime;
            settingsCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0, timer / (fadeSpeed * 0.7f));
            yield return null;
        }

        settingsCanvasGroup.alpha = 0;
        settingsPanel.SetActive(false);
    }

    private float GetMusicVolume()
    {
        // Access MusicManager to get current volume
        if (MusicManager.Instance != null)
        {
            return MusicManager.Instance.MusicVolume;
        }
        return 0.03f; // Default value from MusicManager
    }

    private void OnMusicVolumeChanged(float value)
    {
        // Update music volume in MusicManager
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.MusicVolume = value;
        }
    }

    private void AddButtonHoverEffects(Button button)
    {
        // Add hover animations to buttons
        EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();

        // Clear existing triggers
        trigger.triggers.Clear();

        // Scale up on hover
        EventTrigger.Entry enterEntry = new EventTrigger.Entry();
        enterEntry.eventID = EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener((data) => {
            StartCoroutine(ScaleButton(button.transform, 1.1f, 0.1f));
        });
        trigger.triggers.Add(enterEntry);

        // Scale back on exit
        EventTrigger.Entry exitEntry = new EventTrigger.Entry();
        exitEntry.eventID = EventTriggerType.PointerExit;
        exitEntry.callback.AddListener((data) => {
            StartCoroutine(ScaleButton(button.transform, 1.0f, 0.1f));
        });
        trigger.triggers.Add(exitEntry);
    }

    private IEnumerator ScaleButton(Transform buttonTransform, float targetScale, float duration)
    {
        Vector3 startScale = buttonTransform.localScale;
        Vector3 endScale = new Vector3(targetScale, targetScale, targetScale);

        float timer = 0;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            buttonTransform.localScale = Vector3.Lerp(startScale, endScale, timer / duration);
            yield return null;
        }

        buttonTransform.localScale = endScale;
    }

    private void ResumeGame()
    {
        CloseMenu(true);
    }

    private void ExitGame()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
}