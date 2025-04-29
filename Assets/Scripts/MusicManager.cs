using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections; // Needed for Coroutines

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [SerializeField] private AudioSource musicSource; // Assign in Inspector
    [Header("Music Tracks")]
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip gameMusic;
    // Add more tracks as needed
    [Header("Audio Settings")]
    [SerializeField] [Range(0f, 1f)] private float musicVolume = 0.03f; // Default 5% Volume
    [SerializeField] private float fadeDuration = 1.5f;
    private Coroutine currentFadeCoroutine;

    // Public property to access music volume
    public float MusicVolume
    {
        get { return musicVolume; }
        set
        {
            musicVolume = Mathf.Clamp01(value); // Ensure value is between 0 and 1
            if (musicSource != null && musicSource.isPlaying)
            {
                // Apply volume change immediately if music is playing
                musicSource.volume = musicVolume;
            }
        }
    }

    void Awake()
    {
        // Singleton Logic: Ensure only one instance exists
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (musicSource == null) musicSource = GetComponent<AudioSource>(); // Failsafe
            musicSource.loop = true;
            musicSource.volume = 0f; // <--- CHANGE THIS: Start silent
        }
        else
        {
            // If another instance exists, this one is redundant. Destroy it.
            Destroy(gameObject);
            return; // Stop execution for this duplicate instance
        }
    }

    void OnEnable()
    {
        // Subscribe to the scene loaded event
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        // IMPORTANT: Unsubscribe when the object is disabled or destroyed
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
         // Play initial music based on the *first* scene loaded
         HandleSceneMusic(SceneManager.GetActiveScene());
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HandleSceneMusic(scene);
    }

    private void HandleSceneMusic(Scene scene)
    {
        AudioClip targetClip = null;

        // --- Determine the correct clip based on scene name (or build index) ---
        // This logic is simple; refine if you have many scenes/complex rules.
        if (scene.name == "menuscene") // Use your actual scene names
        {
            targetClip = menuMusic;
        }
        else if (scene.name == "SampleScene") // Use your actual scene names
        {
            targetClip = gameMusic;
        }
        // Add more else if clauses for other scenes/music types

        // --- Play or Change the music ---
        PlayMusic(targetClip);
    }

    public void PlayMusic(AudioClip clipToPlay)
    {
        if (clipToPlay == null)
        {
            // No music for this scene, fade out if playing
            if (musicSource.isPlaying)
            {
               FadeOut();
            }
            return;
        }

        // If the correct clip is already playing (and not fading out), do nothing
        if (musicSource.clip == clipToPlay && musicSource.isPlaying && (currentFadeCoroutine == null || musicSource.volume > 0.01f) )
        {
            return; // Already playing the right tune
        }

        // Stop any existing fade and start the new transition
        if (currentFadeCoroutine != null)
        {
            StopCoroutine(currentFadeCoroutine);
        }
        currentFadeCoroutine = StartCoroutine(FadeTrack(clipToPlay));
    }

    public void FadeOut()
    {
         if (currentFadeCoroutine != null) StopCoroutine(currentFadeCoroutine);
         currentFadeCoroutine = StartCoroutine(FadeVolume(0f));
    }

    private IEnumerator FadeTrack(AudioClip newClip)
    {
        // --- Existing Fade Out Logic ---
        if (musicSource.isPlaying && musicSource.volume > 0.01f) // Use a small threshold
        {
            // Ensure fade out always goes to 0
            yield return StartCoroutine(FadeVolume(0f)); // Assuming FadeVolume handles the actual fade
        }
        else // Ensure volume is 0 if not playing or already silent
        {
            musicSource.volume = 0f;
            if(musicSource.isPlaying) musicSource.Stop(); // Stop if somehow playing at volume 0
        }

        // Change clip and fade in
        musicSource.clip = newClip;
        if (newClip != null)
        {
            musicSource.Play(); // Starts playing (initially at volume 0)
            // Fade in to the target musicVolume
            yield return StartCoroutine(FadeVolume(musicVolume)); // <--- CHANGE THIS: Use musicVolume variable
        }
        // If newClip is null, it correctly does nothing after fading out.

        currentFadeCoroutine = null; // Mark fade as complete
    }

    private IEnumerator FadeVolume(float targetVolume)
    {
        float startVolume = musicSource.volume;
        float time = 0f;
        float volumeDelta = targetVolume - startVolume;
        
        // Quick return if no volume change needed
        if (Mathf.Approximately(volumeDelta, 0f))
        {
            yield break;
        }

        while (time < fadeDuration)
        {
            time += Time.unscaledDeltaTime; // Use unscaled time if you want fades during pause screens etc.
            float t = time / fadeDuration;
            musicSource.volume = startVolume + volumeDelta * t;
            yield return null; // Wait for the next frame
        }

        musicSource.volume = targetVolume; // Ensure it reaches the target exactly
        if (targetVolume <= 0.01f) // If faded out completely, stop the source
        {
            musicSource.Stop();
            musicSource.clip = null; // Clear the clip reference
        }
         // Don't clear currentFadeCoroutine here if called by FadeTrack
    }
}