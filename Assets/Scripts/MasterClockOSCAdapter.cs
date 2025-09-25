using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using Osc2;

/// <summary>
/// MasterClockのOSC統合アダプター
/// UnityEventを使用してMasterClockと疎結合
/// スレッドセーフなProcessTick()に対応し、OSCスレッドから直接処理を実行
/// </summary>
public class MasterClockOSCAdapter : MonoBehaviour {
    
    [Header("Tick Event")]
    [SerializeField] private UnityEvent<uint> onTickReceived = new UnityEvent<uint>();
    
    [Header("OSC Settings")]
    [SerializeField] private bool showDebugInfo = false;

    [SerializeField] private string address = "/clock/tick";
    [SerializeField] private int port = 8000;
    
    private OscReceiver receiver;
    
    #region  unity event methods
    void OnEnable(){
        if (showDebugInfo) {
            Debug.Log("[MasterClockOSCAdapter] Initialized with direct OSC thread processing");
        }

        receiver = new Osc2.OscReceiver(port);
        receiver.Receive += (capsule) => {
            var msg = capsule.message;
            if (msg.path != address) {
                return;
            }
            if (msg.data.Length < 1 || msg.data[0] is not int tick) {
                return;
            }            
            // OSCスレッドから直接イベントを呼び出し（ProcessTick()はスレッドセーフ）
            onTickReceived.Invoke((uint)tick);
        };
        receiver.Error += (e) => {
            Debug.LogError(e);
        };
    }
    
    // Update()メソッドは不要（OSCスレッドから直接処理）
    void OnDisable() {
        receiver?.Dispose();
        receiver = null;
    }
    #endregion
    
    
    /// <summary>
    /// tick値を送信（デバッグ用）
    /// 直接イベントを呼び出す
    /// </summary>
    /// <param name="tick">送信するtick値</param>
    public void SendTick(uint tick) {
        // 直接イベントを呼び出し
        onTickReceived.Invoke(tick);
        
        if (showDebugInfo) {
            Debug.Log($"[MasterClockOSCAdapter] Manual tick {tick} processed directly");
        }
    }
    
    /// <summary>
    /// UnityEventへの参照を取得（動的な購読用）
    /// </summary>
    /// <returns>tick受信イベント</returns>
    public UnityEvent<uint> GetTickEvent() => onTickReceived;
}
