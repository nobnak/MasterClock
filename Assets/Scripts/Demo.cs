using UnityEngine;
using RosettaUI;

public class Demo : MonoBehaviour {

    [SerializeField] Preset preset = new();
    Runtime rt = new();

    [System.Serializable]
    public class Preset {
        public RosettaUIRoot uiBuilder;
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
    void OnDisable() {
    }

    public Element GetUI() {
        var ui = UI.Window(name,
            UI.Column(
                UI.Label("Label")
            )
        );
        return ui;
    }
}