using UnityEngine;
using Mirror;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 外部からtick値を受け取り、NetworkTime.predictedTimeと同期し、MirrorのEMAでオフセットを推定するマスタークロック
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
[DefaultExecutionOrder(-100)]
public class MasterClock : NetworkBehaviour, IMasterClock {
    
    [SerializeField] private MasterClockCore.Config config = new MasterClockCore.Config();
    internal MasterClockCore core;  // エディターからアクセスできるようにinternal
    
    /// <summary>
    /// 設定への読み取り専用アクセスを提供
    /// </summary>
    public MasterClockCore.Config Settings => config;
    
    // SyncVarでクライアントに共有するオフセット
    [SyncVar(hook = nameof(OnOffsetChanged))]
    private double synchronizedOffset = 0.0;

    #region Unity Lifecycle Methods
    private void OnEnable() {
        // MasterClockCoreインスタンスを作成
        core = new MasterClockCore(config);
        
        // デバッグログのコールバックを設定
        core.OnDebugLog += (message) => Debug.Log($"[MasterClock] {message}");
        
        // オフセット更新コールバックを設定
        core.OnOffsetUpdated += (offset) => synchronizedOffset = offset;
        
        if (config.showDebugInfo) {
            Debug.Log("[MasterClock] Component enabled and core created");
        }
    }
    
    public override void OnStartServer() {
        base.OnStartServer();
        
        // サーバー側の初期化
        core.Initialize(GetCurrentTime());
        
        if (config.showDebugInfo) {
            Debug.Log($"[MasterClock] Server started with config: {config}");
        }
    }
    #endregion

    #region Server Methods
    
    /// <summary>
    /// 外部からtick値を受け取り、時刻同期処理を実行
    /// </summary>
    /// <param name="tickValue">外部から入力されるtick値</param>
    [ServerCallback]
    public void ProcessTick(uint tickValue) {
        core.ProcessTick(tickValue, ThreadSafeTime.realtimeSinceStartupAsDouble, "ThreadSafeTime");
    }
    
    /// <summary>
    /// デバッグ用：現在の時刻から推定tick値を生成してProcessTickを呼び出す
    /// </summary>
    [ServerCallback]
    public void ProcessCurrentTimeTick() => core.ProcessCurrentTimeTick(GetCurrentTime(), "NetworkTime");
    
    /// <summary>
    /// EMAオフセットをリセット
    /// </summary>
    [ServerCallback]
    public void ResetOffset() {
        core.ResetOffset(GetCurrentTime());
    }
    
    /// <summary>
    /// EMAの期間を動的に変更
    /// </summary>
    /// <param name="newDuration">新しいEMA期間（秒）</param>
    [ServerCallback]
    public void SetEmaDuration(int newDuration) {
        core.SetEmaDuration(newDuration, GetCurrentTime());
    }
    
    /// <summary>
    /// tickRateを動的に変更
    /// </summary>
    /// <param name="newTickRate">新しいtickRate</param>
    [ServerCallback]
    public void SetTickRate(int newTickRate) {
        core.SetTickRate(newTickRate, GetCurrentTime());
    }
    
    /// <summary>
    /// サーバーの完全再初期化（設定変更時など）
    /// </summary>
    [ServerCallback]
    public void ReinitializeServer() {
        core.Reinitialize(GetCurrentTime());
    }

    /// <summary>
    /// 完全再初期化（インターフェイス実装）
    /// </summary>
    public void Reinitialize() {
        ReinitializeServer();
    }

    #endregion

    #region Client Methods
    /// <summary>
    /// オフセットが変更された時のコールバック（クライアント側）
    /// </summary>
    private void OnOffsetChanged(double oldOffset, double newOffset) {
        if (config.showDebugInfo && !NetworkServer.active) {
            Debug.Log($"[MasterClock Client] Offset updated: {oldOffset:F4}s -> {newOffset:F4}s");
        }
    }
    #endregion

    #region Common Methods (Client & Server)
    /// <summary>
    /// 同期された時刻を取得（クライアント・サーバー共通）
    /// </summary>
    /// <returns>同期された時刻</returns>
    public double GetSynchronizedTime() => GetCurrentTime() + synchronizedOffset;
    
    /// <summary>
    /// リモートクライアントのための同期時刻
    /// </summary>
    /// <returns>リモート同期時刻</returns>
    public double GetRemoteSynchronizedTime() => 
        NetworkServer.active ? (core?.GetRemoteSynchronizedTime() ?? 0.0) : GetCurrentTime() + synchronizedOffset;
    
    /// <summary>
    /// 現在のオフセット値を取得
    /// </summary>
    /// <returns>推定されたオフセット値</returns>
    public double GetCurrentOffset() => synchronizedOffset;
    
    /// <summary>
    /// EMAの統計情報を取得（サーバーのみ）
    /// </summary>
    /// <returns>EMAの値、分散、標準偏差</returns>
    public (double value, double variance, double standardDeviation) GetEmaStatistics() => 
        NetworkServer.active ? (core?.GetEmaStatistics() ?? (synchronizedOffset, 0, 0)) : (synchronizedOffset, 0, 0);
    
    /// <summary>
    /// 最後に入力されたtick値を取得
    /// </summary>
    /// <returns>最後に入力されたtick値</returns>
    public uint GetLastInputTick() => NetworkServer.active ? (core?.GetLastInputTick() ?? 0) : 0;
    
    /// <summary>
    /// 現在の時刻を取得（Mirror NetworkTime.predictedTime を double として返す）
    /// </summary>
    /// <returns>現在の時刻</returns>
    public virtual double GetCurrentTime() => NetworkTime.predictedTime;
    
    /// <summary>
    /// 最後に計算されたtick時刻を取得
    /// </summary>
    /// <returns>tick時刻</returns>
    public double GetCurrentTickTime() => NetworkServer.active ? (core?.runtime.currentTickTime ?? 0.0) : 0.0;
    #endregion

    #region Editor
#if UNITY_EDITOR
    [CustomEditor(typeof(MasterClock))]
    public class MasterClockEditor : Editor {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();
            
            MasterClock masterClock = (MasterClock)target;
            
            if (!masterClock.Settings.showDebugInfo) return;
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("=== Master Clock Debug Info ===", EditorStyles.boldLabel);
            
            EditorGUILayout.LabelField($"Is Server: {NetworkServer.active}");
            
            // coreが初期化されているかチェック
            if (masterClock.core == null) {
                EditorGUILayout.LabelField("Core not initialized (component not enabled or started)");
                return;
            }
            
            EditorGUILayout.LabelField($"Last Input Tick: {masterClock.GetLastInputTick()}");
            EditorGUILayout.LabelField($"Current Tick Time: {masterClock.GetRemoteSynchronizedTime():F4}s");
            EditorGUILayout.LabelField($"Network Predicted Time: {masterClock.GetCurrentTime():F4}s");
            EditorGUILayout.LabelField($"Synchronized Offset: {masterClock.GetCurrentOffset():F4}s");
            EditorGUILayout.LabelField($"Synchronized Time: {masterClock.GetSynchronizedTime():F4}s");
            EditorGUILayout.LabelField($"Remote Sync Time: {masterClock.GetRemoteSynchronizedTime():F4}s");
            EditorGUILayout.LabelField($"EMA Duration: {masterClock.Settings.emaDuration} seconds");
            
            if (NetworkServer.active) {
                var stats = masterClock.GetEmaStatistics();
                EditorGUILayout.LabelField($"EMA Variance: {stats.variance * 1000000:F3}ms²");
                EditorGUILayout.LabelField($"EMA Standard Deviation: {stats.standardDeviation * 1000:F3}ms");
                
                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Reset")) {
                    masterClock.ResetOffset();
                }
                if (GUILayout.Button("Test Tick")) {
                    masterClock.ProcessCurrentTimeTick();
                }
                if (GUILayout.Button("Reinit")) {
                    masterClock.ReinitializeServer();
                }
                EditorGUILayout.EndHorizontal();
            } else {
                EditorGUILayout.LabelField("(Client Mode)");
            }
            
            // プレイモード中は継続的に更新
            if (Application.isPlaying) {
                EditorUtility.SetDirty(target);
                Repaint();
            }
        }
    }
#endif
    #endregion
}