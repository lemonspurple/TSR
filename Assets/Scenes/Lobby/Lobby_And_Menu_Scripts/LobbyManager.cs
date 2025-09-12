using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Fusion;                          // Photon Fusion Haupt-Namespace
using Fusion.Photon.Realtime;         // Enthält PhotonAppSettings, AppSettings und Netzwerk-Datenstrukturen

public class LobbyManager : MonoBehaviour, INetworkRunnerCallbacks
{
    // Verweise auf UI-Elemente (im Inspector zuzuweisen)
    public InputField roomNameInput;
    public Dropdown regionDropdown;
    public Button quickJoinButton;
    public Button hostButton;
    public Button joinButton;
    public Text statusText;

    private NetworkRunner networkRunner;      // Referenz auf die NetworkRunner-Komponente
    private string gameVersion = "1.0";       // Version des Spiels (für Photon matchmaking untersch. Versionen)

    void Start()
    {
        // NetworkRunner-Komponente holen (befindet sich auf demselben GameObject)
        networkRunner = GetComponent<NetworkRunner>();
        if (networkRunner == null)
        {
            // Falls noch kein NetworkRunner dran (sollte nicht passieren, da wir ihn manuell hinzugefügt haben)
            networkRunner = gameObject.AddComponent<NetworkRunner>();
        }

        // Diese Klasse (LobbyManager) als Callback-Empfänger beim NetworkRunner registrieren:
        networkRunner.AddCallbacks(this);

        // Button-Events verküpfen:
        quickJoinButton.onClick.AddListener(OnQuickJoin);
        hostButton.onClick.AddListener(OnHostGame);
        joinButton.onClick.AddListener(OnJoinGame);

        // Anfangsstatus anzeigen
        statusText.text = "Bitte Auswahl treffen...";
    }

    // Methode für Quick Join Button
    private void OnQuickJoin()
    {
        statusText.text = "Quick Join – Spiel wird gesucht/erstellt...";
        StartGame(SessionName: null, allowCreate: true);
        // SessionName null bedeutet: Photon Fusion sucht ein beliebiges offenes Spiel 
        // und erstellt ggf. ein neues, falls keines verfügbar ist:contentReference[oaicite:10]{index=10}.
    }

    // Methode für Host Game Button
    private void OnHostGame()
    {
        // Raumname aus InputField lesen (optional vom Spieler eingegeben)
        string roomName = roomNameInput.text;
        if (string.IsNullOrEmpty(roomName))
        {
            // Wenn kein Name eingegeben, einen zufälligen generieren
            roomName = "Room_" + Random.Range(1000, 9999);
        }
        statusText.text = $"Erstelle Spiel \"{roomName}\"...";
        StartGame(SessionName: roomName, allowCreate: true);
        // SessionName gesetzt => Fusion erstellt diesen Raum neu (oder tritt bei, falls zufällig gleicher Name existiert).
    }

    // Methode für Join Game Button
    private void OnJoinGame()
    {
        string roomName = roomNameInput.text;
        if (string.IsNullOrEmpty(roomName))
        {
            statusText.text = "Bitte geben Sie einen Raum-Namen ein!";
            return;
        }
        statusText.text = $"Trete Spiel \"{roomName}\" bei...";
        StartGame(SessionName: roomName, allowCreate: false);
        // allowCreate = false => nur beitreten, nicht erstellen:contentReference[oaicite:11]{index=11}.
    }

    // Zentrale Methode zum Starten/Beitreten einer Session (Photon Fusion StartGame aufrufen)
    private void StartGame(string SessionName, bool allowCreate)
    {
        // 1. Gewählte Region ermitteln:
        string regionCode = null;
        if (regionDropdown != null)
        {
            // Text der gewählten Dropdown-Option auslesen
            regionCode = regionDropdown.options[regionDropdown.value].text;
            if (regionCode.ToLower() == "auto")
            {
                regionCode = null; // null => keine feste Region -> Best Region verwenden
            }
        }

        // 2. Photon AppSettings basierend auf globalen Einstellungen kopieren und ggf. Region überschreiben:
        Fusion.Photon.Realtime.AppSettings customAppSettings = null;
        if (!string.IsNullOrEmpty(regionCode))
        {
            // Globale Photon Einstellungen holen (enthält AppID usw.)
            var baseSettings = PhotonAppSettings.Instance.AppSettings;
            customAppSettings = baseSettings.GetCopy();     // Kopie erzeugen, um Einstellungen nicht global zu verändern
            customAppSettings.UseNameServer = true;         // NameServer nutzen (standardmäßig true)
            customAppSettings.FixedRegion = regionCode.ToLower();  // Region festlegen (z.B. "eu" oder "us")
            customAppSettings.AppVersion = gameVersion;     // App-Version setzen (optional, für getrenntes Matchmaking nach Version)
        }

        // 3. StartGameArgs für NetworkRunner vorbereiten:
        var startArgs = new StartGameArgs()
        {
            GameMode = GameMode.Shared,      // Shared Mode (kein dedizierter Server, Clients teilen Simulation)
            SessionName = SessionName,       // Name der Session (null für Quick Join)
            PlayerCount = 10,               // maximale Spieleranzahl (optional; hier z.B. 10)
            Scene = SceneManager.GetActiveScene().buildIndex, // aktuelle Szene als Spielszene nutzen
            // Scene = SceneRef.None   // (Alternativ könnte man hier eine andere Szene laden lassen)
            SessionOption = new SessionProperty()            // (Standard-Optionen, hier nicht verändert)
        };
        if (!allowCreate)
        {
            // Option setzen, um nur beizutreten und nicht selbst zu erstellen, falls Session nicht existiert:
            startArgs.EnableClientSessionCreation = false;
        }
        if (customAppSettings != null)
        {
            startArgs.CustomPhotonAppSettings = customAppSettings;
        }

        // 4. Spiel/Session starten bzw. beitreten:
        networkRunner.StartGame(startArgs);
        // Hinweis: StartGame wird asynchron ausgeführt. Ergebnisse kommen über die Callbacks (siehe unten).
    }

    // --- Implementierung der INetworkRunnerCallbacks-Methoden: ---
    public void OnConnectedToServer(NetworkRunner runner)
    {
        // Erfolgreich mit Photon Server (Master Server) verbunden
        statusText.text = "Mit Photon Server verbunden!";
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        // Verbindung/Beitritt fehlgeschlagen
        statusText.text = $"Verbindung fehlgeschlagen: {reason}";
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        // Ein Spieler (oder man selbst) ist der Session beigetreten
        statusText.text = $"Spieler beigetreten: Player {player.PlayerId}";
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        // Ein Spieler hat das Spiel verlassen
        statusText.text = $"Spieler verlassen: Player {player.PlayerId}";
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        // Netzwerk/Session wurde beendet (z.B. alle Spieler raus, Disconnect, Fehler etc.)
        statusText.text = $"Disconnected: {shutdownReason}";
    }

    // Nicht benötigte Callbacks können leer bleiben:
    public void OnDisconnectedFromServer(NetworkRunner runner) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnSessionListUpdated(NetworkRunner runner, System.Collections.Generic.List<SessionInfo> sessionList) { }
    public void OnInput(NetworkRunner runner, NetworkRunnerCallbackArgs.Input input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnLobbyStatisticsUpdate(NetworkRunner runner, Fusion.Photon.Realtime.LobbyStats[] stats) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
}
