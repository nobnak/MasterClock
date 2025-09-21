using UnityEngine;
using UnityEngine.Events;
using StarOSC;

/// <summary>
/// MasterClockのStarOSC統合アダプター
/// UnityEventを使用してMasterClockと疎結合
/// </summary>
public class MasterClockOSCAdapter : MonoBehaviour {
    
    [Header("Tick Event")]
    [SerializeField] private UnityEvent<uint> onTickReceived = new UnityEvent<uint>();
    
    [Header("OSC Settings")]
    [SerializeField] private bool showDebugInfo = false;
    
    private void Start() {
        if (showDebugInfo) {
            Debug.Log("[MasterClockOSCAdapter] Initialized with UnityEvent-based tick distribution");
        }
    }
    
    /// <summary>
    /// OSC メッセージからtick値を受信し、UnityEventで配信
    /// </summary>
    /// <param name="address">OSCメッセージのアドレス</param>
    /// <param name="data">受信したOSCデータ</param>
    /// <param name="_">未使用のOSC受信イベント引数</param>
    public void ListenTick(string address, ReceivedOscArguments data, OscReceiverEventArgs _) {
        if (showDebugInfo) {
            Debug.Log($"[MasterClockOSCAdapter] ListenTick: {address}, data count: {data.Remaining}");
        }
        
        // OSCデータからtick値を取得
        if (!data.TryRead(out int tick)) {
            Debug.LogWarning("[MasterClockOSCAdapter] Failed to read tick from OSC message");
            return;
        }
        
        if (tick < 0) {
            Debug.LogWarning($"[MasterClockOSCAdapter] Invalid tick value: {tick}");
            return;
        }
        
        // UnityEventでtick値を配信
        onTickReceived.Invoke((uint)tick);
        
        if (showDebugInfo) {
            Debug.Log($"[MasterClockOSCAdapter] Tick {tick} distributed via UnityEvent");
        }
    }
    
    /// <summary>
    /// UnityEventに直接tick値を送信（デバッグ用）
    /// </summary>
    /// <param name="tick">送信するtick値</param>
    public void SendTick(uint tick) {
        onTickReceived.Invoke(tick);
        
        if (showDebugInfo) {
            Debug.Log($"[MasterClockOSCAdapter] Manual tick {tick} sent via UnityEvent");
        }
    }
    
    /// <summary>
    /// UnityEventへの参照を取得（動的な購読用）
    /// </summary>
    /// <returns>tick受信イベント</returns>
    public UnityEvent<uint> GetTickEvent() => onTickReceived;
}
