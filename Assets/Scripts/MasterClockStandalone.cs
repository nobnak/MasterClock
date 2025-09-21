using UnityEngine;
using Unity.Mathematics;
using StarOSC;
using Mirror;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// スタンドアローン版マスタークロック - 外部からtick値を受け取り、Unityの時刻と同期する
/// </summary>
[DefaultExecutionOrder(-100)]
public class MasterClockStandalone : MonoBehaviour {
    
    
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
    
    /// <summary>
    /// ランタイム状態を管理する内部クラス（シリアライズ対象外）
    /// </summary>
    public class Runtime {
        public double synchronizedOffset = 0.0;
        public uint tickCount = 0;
        public double currentTickTime = 0.0;
        public double lastUpdateTime = 0.0;
        
        public ExponentialMovingAverage offsetEma;
        
        /// <summary>
        /// ランタイム状態を初期化
        /// </summary>
        /// <param name="emaSamples">EMAのサンプル数</param>
        /// <param name="currentTime">現在の時刻</param>
        public void Initialize(int emaSamples, double currentTime) {
            offsetEma = new ExponentialMovingAverage(emaSamples);
            lastUpdateTime = currentTime;
            
            // 初期状態をリセット
            tickCount = 0;
            currentTickTime = 0.0;
            synchronizedOffset = 0.0;
        }
        
        /// <summary>
        /// 状態をリセット
        /// </summary>
        public void Reset() {
            synchronizedOffset = 0.0;
            tickCount = 0;
            currentTickTime = 0.0;
            lastUpdateTime = 0.0;
            offsetEma.Reset();
        }
    }
    
    [SerializeField] private Config config = new Config();
    private Runtime runtime = new Runtime();
    
    /// <summary>
    /// 設定への読み取り専用アクセスを提供
    /// </summary>
    public Config Settings => config;

    #region Unity Lifecycle Methods
    private void OnEnable() {
        // 設定値を検証・修正
        config.ValidateSettings();
        
        // Runtimeインスタンスを新規作成してリセット
        runtime = new Runtime();
        
        if (config.showDebugInfo) {
            Debug.Log("[MasterClockStandalone] Component enabled, settings validated, and runtime reset");
        }
    }
    
    private void Start() {
        // 初期化処理
        Initialize();
        
        if (config.showDebugInfo) {
            Debug.Log($"[MasterClockStandalone] Started with config: {config}");
        }
    }
    #endregion

    #region Core Methods
    /// <summary>
    /// 初期化処理
    /// </summary>
    private void Initialize() {
        // ランタイム状態を初期化（期間 × tickRateでサンプル数を計算）
        runtime.Initialize(config.emaDuration * config.tickRate, Time.time);
    }
    
    /// <summary>
    /// 外部からtick値を受け取り、時刻同期処理を実行
    /// </summary>
    /// <param name="tickValue">外部から入力されるtick値</param>
    public void ProcessTick(uint tickValue) {
        // 受け取ったtick値を設定
        runtime.tickCount = tickValue;
        
        // tickCountを秒に変換
        runtime.currentTickTime = (double)runtime.tickCount / config.tickRate;
        
        // Unity Time.timeとの差分を計算
        double unityTime = Time.time;
        double timeDifference = runtime.currentTickTime - unityTime;
        
        // MirrorのEMAでオフセットを推定
        runtime.offsetEma.Add(timeDifference);
        
        // オフセットを更新
        runtime.synchronizedOffset = runtime.offsetEma.Value;
        
        // デバッグ情報を表示
        if (config.showDebugInfo) {
            Debug.Log($"[MasterClockStandalone] Input Tick: {tickValue}, " +
                     $"TickTime: {runtime.currentTickTime:F4}s, " +
                     $"UnityTime: {unityTime:F4}s, " +
                     $"Difference: {timeDifference:F4}s, " +
                     $"EMA Offset: {runtime.offsetEma.Value:F4}s, " +
                     $"EMA Variance: {runtime.offsetEma.Variance:F6}, " +
                     $"EMA StdDev: {runtime.offsetEma.StandardDeviation:F4}s");
        }
        
        runtime.lastUpdateTime = unityTime;
    }
    
    /// <summary>
    /// デバッグ用：現在の時刻から推定tick値を生成してProcessTickを呼び出す
    /// </summary>
    public void ProcessCurrentTimeTick() => ProcessTick((uint)math.round(Time.time * config.tickRate));
    
    /// <summary>
    /// EMAオフセットをリセット
    /// </summary>
    public void ResetOffset() {
        // 新しいRuntimeインスタンスを作成して再初期化
        runtime = new Runtime();
        Initialize();
        
        if (config.showDebugInfo) {
            Debug.Log("[MasterClockStandalone] State reset and reinitialized");
        }
    }
    
    /// <summary>
    /// EMAの期間を動的に変更
    /// </summary>
    /// <param name="newDuration">新しいEMA期間（秒）</param>
    public void SetEmaDuration(int newDuration) {
        config.emaDuration = math.max(1, newDuration);
        
        // 設定変更後に再初期化
        Initialize();
        
        if (config.showDebugInfo) {
            Debug.Log($"[MasterClockStandalone] EMA Duration changed to: {config.emaDuration} seconds - reinitialized");
        }
    }
    
    /// <summary>
    /// tickRateを動的に変更
    /// </summary>
    /// <param name="newTickRate">新しいtickRate</param>
    public void SetTickRate(int newTickRate) {
        config.tickRate = math.max(1, newTickRate);
        
        // 設定変更後に再初期化
        Initialize();
        
        if (config.showDebugInfo) {
            Debug.Log($"[MasterClockStandalone] Tick Rate changed to: {config.tickRate}Hz - reinitialized");
        }
    }
    
    /// <summary>
    /// 完全再初期化（設定変更時など）
    /// </summary>
    public void Reinitialize() {
        config.ValidateSettings();
        Initialize();
        
        if (config.showDebugInfo) {
            Debug.Log($"[MasterClockStandalone] Fully reinitialized with config: {config}");
        }
    }

    /// <summary>
    /// OSC メッセージからtick値を受信し、時刻同期処理を実行
    /// </summary>
    /// <param name="address">OSCメッセージのアドレス</param>
    /// <param name="data">受信したOSCデータ</param>
    /// <param name="_">未使用のOSC受信イベント引数</param>
    public void ListenTick(string address, ReceivedOscArguments data, OscReceiverEventArgs _) {
        #if UNITY_EDITOR
        Debug.Log($"[MasterClockStandalone] ListenTick: {address}, {data}, {_}");
        #endif
        
        if (!isActiveAndEnabled) return;
        
        if (!data.TryRead(out int tick)) {
            Debug.LogError($"[MasterClockStandalone] Failed to read tick from OSC message");
            return;
        }

        ProcessTick((uint)tick);
    }
    #endregion

    #region Query Methods
    /// <summary>
    /// 同期された時刻を取得
    /// </summary>
    /// <returns>同期された時刻</returns>
    public double GetSynchronizedTime() => Time.time + runtime.synchronizedOffset;
    
    /// <summary>
    /// 同期された時刻を取得（リモート用の互換メソッド）
    /// </summary>
    /// <returns>同期された時刻</returns>
    public double GetRemoteSynchronizedTime() => runtime.currentTickTime;
    
    /// <summary>
    /// 現在のオフセット値を取得
    /// </summary>
    /// <returns>推定されたオフセット値</returns>
    public double GetCurrentOffset() => runtime.synchronizedOffset;
    
    /// <summary>
    /// EMAの統計情報を取得
    /// </summary>
    /// <returns>EMAの値、分散、標準偏差</returns>
    public (double value, double variance, double standardDeviation) GetEmaStatistics() => 
        (runtime.offsetEma.Value, runtime.offsetEma.Variance, runtime.offsetEma.StandardDeviation);
    
    /// <summary>
    /// 最後に入力されたtick値を取得
    /// </summary>
    /// <returns>最後に入力されたtick値</returns>
    public uint GetLastInputTick() => runtime.tickCount;
    
    /// <summary>
    /// 現在の Unity 時刻を取得
    /// </summary>
    /// <returns>Unity の Time.time</returns>
    public double GetUnityTime() => Time.time;
    
    /// <summary>
    /// 最後に計算されたtick時刻を取得
    /// </summary>
    /// <returns>tick時刻</returns>
    public double GetCurrentTickTime() => runtime.currentTickTime;
    #endregion

    #region Editor
#if UNITY_EDITOR
    [CustomEditor(typeof(MasterClockStandalone))]
    public class MasterClockStandaloneEditor : Editor {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();
            
            MasterClockStandalone masterClock = (MasterClockStandalone)target;
            
            if (!masterClock.config.showDebugInfo) return;
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("=== Master Clock Standalone Debug Info ===", EditorStyles.boldLabel);
            
            // runtime変数に直接アクセス（インナークラスの特権）
            EditorGUILayout.LabelField($"Last Input Tick: {masterClock.runtime.tickCount}");
            EditorGUILayout.LabelField($"Current Tick Time: {masterClock.runtime.currentTickTime:F4}s");
            EditorGUILayout.LabelField($"Unity Time: {Time.time:F4}s");
            EditorGUILayout.LabelField($"Synchronized Offset: {masterClock.runtime.synchronizedOffset:F4}s");
            EditorGUILayout.LabelField($"Synchronized Time: {Time.time + masterClock.runtime.synchronizedOffset:F4}s");
            EditorGUILayout.LabelField($"EMA Duration: {masterClock.config.emaDuration} seconds");
            
            EditorGUILayout.LabelField($"EMA Variance: {masterClock.runtime.offsetEma.Variance:F6}");
            EditorGUILayout.LabelField($"EMA Standard Deviation: {masterClock.runtime.offsetEma.StandardDeviation:F4}s");
            
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset")) {
                masterClock.ResetOffset();
            }
            if (GUILayout.Button("Test Tick")) {
                masterClock.ProcessCurrentTimeTick();
            }
            if (GUILayout.Button("Reinit")) {
                masterClock.Reinitialize();
            }
            EditorGUILayout.EndHorizontal();
            
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
