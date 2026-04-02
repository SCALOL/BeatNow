using System.Xml.Serialization;
using UnityEngine;

public enum SFXType
{
    Hover,
    Click,
}

public class SFXManager : MonoBehaviour
{
    
    AudioSource audioSource;
    [SerializeField] AudioClip hoverSFX;
    [SerializeField] AudioClip clickedSFX;
    [SerializeField] AudioClip hitSFX;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }
    public void PlayHoverSFX() => PlaySFX(SFXType.Hover);
    public void PlayClickSFX() => PlaySFX(SFXType.Click);
    public void PlaySFX(SFXType sfxType)
    {
        switch (sfxType)
        {
            case SFXType.Hover:
                audioSource.PlayOneShot(hoverSFX);
                break;
            case SFXType.Click:
                audioSource.PlayOneShot(clickedSFX);
                break;
        }
        
    }
    public void ExitGame()
    {
        Application.Quit();
    }

    public void HitSound()
    { 
        audioSource.PlayOneShot(hitSFX);
    }
}
