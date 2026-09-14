using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class IntermittentActivator : MonoBehaviour
{
    [Header("Interval Settings")]
    [Tooltip("Time in seconds the children remain active.")]
    public float activeDuration = 2.0f;
    
    [Tooltip("Time in seconds the children remain inactive.")]
    public float inactiveDuration = 2.0f;

    [Header("Behavior Settings")]
    [Tooltip("If true, starts with children enabled. If false, starts with them disabled.")]
    public bool startActive = true;

    private List<GameObject> children = new List<GameObject>();
    private Coroutine toggleCoroutine;

    void Awake()
    {
        // Cache children so we don't call GetChild in the loop
        foreach (Transform child in transform)
        {
            children.Add(child.gameObject);
        }
    }

    void OnEnable()
    {
        // Start the loop whenever the parent object is activated
        toggleCoroutine = StartCoroutine(ToggleChildrenLoop());
    }

    void OnDisable()
    {
        // Safety: Stop the routine if the parent is deactivated
        if (toggleCoroutine != null)
        {
            StopCoroutine(toggleCoroutine);
        }
    }

    IEnumerator ToggleChildrenLoop()
    {
        bool currentState = startActive;

        while (true)
        {
            // Set all cached children to the current state
            foreach (GameObject child in children)
            {
                if (child != null) 
                    child.SetActive(currentState);
            }

            // Wait for the duration corresponding to the current state
            yield return new WaitForSeconds(currentState ? activeDuration : inactiveDuration);

            // Flip the state for the next iteration
            currentState = !currentState;
        }
    }
}