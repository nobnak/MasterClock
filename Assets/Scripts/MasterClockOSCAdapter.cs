using System.Collections.Concurrent;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// MasterClockのOSC統合アダプター
/// UnityEventを使用してMasterClockと疎結合
/// バックグラウンドスレッドからのOSCメッセージをメインスレッドで安全に処理するためのディスパッチシステムを内蔵
/// </summary>
public class MasterClockOSCAdapter : MonoBehaviour {
    
    [Header("Tick Event")]
    [SerializeField] private UnityEvent<uint> onTickReceived = new UnityEvent<uint>();
    
    [Header("OSC Settings")]
    [SerializeField] private bool showDebugInfo = false;

    [SerializeField] private string address = "/clock/tick";
    [SerializeField] private int port = 8000;
    
    // メインスレッドディスパッチ用キュー
    private readonly ConcurrentQueue<uint> tickQueue = new ConcurrentQueue<uint>();
    
    #region  unity event methods
    void OnEnable(){
        if (showDebugInfo) {
            Debug.Log("[MasterClockOSCAdapter] Initialized with UnityEvent-based tick distribution");
        }

        StartCoroutine(Receiver());
        System.Collections.IEnumerator Receiver() {
            yield return null;

            using var recv = new Osc2.OscReceiver(port);
            recv.Receive += (capsule) => {
                var msg = capsule.message;
                if (msg.path == address) {
                    ListenTick(msg);
                }
            };
            recv.Error += (e) => {
                Debug.LogError(e);
            };

            while (isActiveAndEnabled) {
                yield return null;
            }
        }       
    }
    
    void Update() {
        // メインスレッドでキューからtickを取得して処理
        ProcessTickQueue();
    }
    #endregion
    
    /// <summary>
    /// メインスレッドでキューに蓄積されたtick値を処理
    /// </summary>
    private void ProcessTickQueue() {
        // パフォーマンスを考慮してフレームあたり最大処理数を制限
        const int maxTicksPerFrame = 10;
        int processedCount = 0;
        
        while (processedCount < maxTicksPerFrame && tickQueue.TryDequeue(out uint tick)) {
            // メインスレッドでUnityEventを安全に呼び出し
            onTickReceived.Invoke(tick);
            
            if (showDebugInfo) {
                Debug.Log($"[MasterClockOSCAdapter] Tick {tick} processed on main thread");
            }
            
            processedCount++;
        }
    }
    
    /// <summary>
    /// OSC メッセージからtick値を受信し、メインスレッドでの処理用にキューへ追加
    /// このメソッドはOSCバックグラウンドスレッドから呼び出される可能性がある
    /// </summary>
    /// <param name="msg">受信したOSCメッセージ</param>
    public void ListenTick(Osc2.Message msg) {        
        // OSCデータからtick値を取得
        if (msg.data.Length < 1 || msg.data[0] is not int tick) {
            // バックグラウンドスレッドからのDebug.LogWarningは避ける
            return;
        }
        
        if (tick < 0) {
            return;
        }
        
        // メインスレッドでの処理用にキューへ追加（スレッドセーフ）
        tickQueue.Enqueue((uint)tick);
    }
    
    /// <summary>
    /// tick値を送信（デバッグ用）
    /// メインスレッドディスパッチシステムを通して処理される
    /// </summary>
    /// <param name="tick">送信するtick値</param>
    public void SendTick(uint tick) {
        // 一貫性のためキューシステムを使用
        tickQueue.Enqueue(tick);
        
        if (showDebugInfo) {
            Debug.Log($"[MasterClockOSCAdapter] Manual tick {tick} queued for processing");
        }
    }
    
    /// <summary>
    /// UnityEventへの参照を取得（動的な購読用）
    /// </summary>
    /// <returns>tick受信イベント</returns>
    public UnityEvent<uint> GetTickEvent() => onTickReceived;
}
