using UnityEngine;
using Unity.Netcode;
using Unity.Cinemachine;

public class LocalPlayerCameraBinder : NetworkBehaviour
{
    [Header("Optional explicit targets on player prefab")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private Transform lookAtTarget;

    [Header("Scene camera refs (optional, auto-find if null)")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private bool disableAudioListenerOnNonOwner = true;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
        {
            if (disableAudioListenerOnNonOwner)
            {
                var listeners = GetComponentsInChildren<AudioListener>(true);
                for (int i = 0; i < listeners.Length; i++)
                    listeners[i].enabled = false;
            }
            return;
        }

        BindCamera();
    }

    private void BindCamera()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (cinemachineCamera == null) cinemachineCamera = FindFirstObjectByType<CinemachineCamera>();

        if (cinemachineCamera == null)
        {
            Debug.LogWarning("[LocalPlayerCameraBinder] No CinemachineCamera found in scene.");
            return;
        }

        Transform follow = followTarget != null ? followTarget : transform;
        Transform lookAt = lookAtTarget != null ? lookAtTarget : follow;

        cinemachineCamera.Follow = follow;
        cinemachineCamera.LookAt = lookAt;

        if (mainCamera != null)
        {
            PlayerEvents.LocalGameplayCamera = mainCamera;
            if (mainCamera.GetComponent<CinemachineBrain>() == null)
                mainCamera.gameObject.AddComponent<CinemachineBrain>();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner && mainCamera != null && PlayerEvents.LocalGameplayCamera == mainCamera)
            PlayerEvents.LocalGameplayCamera = null;
        base.OnNetworkDespawn();
    }
}