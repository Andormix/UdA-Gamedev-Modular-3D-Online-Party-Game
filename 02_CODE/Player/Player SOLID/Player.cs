using System;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerAnimator))]
[RequireComponent(typeof(PlayerInteractions))]
 [RequireComponent(typeof(PlayerClickMoveController))]
public class Player : NetworkBehaviour, InterfaceSceneObjectParent
{
    [SerializeField] private Transform sceneObjectSpawnPointReferenceOnPlayer;
    [SerializeField] private bool canPauseGame = true;

    private readonly NetworkVariable<NetworkObjectReference> heldRef =
    new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private SceneObject sceneObject;

    private PlayerInput m_PlayerInput;
    private PlayerMovement m_PlayerMovement;
    private PlayerAnimator m_PlayerAnimator;
    private PlayerInteractions m_PlayerInteractions;
    private PlayerClickMoveController m_PlayerClickMoveController;

    private void Awake()
    {
        m_PlayerInput = GetComponent<PlayerInput>();
        m_PlayerMovement = GetComponent<PlayerMovement>();
        m_PlayerAnimator = GetComponent<PlayerAnimator>();
        m_PlayerInteractions = GetComponent<PlayerInteractions>();
        m_PlayerClickMoveController = GetComponent<PlayerClickMoveController>();

    }

    private void Start()
    {
        bool isMultiplayerRun = GameMultiplayerManager.playMultiplayer;

        if (!isMultiplayerRun)
        {
            GetComponentInChildren<PlayerCharacterCustomized>()?.Load();
            Debug.Log("[Player] Loaded offline customization from PlayerPrefs.");
        }

        Debug.Log("IMM: " + GameManager.Instance.IsMainMenu());
        Debug.Log("CS: " + GameManager.Instance.GetGameState());
    }

    private void Update()
    {
        if(!IsOwner) return;

        Vector2 keyboardMove = m_PlayerInput.GetMovementVectorNorm();
        Vector2 finalMove = keyboardMove;

        if (keyboardMove.sqrMagnitude > 0.0001f)
        {
            m_PlayerClickMoveController?.CancelMove();
        }
        else if (m_PlayerClickMoveController != null)
        {
            finalMove = m_PlayerClickMoveController.GetMoveVector();
        }

        m_PlayerMovement.Move(finalMove);
        m_PlayerAnimator.UpdateAnimations(m_PlayerMovement.IsWalking());
        m_PlayerInteractions.HandleInteractions();

    }

    private void OnEnable()
    {
        if (m_PlayerInput != null)
            m_PlayerInput.OnPauseAction += OnPausePressed;
    }

    private void OnDisable()
    {
        if (m_PlayerInput != null)
            m_PlayerInput.OnPauseAction -= OnPausePressed;
    }

    private void OnPausePressed(object sender, EventArgs e)
    {
        if (!IsOwner) return; 

        Debug.Log($"Pause pressed on Player netId={NetworkObjectId} owner={OwnerClientId} IsOwner={IsOwner}", this);
        if (!canPauseGame) return;

        // GameManager can remain singleton (one per scene), that's fine.
        if (GameManager.Instance != null)
            GameManager.Instance.PauseToggle();
    }


    // --------------------------------------------------------------------------
    //                                Helpers
    // --------------------------------------------------------------------------

    public void SetSceneObject(SceneObject sceneObject) => this.sceneObject = sceneObject;
    public SceneObject GetSceneObject() => sceneObject;
    public void ClearSceneObject() => sceneObject = null;
    public bool HasSceneObject() => sceneObject != null;
    public bool CanAccept(SceneObject sceneObject) => sceneObject != null && !HasSceneObject();
    public Transform GetSceneObjectSpawnReference() => sceneObjectSpawnPointReferenceOnPlayer;

    public bool HasHeldNet()
    {
        return heldRef.Value.TryGet(out NetworkObject _);
    }

    public SceneObject GetHeldNet()
    {
        return heldRef.Value.TryGet(out NetworkObject no) ? no.GetComponent<SceneObject>() : null;
    }

    // server-only setters
    public void ServerSetHeld(SceneObject obj)
    {
        if (!IsServer) return;
        heldRef.Value = obj == null ? default : obj.GetComponent<NetworkObject>();
    }



    // --------------------------------------------------------------------------
    //                                Triggers
    // --------------------------------------------------------------------------


    public override void OnNetworkSpawn()
    {
        Debug.Log($"Player spawned: netId={NetworkObjectId} owner={OwnerClientId} IsOwner={IsOwner}", this);

        if (IsOwner)
        m_PlayerInput.EnableControls();
        else
        m_PlayerInput.DisableControls();

        ApplyNetworkTransformQualitySettings();

        heldRef.OnValueChanged += (_, __) =>
        {
            // Unbind old
            if (sceneObject != null)
            {
                sceneObject.ClientUnbindParent();
            }

            // Re-resolve current
            sceneObject = GetHeldNet();

            // Bind new (for clients + host)
            if (sceneObject != null)
            {
                sceneObject.ClientBindParent(this);
            }
        };

        // initial sync
        sceneObject = GetHeldNet();
        if (sceneObject != null)
            sceneObject.ClientBindParent(this);

        NetworkManager.Singleton.OnClientDisconnectCallback += NetworkManager_OnClientDisconnectCallback;
       
    }

    private void NetworkManager_OnClientDisconnectCallback(ulong clientId)
    {
        //TODO: Handle objects if we finally allow to continuar el juego.
    }


    public override void OnNetworkDespawn()
    {
        if (m_PlayerInput != null)
            m_PlayerInput.DisableControls();
    }

    private void ApplyNetworkTransformQualitySettings()
    {
        var nt = GetComponent<NetworkTransform>();
        if (nt == null)
            return;

        if (GraphicsQualityBootstrap.IsLowQuality)
        {
            nt.PositionThreshold = 0.02f;
            nt.RotAngleThreshold = 0.5f;
        }
    }

}