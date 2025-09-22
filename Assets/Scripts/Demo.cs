using UnityEngine;
using RosettaUI;

public class Demo : MonoBehaviour {

    [SerializeField] Preset preset = new();
    Runtime rt = new();

    [System.Serializable]
    public class Preset {
        public RosettaUIRoot uiBuilder;
        public MasterClockQuery clockQuery;
        public MasterClockOSCAdapter oscAdapter;
    }
    public class Runtime {
        public Element ui;
    }

    void OnEnable() {
        if (rt.ui == null) {
            rt.ui = GetUI();
            preset.uiBuilder.Build(rt.ui);
        }
    }

    public Element GetUI() {
        var ui = UI.Window(name,
            UI.Column(
                UI.Field("Osc", 
                    () => preset.oscAdapter.gameObject.activeSelf,
                    v => preset.oscAdapter.gameObject.SetActive(v)),
                UI.Field("Clock Type", () => preset.clockQuery.CurrentType),
                UI.Box(
                    UI.Label("Master Clock"),
                    UI.FieldReadOnly("Seconds", () => $"{preset.clockQuery.GetSynchronizedTime():F4}s"),
                    UI.FieldReadOnly("Tick", () => $"{preset.clockQuery.GetLastInputTick():D10}"),
                    UI.FieldReadOnly("Offset", () => $"{preset.clockQuery.GetCurrentOffset():F4}s")
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