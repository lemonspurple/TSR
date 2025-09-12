using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Fusion;
using Fusion.Photon.Realtime; // für PhotonAppSettings
using System.Threading;       // falls du später CancelTokens nutzt
using System.Threading.Tasks;
using Fusion.Sockets; // falls du später awaitest
using TMPro;

public class LobbyManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("UI")]
    public TMP_InputField roomNameInput;
    public TMP_Dropdown regionDropdown;        // Einträge: z.B. "auto", "eu", "us", "usw", "asia", ...
    public Button quickJoinButton;
    public Button hostButton;
    public Button joinButton;
    public TMP_Text statusText;

    private NetworkRunner networkRunner;
    private const string GameVersion = "1.0.0";

    void Awake()
    {
        networkRunner = GetComponent<NetworkRunner>();
        if (networkRunner == null) networkRunner = gameObject.AddComponent<NetworkRunner>();

        // UI-Events
        if (quickJoinButton) quickJoinButton.onClick.AddListener(OnQuickJoin);
        if (hostButton)      hostButton.onClick.AddListener(OnHostGame);
        if (joinButton)      joinButton.onClick.AddListener(OnJoinGame);

        // Callbacks registrieren
        networkRunner.AddCallbacks(this);

        if (statusText) statusText.text = "Bitte Auswahl treffen...";
    }

    // ---------- Button-Handler ----------
    void OnQuickJoin()
    {
        if (statusText) statusText.text = "Quick Join – suche/erstelle Spiel…";
        StartGame(sessionName: null, allowCreate: true);
    }

    void OnHostGame()
    {
        var roomName = string.IsNullOrWhiteSpace(roomNameInput?.text)
            ? $"Room_{UnityEngine.Random.Range(1000, 9999)}"
            : roomNameInput.text;

        if (statusText) statusText.text = $"Erstelle Spiel \"{roomName}\"…";
        StartGame(sessionName: roomName, allowCreate: true);
    }

    void OnJoinGame()
    {
        var roomName = roomNameInput?.text;
        if (string.IsNullOrWhiteSpace(roomName))
        {
            if (statusText) statusText.text = "Bitte einen Raum-Namen eingeben!";
            return;
        }
        if (statusText) statusText.text = $"Trete Spiel \"{roomName}\" bei…";
        StartGame(sessionName: roomName, allowCreate: false);
    }

    // ---------- Runner starten ----------
    void StartGame(string sessionName, bool allowCreate)
    {
        // Region aus Dropdown lesen ("" oder "auto" => Best Region)
        string regionText = null;
        if (regionDropdown && regionDropdown.options.Count > 0)
            regionText = regionDropdown.options[regionDropdown.value].text;

        // AppSettings-Kopie bauen (Region, AppVersion usw.)
        var appSettings = BuildCustomAppSettings(regionText);

        // Optional: aktuelle Szene als Startszene hinterlegen
        var sceneRef = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
        var sceneInfo = new NetworkSceneInfo();
        if (sceneRef.IsValid)
            sceneInfo.AddSceneRef(sceneRef, LoadSceneMode.Single); // bleibt in derselben Szene

        var startArgs = new StartGameArgs
        {
            GameMode = GameMode.Shared,
            SessionName = sessionName,                  // null => Random/Quick Join
            PlayerCount = 10,
            EnableClientSessionCreation = allowCreate,  // nur joinen, wenn false
            CustomPhotonAppSettings = appSettings,
            Scene = sceneInfo
            // SessionProperties = new Dictionary<string, SessionProperty> { { "mode", (SessionProperty)"lobby" } }
        };

        // Hinweis: StartGame ist async; wir lassen es hier "fire-and-forget".
        networkRunner.StartGame(startArgs);
    }

    // Baut eine FusionAppSettings-Kopie mit optionaler FixedRegion
    FusionAppSettings BuildCustomAppSettings(string region)
    {
        // Doku zeigt "PhotonAppSettings.Global" – das ist in Fusion 2 so vorgesehen
        var app = PhotonAppSettings.Global.AppSettings.GetCopy();
        app.UseNameServer = true;
        app.AppVersion = GameVersion;

        if (!string.IsNullOrEmpty(region) && region.ToLower() != "auto")
            app.FixedRegion = region.ToLower(); // z.B. "eu", "us", "usw", "asia", ...

        return app;
    }

    // ---------- INetworkRunnerCallbacks (Fusion 2 Signaturen) ----------
    public void OnConnectedToServer(NetworkRunner runner)
        => SafeStatus("Mit Photon Server verbunden.");

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        => SafeStatus($"Verbindung fehlgeschlagen: {reason}");

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        => SafeStatus($"Getrennt: {reason}");

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

    public void OnInput(NetworkRunner runner, NetworkInput input) { /* In der Lobby ungenutzt */ }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        => SafeStatus($"Spieler beigetreten: Player {player.PlayerId}");

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        => SafeStatus($"Spieler verlassen: Player {player.PlayerId}");

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }

    public void OnSceneLoadStart(NetworkRunner runner) { }

    public void OnSceneLoadDone(NetworkRunner runner) { }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        => SafeStatus($"Runner beendet: {shutdownReason}");

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

    // Hilfsfunktion, um Null-Checks zu sparen
    void SafeStatus(string msg) { if (statusText) statusText.text = msg; }








}
