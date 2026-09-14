using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class CharacterSelectReady : NetworkBehaviour
{
    public static CharacterSelectReady Instance { get; private set; }

    private NetworkList<ulong> readyClientIds;

    public event EventHandler OnReadyChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        readyClientIds = new NetworkList<ulong>();
    }

    public override void OnNetworkSpawn()
    {
        readyClientIds.OnListChanged += ReadyClientIds_OnListChanged;

        // when a new client joins, NGO syncs NetworkList automatically trigger local refresh "once" TODO 72: Check weird bug that happens sometimes.
        OnReadyChanged?.Invoke(this, EventArgs.Empty);
    }

    public override void OnNetworkDespawn()
    {
        if (readyClientIds != null)
            readyClientIds.OnListChanged -= ReadyClientIds_OnListChanged;
    }

    private void ReadyClientIds_OnListChanged(NetworkListEvent<ulong> changeEvent)
    {
        OnReadyChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetPlayerReady()
    {
        SetPlayerReadyServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetPlayerReadyServerRpc(ServerRpcParams serverRpcParams = default)
    {
        ulong senderId = serverRpcParams.Receive.SenderClientId;

        if (!readyClientIds.Contains(senderId))
            readyClientIds.Add(senderId);

        bool allClientsReady = true;
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (!readyClientIds.Contains(clientId))
            {
                allClientsReady = false;
                break;
            }
        }

        if (allClientsReady)
        {
            Loader.Scene targetScene = Loader.Scene.Map_01;
            bool isCoopCampaign = false;
            string selectedLevelId = Loader.Scene.Map_01.ToString();

            if (GameLobby.Instance != null)
            {
                targetScene = GameLobby.Instance.GetSelectedMapSceneOrDefault();
                isCoopCampaign = GameLobby.Instance.IsCoopCampaignLobby();
                selectedLevelId = GameLobby.Instance.GetSelectedMapLevelId();
            }

            CoopCampaignSessionContext.IsCoopCampaignRun = isCoopCampaign;
            CoopCampaignSessionContext.SelectedLevelId = selectedLevelId;
            CampaignRuntimeContext.Clear();

            if (GameLobby.Instance != null)
                _ = GameLobby.Instance.DeleteLobby();

            Loader.LoadNetwork(targetScene);
        }
    }

    public bool IsPlayerReady(ulong clientId)
    {
        return readyClientIds != null && readyClientIds.Contains(clientId);
    }
}