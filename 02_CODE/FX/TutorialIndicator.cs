using UnityEngine;
using System.Collections;

public class TutorialIndicator : MonoBehaviour
{
    [Header("Pop-In Settings")]
    [SerializeField] private float popDuration = 0.3f;
    [SerializeField] private AnimationCurve popCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Floating Animation")]
    [SerializeField] private float floatAmplitude = 0.2f;
    [SerializeField] private float floatFrequency = 1.5f;
    [SerializeField] private Vector3 rotationPerSecond = new Vector3(0, 90, 0);

    private Vector3 _basePosition;
    private Vector3 _fullScale;

    private void Awake()
    {
        _basePosition = transform.localPosition;
        _fullScale = transform.localScale;
    }

    private void OnEnable()
    {
        // Start the pop effect every time the object is activated
        StopAllCoroutines();
        StartCoroutine(PopInRoutine());
    }

    private void Update()
    {
        // Continuous float and rotate
        float newY = _basePosition.y + Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
        transform.localPosition = new Vector3(_basePosition.x, newY, _basePosition.z);
        
        transform.Rotate(rotationPerSecond * Time.deltaTime);
    }

    private IEnumerator PopInRoutine()
    {
        float elapsed = 0;
        transform.localScale = Vector3.zero;

        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float percent = elapsed / popDuration;
            
            // Apply the curve for "a more natural feel"
            transform.localScale = _fullScale * popCurve.Evaluate(percent);
            yield return null;
        }

        transform.localScale = _fullScale;
    }
}