using UnityEngine;

public class CameraEdgePan : MonoBehaviour
{
    [Header("Pan Settings")]
    public float panSpeed = 5f;
    public float edgeBoundary = 30f; // How close to the edge (in pixels) to trigger panning

    [Header("Pan Limits")]
    public float limitX = 3f; // Max distance it can move left/right from the start
    public float limitY = 2f; // Max distance it can move up/down from the start

    private Vector3 startPos;

    void Start()
    {
        // Remember the starting position so we don't drift away infinitely like before
        startPos = transform.position; 
    }

    void Update()
    {
        Vector3 pos = transform.position;
        Vector3 mousePos = Input.mousePosition;

        // Move Left or Right
        if (mousePos.x < edgeBoundary)
        {
            pos.x -= panSpeed * Time.deltaTime;
        }
        else if (mousePos.x > Screen.width - edgeBoundary)
        {
            pos.x += panSpeed * Time.deltaTime;
        }

        // Move Down or Up
        if (mousePos.y < edgeBoundary)
        {
            pos.y -= panSpeed * Time.deltaTime;
        }
        else if (mousePos.y > Screen.height - edgeBoundary)
        {
            pos.y += panSpeed * Time.deltaTime;
        }

        // Clamp the camera's position so it doesn't leave the background area
        pos.x = Mathf.Clamp(pos.x, startPos.x - limitX, startPos.x + limitX);
        pos.y = Mathf.Clamp(pos.y, startPos.y - limitY, startPos.y + limitY);

        // Apply the new position
        transform.position = pos;
    }
}