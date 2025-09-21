using UnityEngine;
using Unity.Mathematics;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// スタンドアローン版マスタークロック - 外部からtick値を受け取り、Unityの時刻と同期する
/// </summary>
[DefaultExecutionOrder(-100)]
public class MasterClockStandalone : MonoBehaviour, IMasterClock {
    
    [SerializeField] private MasterClockCore.Config config = new MasterClockCore.Config();
    internal MasterClockCore core;  // エディターからアクセスできるようにinternal
    
    /// <summary>
    /// 設定への読み取り専用アクセスを提供
    /// </summary>
    public MasterClockCore.Config Settings => config;

    #region Unity Lifecycle Methods
    private void OnEnable() {
        // MasterClockCoreインスタンスを作成
        core = new MasterClockCore(config);
        
        // デバッグログのコールバックを設定
        core.OnDebugLog += (message) => Debug.Log($"[MasterClockStandalone] {message}");
        
        // 初期化処理
        core.Initialize(GetCurrentTime());
        
        if (config.showDebugInfo) {
            Debug.Log($"[MasterClockStandalone] Component enabled, core created and initialized with config: {config}");
        }
    }
    #endregion

    #region Core Methods
    
    /// <summary>
    /// 外部からtick値を受け取り、時刻同期処理を実行
    /// </summary>
    /// <param name="tickValue">外部から入力されるtick値</param>
    public void ProcessTick(uint tickValue) {
        core.ProcessTick(tickValue, GetCurrentTime(), "UnityTime");
    }
    
    /// <summary>
    /// デバッグ用：現在の時刻から推定tick値を生成してProcessTickを呼び出す
    /// </summary>
    public void ProcessCurrentTimeTick() => core.ProcessCurrentTimeTick(GetCurrentTime(), "UnityTime");
    
    /// <summary>
    /// EMAオフセットをリセット
    /// </summary>
    public void ResetOffset() {
        core.ResetOffset(GetCurrentTime());
    }
    
    /// <summary>
    /// EMAの期間を動的に変更
    /// </summary>
    /// <param name="newDuration">新しいEMA期間（秒）</param>
    public void SetEmaDuration(int newDuration) {
        core.SetEmaDuration(newDuration, GetCurrentTime());
    }
    
    /// <summary>
    /// tickRateを動的に変更
    /// </summary>
    /// <param name="newTickRate">新しいtickRate</param>
    public void SetTickRate(int newTickRate) {
        core.SetTickRate(newTickRate, GetCurrentTime());
    }
    
    /// <summary>
    /// 完全再初期化（設定変更時など）
    /// </summary>
    public void Reinitialize() {
        core.Reinitialize(GetCurrentTime());
    }

    #endregion

    #region Query Methods
    /// <summary>
    /// 同期された時刻を取得
    /// </summary>
    /// <returns>同期された時刻</returns>
    public double GetSynchronizedTime() => core?.GetSynchronizedTime(GetCurrentTime()) ?? 0.0;
    
    /// <summary>
    /// 同期された時刻を取得（リモート用の互換メソッド）
    /// </summary>
    /// <returns>同期された時刻</returns>
    public double GetRemoteSynchronizedTime() => core?.GetRemoteSynchronizedTime() ?? 0.0;
    
    /// <summary>
    /// 現在のオフセット値を取得
    /// </summary>
    /// <returns>推定されたオフセット値</returns>
    public double GetCurrentOffset() => core?.GetCurrentOffset() ?? 0.0;
    
    /// <summary>
    /// EMAの統計情報を取得
    /// </summary>
    /// <returns>EMAの値、分散、標準偏差</returns>
    public (double value, double variance, double standardDeviation) GetEmaStatistics() => 
        core?.GetEmaStatistics() ?? (0.0, 0.0, 0.0);
    
    /// <summary>
    /// 最後に入力されたtick値を取得
    /// </summary>
    /// <returns>最後に入力されたtick値</returns>
    public uint GetLastInputTick() => core?.GetLastInputTick() ?? 0;
    
    /// <summary>
    /// 最後に計算されたtick時刻を取得
    /// </summary>
    /// <returns>tick時刻</returns>
    public double GetCurrentTickTime() => core?.runtime.currentTickTime ?? 0.0;
    
    /// <summary>
    /// 現在の時刻を取得（Unity Time.timeAsDouble を使用）
    /// </summary>
    /// <returns>現在の時刻</returns>
    public virtual double GetCurrentTime() => Time.timeAsDouble;
    #endregion

    #region Editor
#if UNITY_EDITOR
    [CustomEditor(typeof(MasterClockStandalone))]
    public class MasterClockStandaloneEditor : Editor {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();
            
            MasterClockStandalone masterClock = (MasterClockStandalone)target;
            
            if (!masterClock.Settings.showDebugInfo) return;
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("=== Master Clock Standalone Debug Info ===", EditorStyles.boldLabel);
            
            // coreが初期化されているかチェック
            if (masterClock.core == null) {
                EditorGUILayout.LabelField("Core not initialized (component not enabled or started)");
                return;
            }
            
            // coreから情報を取得
            EditorGUILayout.LabelField($"Last Input Tick: {masterClock.GetLastInputTick()}");
            EditorGUILayout.LabelField($"Current Tick Time: {masterClock.GetCurrentTickTime():F4}s");
            EditorGUILayout.LabelField($"Unity Time: {masterClock.GetCurrentTime():F4}s");
            EditorGUILayout.LabelField($"Synchronized Offset: {masterClock.GetCurrentOffset():F4}s");
            EditorGUILayout.LabelField($"Synchronized Time: {masterClock.GetSynchronizedTime():F4}s");
            EditorGUILayout.LabelField($"EMA Duration: {masterClock.Settings.emaDuration} seconds");
            
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
