using System.Collections;
using UnityEngine;

public class UIAnimation : MonoBehaviour
{
    [SerializeField] private float hoverScale = 1.5f;
    [SerializeField] private float animationDuration = 0.2f;

    private Vector3 _defaultScale;
    private Coroutine _scaleCoroutine;

    private void Awake()
    {
        // Store the original scale so we know what to return to
        _defaultScale = transform.localScale;
    }

    // Function for Event Trigger: Pointer Enter
    public void OnHoverEnter()
    {
        StartScaling(Vector3.one * hoverScale);
    }

    // Function for Event Trigger: Pointer Exit
    public void OnHoverExit()
    {
        StartScaling(_defaultScale);
    }

    private void StartScaling(Vector3 targetScale)
    {
        // Stop any existing scaling animation to prevent "jittering"
        if (_scaleCoroutine != null)
            StopCoroutine(_scaleCoroutine);

        _scaleCoroutine = StartCoroutine(ScaleRoutine(targetScale));
    }

    private IEnumerator ScaleRoutine(Vector3 targetScale)
    {
        Vector3 initialScale = transform.localScale;
        float elapsedTime = 0;

        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / animationDuration;

            // "Ease In" Math: t * t creates a quadratic curve
            float easeIn = t * t;

            transform.localScale = Vector3.Lerp(initialScale, targetScale, easeIn);
            yield return null;
        }

        transform.localScale = targetScale;
    }
}