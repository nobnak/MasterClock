using Mirror;
using RosettaUI;
using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkManager))]
[HelpURL("https://mirror-networking.gitbook.io/docs/components/network-manager-hud")]
public class Lobby : MonoBehaviour {

    event System.Action OnConnect;
    event System.Action OnDisconnect;

    [SerializeField] protected Preset preset = new();
    [SerializeField] protected Tuner tuner = new();
    Runtime rt = new();

    protected NetworkManager manager;
    protected Element ui;

    protected Coroutine coConnectionChecker;
    protected Coroutine coDisconnected;

    #region unity
    void Awake() {
        manager = GetComponent<NetworkManager>();

        coConnectionChecker = StartCoroutine(CoConnectionChecker());

        OnConnect += () => {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //Debug.Log($"{nameof(OnConnect)}: ");
#endif
            if (preset.features.HasFlag(Features.HideOnConnect))
                preset.guiEnabled = false;
            ClearDisconnectedRoutine();
        };
        OnDisconnect += () => {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //Debug.Log($"{nameof(OnDisconnect)}: ");
#endif
            if (preset.features.HasFlag(Features.ShowOnDisconnect))
                preset.guiEnabled = true;

            ClearDisconnectedRoutine();
            coDisconnected = StartCoroutine(CoDisconnectedAction());
        };
    }

    void OnEnable() {
        rt = new();

        if (preset.uiRoot != null) {
            if (ui == null) {
                ui = GetWindow();
                preset.uiRoot.Build(ui);
            }
            ui.Enable = true;
        }

        ResetNetworkSettings();
        LoadTuner();
    }

    void OnDisable() {
        if (ui != null) {
            ui.Close();
            ui.Enable = false;
        }
    }
    void OnValidate() {
        Invalidate();
    }
    void Update() {
        Validate();
        if (!RosettaUIRoot.WillUseKeyInputAny() && Input.GetKeyDown(preset.guiOpenKey))
        {
            preset.guiEnabled = !preset.guiEnabled;
        }
        if (ui != null)
        {
            ui.Enable = preset.guiEnabled;
        }
    }

    private void Validate() {
        if (!rt.valid) {
            ApplyNetworkSettings();
            SaveTuner();
            rt.valid = true;
        }
        ;
    }
    void Invalidate() {
        rt.valid = false;
    }
    #endregion

    #region routine
    bool NotConnected => !NetworkClient.ready && !NetworkServer.active;
    IEnumerator CoConnectionChecker() {
        yield return null;
        var notConnected = NotConnected;
        OnDisconnect.Invoke();

        while (true) {
            yield return null;

            var notConnectedPrev = notConnected;
            notConnected = NotConnected;
            if (notConnectedPrev != notConnected) {
                if (notConnected) {
                    OnDisconnect.Invoke();
                } else {
                    OnConnect.Invoke();
                }
            }
        }
    }
    IEnumerator CoDisconnectedAction() {
        while (true) {
            yield return new WaitForSeconds(preset.featureActionDelay);

            if (preset.features.HasFlag(Features.AutoConnect) && NotConnected) {
                switch (tuner.autoConnect) {
                    case AutoConnect.Host: {
                        manager.StartHost();
                        break;
                    }
                    case AutoConnect.Client: {
                        manager.StartClient();
                        break;
                    }
                }
                if (tuner.autoConnect != AutoConnect.None) {
                    Debug.Log($"Auto connecting as {tuner.autoConnect}..");
                }
            }
        }
    }

    private void ClearDisconnectedRoutine() {
        if (coDisconnected != null) {
            StopCoroutine(coDisconnected);
            coDisconnected = null;
        }
    }
    #endregion

    #region methods
    public WindowElement GetWindow() {
        var ui = UI.Window("Lobby",
            UI.DynamicElementOnStatusChanged(
                () => !NetworkClient.active && !NetworkServer.active,
                f => f ? UI.Column(
                    UI.Button("Start host", () => {
                        manager.StartHost();
                    }),
                    UI.Button("Start client", () => {
                        manager.StartClient();
                    }),
                    UI.Box(
                        UI.Field("Auto connect:", () => tuner.autoConnect),
                        UI.Field("Address", () => tuner.networkAddress),
                        UI.DynamicElementIf(
                            () => Transport.active is PortTransport,
                            () => UI.Field("Port",
                                () => tuner.port.ToString(),
                                v => {
                                    if (ushort.TryParse(v, out var port)) {
                                        tuner.port = port;
                                    }
                                }
                            )
                        )
                    ).RegisterValueChangeCallback(() => {
                        Invalidate();
                    })
                ) : UI.Column(
                    UI.DynamicElementIf(
                        () => NetworkServer.active,
                        () => UI.Label(() => $"<b>{(NetworkClient.active ? "Host" : "Server")} </b>: running via {Transport.active}")
                    ),
                    UI.DynamicElementIf(
                        () => NetworkClient.isConnecting,
                        () => UI.Column(
                            UI.Label(() => $"Connecting to {manager.networkAddress}.."),
                            UI.Button("Cancel Connection Attempt", () => {
                                manager.StopClient();
                            })
                        )
                    ),
                    UI.DynamicElementIf(
                        () => NetworkClient.isConnected,
                        () => UI.Label(() => $"<b>Client</b>: connected to {manager.networkAddress} via {Transport.active}")
                    )
                )
            ),
            UI.Column(
                UI.DynamicElementIf(
                    () => NetworkServer.active,
                    () => UI.DynamicElementOnStatusChanged(
                        () => NetworkClient.isConnected,
                        f => f ?
                        UI.Button("Stop Host", () => {
                            manager.StopHost();
                        }) :
                        UI.Button("Stop Server", () => {
                            manager.StopServer();
                        })
                    )
                ),
                UI.DynamicElementIf(
                    () => NetworkClient.isConnected,
                    () => UI.Button("Stop Client", () => {
                        manager.StopClient();
                    })
                )
            )
        );
        return ui;
    }
    void ResetNetworkSettings() {
        tuner.networkAddress = manager.networkAddress;
        if (Transport.active is PortTransport portTransport) {
            tuner.port = portTransport.Port;
        }
    }
    void ApplyNetworkSettings() {
        manager.networkAddress = tuner.networkAddress;
        if (Transport.active is PortTransport portTransport) {
            portTransport.Port = tuner.port;
        }
    }
    void LoadTuner() {
        if (tuner.LoadFromPlayerPrefs(KEY_TUNER)) {
            Invalidate();
        }
    }
    void SaveTuner() {
        if (!tuner.SaveToPlayerPrefs(KEY_TUNER)) {
            Debug.LogWarning($"{nameof(SaveTuner)}: failed to save tuner");
        }
    }
    #endregion

    #region declarations
    public static readonly string KEY_TUNER = $"{nameof(Lobby.tuner)}";

    public enum AutoConnect {
        None = 0,
        Host = 1,
        Client = 2,
    }
    [System.Flags]
    public enum Features {
        None = 0,
        HideOnConnect = 1 << 0,
        ShowOnDisconnect = 1 << 1,
        AutoConnect = 1 << 2,
        Everything = -1,
    }

    [System.Serializable]
    public class Preset {
        public bool guiEnabled;
        public KeyCode guiOpenKey = KeyCode.N;
        public RosettaUIRoot uiRoot;

        public Features features = Features.Everything;
        public float featureActionDelay = 3f;
    }
    [System.Serializable]
    public class Tuner : IEquatable<Tuner> {
        public AutoConnect autoConnect = AutoConnect.None;

        public string networkAddress = "localhost";
        public ushort port = 7777;

        public bool Equals(Tuner other) {
            if (other == null) return false;
            return autoConnect == other.autoConnect &&
                networkAddress == other.networkAddress &&
                port == other.port;
        }
        public override bool Equals(object obj) => Equals(obj as Tuner);
        public override int GetHashCode() => base.GetHashCode();
    }
    public class Runtime {
        public bool valid;
    }

    #endregion
}
