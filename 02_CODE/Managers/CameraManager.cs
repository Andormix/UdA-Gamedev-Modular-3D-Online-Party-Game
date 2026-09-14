using Unity.Cinemachine;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance{ get; private set;}

    public CinemachineCamera cam1;
    public CinemachineCamera cam2;

    private const int ActivePriority = 15;
    private const int InactivePriority = 10;

    public void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        SetAllCamerasToInactive();
        SwitchToCam1();
    }

    private void SetAllCamerasToInactive()
    {
        cam1.Priority = InactivePriority;
        cam2.Priority = InactivePriority;
    }

    public void SetActiveCamera(CinemachineCamera cameraToActivate)
    {
        SetAllCamerasToInactive();
        if (cameraToActivate != null)
        {
            cameraToActivate.Priority = ActivePriority;
        }
    }

    public void SwitchToCam1()
    {
        SetAllCamerasToInactive();
        cam1.Priority = ActivePriority;
    }

    public void SwitchToCam2()
    {
        SetAllCamerasToInactive();
        cam2.Priority = ActivePriority;
    }

}
