using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using Fusion.Photon.Realtime; // SessionInfo, SessionLobby
using Fusion.Sockets;         // für ReliableKey, SimulationMessagePtr

public class RoomListManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("UI References")]
    public NetworkRunner networkRunner;       // Referenz auf den NetworkRunner (Photon Fusion)
    public RectTransform content;             // Content-Objekt des ScrollView
    public GameObject roomItemPrefab;         // Prefab für einen Raumeintrag (RoomListItem)
    public TMP_InputField nameFilterInput;    // Eingabefeld für Namensfilter
    public TMP_Dropdown regionDropdown;       // Dropdown für Regionsfilter
    const string APP_VERSION = "1.0.0";
    private List<SessionInfo> cachedSessions = new List<SessionInfo>();

    async void Start()
    {
        // Runner muss Callbacks kennen
        if (networkRunner != null)
        {
            networkRunner.AddCallbacks(this);
        }

        // Lobby beitreten, um Raumliste zu erhalten
        var result = await networkRunner.JoinSessionLobby(SessionLobby.ClientServer);
        if (!result.Ok)
        {
            Debug.LogError("Lobby-Beitritt fehlgeschlagen: " + result.ShutdownReason);
            return;
        }

        // Alle 2 Sekunden Liste refreshen
        StartCoroutine(AutoRefreshList());
    }

    // ----- Raumliste aktualisieren -----
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        cachedSessions = sessionList;
        UpdateRoomListUI();
        Debug.Log($"Raumliste aktualisiert ({sessionList.Count} Räume gefunden)");
    }

    private System.Collections.IEnumerator AutoRefreshList()
    {
        while (true)
        {
            UpdateRoomListUI();
            yield return new WaitForSeconds(2f);
        }
    }

    public void OnRefreshButtonClicked()
    {
        UpdateRoomListUI();
    }

    private void UpdateRoomListUI()
    {
        foreach (Transform child in content)
            Destroy(child.gameObject);

        string filterName = nameFilterInput.text.Trim().ToLower();
        string filterRegion = regionDropdown.options[regionDropdown.value].text;

        var filtered = cachedSessions;
        if (!string.IsNullOrEmpty(filterName))
            filtered = filtered.FindAll(s => s.Name.ToLower().Contains(filterName));

        if (filterRegion != "All")
            filtered = filtered.FindAll(s => s.Region.Equals(filterRegion, StringComparison.OrdinalIgnoreCase));

        filtered.Sort((a, b) => a.Name.CompareTo(b.Name));

        foreach (var session in filtered)
        {
            GameObject item = Instantiate(roomItemPrefab, content);
            item.transform.Find("NameText").GetComponent<TMP_Text>().text = session.Name;
            item.transform.Find("RegionText").GetComponent<TMP_Text>().text = session.Region;
            item.transform.Find("PlayersText").GetComponent<TMP_Text>().text =
                $"{session.PlayerCount}/{session.MaxPlayers} Spieler";

            Button joinBtn = item.transform.Find("JoinButton").GetComponent<Button>();
            string sessionName   = session.Name;
            string sessionRegion = session.Region;
            joinBtn.onClick.AddListener(() => JoinSelectedRoom(sessionName, sessionRegion));
        }
    }

    void ApplyCommonAppSettings(string regionOrNull)
    {
        var a = PhotonAppSettings.Global.AppSettings.GetCopy();
        a.UseNameServer = true;
        a.AppVersion = APP_VERSION;                     // überall gleich
        a.FixedRegion = string.IsNullOrWhiteSpace(regionOrNull) ? null : regionOrNull.ToLower();
        PhotonAppSettings.Global.AppSettings = a;
    }

    private async void JoinSelectedRoom(string sessionName, string sessionRegion)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
        {
            Debug.LogWarning("[RoomListManager] Ungültiger SessionName.");
            return;
        }

        // 1) Region passend zum Raum setzen (sonst suchst du in der falschen Region)
        ApplyCommonAppSettings(sessionRegion);

        Debug.Log($"[RoomListManager] Join '{sessionName}' in region '{sessionRegion}' (Shared)");

        // 2) Shared-Join, ohne neue Session zu erstellen
        var result = await networkRunner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Shared,
            SessionName = sessionName,
            EnableClientSessionCreation = false
        });

        if (!result.Ok)
            Debug.LogError($"Beitritt fehlgeschlagen: {result.ShutdownReason}");
    }

    // ----- INetworkRunnerCallbacks (Photon Fusion 2) -----
    void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { } //Experimental change
    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { } //Experimental change
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
}