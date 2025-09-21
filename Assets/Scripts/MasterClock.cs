using UnityEngine;
using Mirror;
using Unity.Mathematics;
using StarOSC;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 外部からtick値を受け取り、NetworkTime.predictedTimeと同期し、MirrorのEMAでオフセットを推定するマスタークロック
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
[DefaultExecutionOrder(-100)]
public class MasterClock : NetworkBehaviour {
    [System.Serializable]
    public class Config {
        [Header("時刻同期設定")]
        [SerializeField] public int tickRate = 30; // 1秒に30tick
        [SerializeField] public int emaDuration = 10; // EMAの期間（秒）
        
        [Header("デバッグ情報")]
        [SerializeField] public bool showDebugInfo = true;
        
        /// <summary>
        /// 設定値を検証・修正する
        /// </summary>
        public void ValidateSettings() 
            => (tickRate, emaDuration) = (math.max(1, tickRate), math.max(1, emaDuration));
        
        /// <summary>
        /// 設定を文字列として出力
        /// </summary>
        public override string ToString() 
            => $"TickRate: {tickRate}Hz, EMA Duration: {emaDuration}s, Debug: {showDebugInfo}";
    }
    
    [SerializeField] private Config config = new Config();
    
    /// <summary>
    /// 設定への読み取り専用アクセスを提供
    /// </summary>
    public Config Settings => config;
    
    // SyncVarでクライアントに共有するオフセット
    [SyncVar(hook = nameof(OnOffsetChanged))]
    private double synchronizedOffset = 0.0;
    
    // プライベート変数
    private uint tickCount = 0;
    private double currentTickTime = 0.0;
    private double lastUpdateTime = 0.0;
    
    // MirrorのEMA実装を使用
    private ExponentialMovingAverage offsetEma;

    #region Unity Lifecycle Methods
    private void OnEnable() {
        // 設定値を検証・修正
        config.ValidateSettings();
        
        if (config.showDebugInfo) {
            Debug.Log("[MasterClock] Component enabled and settings validated");
        }
    }
    
    public override void OnStartServer() {
        base.OnStartServer();
        
        // サーバー側の初期化
        InitializeServerSide();
        
        if (config.showDebugInfo) {
            Debug.Log($"[MasterClock] Server started with config: {config}");
        }
    }
    #endregion

    #region Server Methods
    /// <summary>
    /// サーバー側の初期化処理
    /// </summary>
    private void InitializeServerSide() {
        // MirrorのEMAを初期化（期間 × tickRateでサンプル数を計算）
        offsetEma = new ExponentialMovingAverage(config.emaDuration * config.tickRate);
        lastUpdateTime = NetworkTime.predictedTime;
        
        // 初期状態をリセット
        tickCount = 0;
        currentTickTime = 0.0;
        synchronizedOffset = 0.0;
    }
    
    /// <summary>
    /// 外部からtick値を受け取り、時刻同期処理を実行
    /// </summary>
    /// <param name="tickValue">外部から入力されるtick値</param>
    [ServerCallback]
    public void ProcessTick(uint tickValue) {
        // 受け取ったtick値を設定
        tickCount = tickValue;
        
        // tickCountを秒に変換
        currentTickTime = (double)tickCount / config.tickRate;
        
        // NetworkTime.predictedTimeとの差分を計算
        double networkTime = NetworkTime.predictedTime;
        double timeDifference = currentTickTime - networkTime;
        
        // MirrorのEMAでオフセットを推定
        offsetEma.Add(timeDifference);
        
        // SyncVarを更新してクライアントに共有
        synchronizedOffset = offsetEma.Value;
        
        // デバッグ情報を表示
        if (config.showDebugInfo) {
            Debug.Log($"[MasterClock] Input Tick: {tickValue}, " +
                     $"TickTime: {currentTickTime:F4}s, " +
                     $"NetworkTime: {networkTime:F4}s, " +
                     $"Difference: {timeDifference:F4}s, " +
                     $"EMA Offset: {offsetEma.Value:F4}s, " +
                     $"EMA Variance: {offsetEma.Variance:F6}, " +
                     $"EMA StdDev: {offsetEma.StandardDeviation:F4}s");
        }
        
        lastUpdateTime = networkTime;
    }
    
    /// <summary>
    /// デバッグ用：現在の時刻から推定tick値を生成してProcessTickを呼び出す
    /// </summary>
    [ServerCallback]
    public void ProcessCurrentTimeTick() => ProcessTick((uint)math.round(NetworkTime.predictedTime * config.tickRate));
    
    /// <summary>
    /// EMAオフセットをリセット
    /// </summary>
    [ServerCallback]
    public void ResetOffset() {
        // サーバー側の状態を再初期化
        InitializeServerSide();
        
        if (config.showDebugInfo) {
            Debug.Log("[MasterClock] Server state reset and reinitialized");
        }
    }
    
    /// <summary>
    /// EMAの期間を動的に変更
    /// </summary>
    /// <param name="newDuration">新しいEMA期間（秒）</param>
    [ServerCallback]
    public void SetEmaDuration(int newDuration) {
        config.emaDuration = math.max(1, newDuration);
        
        // 設定変更後にサーバー側を再初期化
        InitializeServerSide();
        
        if (config.showDebugInfo) {
            Debug.Log($"[MasterClock] EMA Duration changed to: {config.emaDuration} seconds - server reinitialized");
        }
    }
    
    /// <summary>
    /// tickRateを動的に変更
    /// </summary>
    /// <param name="newTickRate">新しいtickRate</param>
    [ServerCallback]
    public void SetTickRate(int newTickRate) {
        config.tickRate = math.max(1, newTickRate);
        
        // 設定変更後にサーバー側を再初期化
        InitializeServerSide();
        
        if (config.showDebugInfo) {
            Debug.Log($"[MasterClock] Tick Rate changed to: {config.tickRate}Hz - server reinitialized");
        }
    }
    
    /// <summary>
    /// サーバーの完全再初期化（設定変更時など）
    /// </summary>
    [ServerCallback]
    public void ReinitializeServer() {
        config.ValidateSettings();
        InitializeServerSide();
        
        if (config.showDebugInfo) {
            Debug.Log($"[MasterClock] Server fully reinitialized with config: {config}");
        }
    }

    /// <summary>
    /// OSC メッセージからtick値を受信し、時刻同期処理を実行
    /// </summary>
    /// <param name="address">OSCメッセージのアドレス</param>
    /// <param name="data">受信したOSCデータ</param>
    /// <param name="_">未使用のOSC受信イベント引数</param>
    [ServerCallback]
    public void ListenTick(string address, ReceivedOscArguments data, OscReceiverEventArgs _) {
        if (!isActiveAndEnabled) return;
        if (!data.TryRead(out int tick)) {
            Debug.LogError($"[MasterClock] Failed to read tick from OSC message");
            return;
        }

        ProcessTick((uint)tick);
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
    public double GetSynchronizedTime() => NetworkTime.predictedTime + synchronizedOffset;
    
    /// <summary>
    /// リモートクライアントのための同期時刻
    /// </summary>
    /// <returns>リモート同期時刻</returns>
    public double GetRemoteSynchronizedTime() => 
        NetworkServer.active ? currentTickTime : NetworkTime.predictedTime + synchronizedOffset;
    
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
        NetworkServer.active ? (offsetEma.Value, offsetEma.Variance, offsetEma.StandardDeviation) : (synchronizedOffset, 0, 0);
    
    /// <summary>
    /// 最後に入力されたtick値を取得
    /// </summary>
    /// <returns>最後に入力されたtick値</returns>
    public uint GetLastInputTick() => tickCount;
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
            EditorGUILayout.LabelField($"Last Input Tick: {masterClock.GetLastInputTick()}");
            EditorGUILayout.LabelField($"Current Tick Time: {masterClock.GetRemoteSynchronizedTime():F4}s");
            EditorGUILayout.LabelField($"Network Predicted Time: {NetworkTime.predictedTime:F4}s");
            EditorGUILayout.LabelField($"Synchronized Offset: {masterClock.GetCurrentOffset():F4}s");
            EditorGUILayout.LabelField($"Synchronized Time: {masterClock.GetSynchronizedTime():F4}s");
            EditorGUILayout.LabelField($"Remote Sync Time: {masterClock.GetRemoteSynchronizedTime():F4}s");
            EditorGUILayout.LabelField($"EMA Duration: {masterClock.Settings.emaDuration} seconds");
            
            if (NetworkServer.active) {
                var stats = masterClock.GetEmaStatistics();
                EditorGUILayout.LabelField($"EMA Variance: {stats.variance:F6}");
                EditorGUILayout.LabelField($"EMA Standard Deviation: {stats.standardDeviation:F4}s");
                
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