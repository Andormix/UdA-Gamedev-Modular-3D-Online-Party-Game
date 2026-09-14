using UnityEngine;

public class AutoScroll : MonoBehaviour
{
    [Tooltip("Positive values move right, negative move left.")]
    public float scrollSpeedX = 0.1f;
    
    [Tooltip("Positive values move up, negative move down.")]
    public float scrollSpeedY = -0.1f; 

    private Renderer rend;

    void Start()
    {
        rend = GetComponent<Renderer>();
    }

    void Update()
    {
        // Calculate the new offset based on time and speed
        float offsetX = Time.time * scrollSpeedX;
        float offsetY = Time.time * scrollSpeedY;

        // TODO: Bug fix In URP, the main texture is called "_BaseMap" instead of "_MainTex"
        rend.material.SetTextureOffset("_BaseMap", new Vector2(offsetX, offsetY));
    }
}