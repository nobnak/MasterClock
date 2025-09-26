using UnityEngine;
using RosettaUI;

public class Demo : MonoBehaviour {

    [SerializeField] Preset preset = new();
    Runtime rt = new();

    [System.Serializable]
    public class Preset {
        public RosettaUIRoot uiBuilder;
        public MasterClockOSCAdapter oscAdapter;

        public MasterClockType masterClockType = MasterClockType.Standalone;
        public MasterClockStandalone masterClockStandalone;
        public MasterClockNet masterClockNet;
    }
    public class Runtime {
        public Element ui;
    }
    public enum MasterClockType {
        Standalone,
        Net,
    }

    void OnEnable() {
        if (rt.ui == null) {
            rt.ui = GetUI();
            preset.uiBuilder.Build(rt.ui);
        }
    }
    void Update() {
        switch (preset.masterClockType) {
            case MasterClockType.Standalone:
                preset.masterClockStandalone.gameObject.SetActive(true);
                preset.masterClockNet.gameObject.SetActive(false);
                break;
            case MasterClockType.Net:
                preset.masterClockStandalone.gameObject.SetActive(false);
                preset.masterClockNet.gameObject.SetActive(true);
                break;
        }
    }
    public Element GetUI() {
        var ui = UI.Window(name,
            UI.Column(
                UI.Field("Osc", 
                    () => preset.oscAdapter.gameObject.activeSelf,
                    v => preset.oscAdapter.gameObject.SetActive(v)),
                UI.Box(
                    UI.Label("Master Clock (Global)"),
                    UI.Field("Type", () => preset.masterClockType, v => preset.masterClockType = v),
                    UI.FieldReadOnly("Name", () => MasterClock.Global?.name ?? "No Clock Available"),
                    UI.FieldReadOnly("Seconds", () => $"{(MasterClock.Global?.GetSynchronizedTime() ?? 0.0):F4}s"),
                    UI.FieldReadOnly("Tick", () => $"{(MasterClock.Global?.GetLastInputTick() ?? 0):D10}"),
                    UI.FieldReadOnly("Offset", () => $"{(MasterClock.Global?.GetCurrentOffset() ?? 0.0):F4}s")
                ),
                UI.Box(
                    UI.Label("Unity Time"),
                    UI.FieldReadOnly("Seconds", () => $"{Time.timeAsDouble:F4}s")
                )
            )
        );
        return ui;
    }
}