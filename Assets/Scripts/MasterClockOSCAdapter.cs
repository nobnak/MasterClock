using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using Osc2;
using System.Diagnostics;
using System.Collections.Concurrent;

/// <summary>
/// MasterClockのOSC統合アダプター
/// UnityEventを使用してMasterClockと疎結合
/// OSCスレッドから受信したtick/timeペアをキューに蓄積し、メインスレッドから安全にUnityEventを呼び出し
/// スレッドセーフでないレシーバーにも対応
/// </summary>
public class MasterClockOSCAdapter : MonoBehaviour {
    
    [Header("Tick Event")]
    [SerializeField] private UnityEvent<uint, double> onTickReceived = new UnityEvent<uint, double>();
    
    [Header("OSC Settings")]
    [SerializeField] private bool showDebugInfo = false;

    [SerializeField] private string address = "/clock/tick";
    [SerializeField] private int port = 8000;
    
    private OscReceiver receiver;
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();
    private readonly ConcurrentQueue<(uint tick, double time)> tickQueue = new ConcurrentQueue<(uint tick, double time)>();
    
    #region  unity event methods
    void OnEnable(){
        if (showDebugInfo) {
            UnityEngine.Debug.Log("[MasterClockOSCAdapter] Initialized with main-thread safe processing");
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
            // OSCスレッドからキューにエンキュー（メインスレッドで処理される）
            tickQueue.Enqueue(((uint)tick, GetThreadSafeTime()));
        };
        receiver.Error += (e) => {
            UnityEngine.Debug.LogError(e);
        };
    }
    
    void Update() {
        // キューからtick/timeペアを取り出してメインスレッドでUnityEventを呼び出し
        while (tickQueue.TryDequeue(out var tickData)) {
            onTickReceived.Invoke(tickData.tick, tickData.time);
            
            if (showDebugInfo) {
                UnityEngine.Debug.Log($"[MasterClockOSCAdapter] Processed queued tick {tickData.tick} at time {tickData.time:F3}s");
            }
        }
    }
    
    void OnDisable() {
        receiver?.Dispose();
        receiver = null;
    }
    #endregion
    
    
    /// <summary>
    /// tick値を送信（デバッグ用）
    /// キュー経由でメインスレッドから処理される
    /// </summary>
    /// <param name="tick">送信するtick値</param>
    public void SendTick(uint tick) {
        double time = GetThreadSafeTime();
        // キューにエンキュー（メインスレッドのUpdate()で処理）
        tickQueue.Enqueue((tick, time));
        
        if (showDebugInfo) {
            UnityEngine.Debug.Log($"[MasterClockOSCAdapter] Manual tick {tick} at time {time:F3}s queued");
        }
    }
    
    /// <summary>
    /// UnityEventへの参照を取得（動的な購読用）
    /// </summary>
    /// <returns>tick受信イベント</returns>
    public UnityEvent<uint, double> GetTickEvent() => onTickReceived;
    
    /// <summary>
    /// スレッドセーフなtime値を取得
    /// </summary>
    /// <returns>アプリケーション開始からの経過時間（秒）</returns>
    private double GetThreadSafeTime() => stopwatch.Elapsed.TotalSeconds;
}
