using UnityEngine;
using Mirror;
using Unity.Mathematics;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Nobnak.MasterClock {

    /// <summary>
    /// 外部からtick値を受け取り、NetworkTime.predictedTimeと同期し、MirrorのEMAでオフセットを推定するマスタークロック（ネットワーク版）
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    [DefaultExecutionOrder(-100)]
    public class MasterClockNet : NetworkBehaviour, IMasterClock {
        
        [SerializeField] private MasterClock.Config config = new MasterClock.Config();
        internal MasterClock core;  // エディターからアクセスできるようにinternal
        
        /// <summary>
        /// 設定への読み取り専用アクセスを提供
        /// </summary>
        public MasterClock.Config Settings => config;
        
        /// <summary>
        /// このMasterClockの名前（IMasterClockQueryインターフェイス実装）
        /// </summary>
        public new string name => gameObject.name;
        
        // SyncVarでクライアントに共有するオフセット
        [SyncVar(hook = nameof(OnOffsetChanged))]
        private double synchronizedOffset = 0.0;
        
        // クライアント側EMAスムーズ化のための変数
        private double smoothedOffset = 0.0;
        private Coroutine smoothingCoroutine;
        private readonly float emaAlpha = 0.1f;  // EMA smoothing factor
        private readonly float updateInterval = 0.016f;  // 約60Hz更新

        #region Unity Lifecycle Methods
        private void OnEnable() {
            // MasterClockインスタンスを作成
            core = new MasterClock(config);
            
            // デバッグログのコールバックを設定
            core.OnDebugLog += (message) => Debug.Log($"[MasterClockNet] {message}");
            
            // オフセット更新コールバックを設定
            core.OnOffsetUpdated += (offset) => synchronizedOffset = offset;
            
            if (config.showDebugInfo) {
                Debug.Log("[MasterClockNet] Component enabled and core created");
            }
            
            // グローバルインスタンスとして登録
            MasterClock.SetGlobalInstance(this);
        }
        
        public override void OnStartServer() {
            base.OnStartServer();
            
            // サーバー側の初期化
            core.Initialize(GetCurrentTime());
            
            if (config.showDebugInfo) {
                Debug.Log($"[MasterClockNet] Server started with config: {config}");
            }
        }
        
        public override void OnStartClient() {
            base.OnStartClient();
            
            // クライアント側でのスムーズ化開始
            if (!NetworkServer.active) {
                smoothedOffset = synchronizedOffset;
                StartSmoothingCoroutine();
                if (config.showDebugInfo) {
                    Debug.Log("[MasterClockNet Client] EMA smoothing started");
                }
            }
        }
        
        public override void OnStopClient() {
            base.OnStopClient();
            StopSmoothingCoroutine();
        }
        
        private void OnDisable() {
            // グローバルインスタンスをクリア
            MasterClock.ClearGlobalInstance(this);
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
        /// 外部からtick値とtime値を受け取り、時刻同期処理を実行
        /// </summary>
        /// <param name="tickValue">外部から入力されるtick値</param>
        /// <param name="timeValue">外部から入力されるtime値</param>
        [ServerCallback]
        public void ProcessTick(uint tickValue, double timeValue) {
            core.ProcessTick(tickValue, timeValue, "ExternalTime");
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
                Debug.Log($"[MasterClockNet Client] Offset updated: {oldOffset:F4}s -> {newOffset:F4}s");
            }
        }
        
        #endregion
        
        #region EMA Smoothing Methods
        
        private void StartSmoothingCoroutine() {
            StopSmoothingCoroutine();
            smoothingCoroutine = StartCoroutine(SmoothOffsetRoutine());
        }
        
        private void StopSmoothingCoroutine() {
            if (smoothingCoroutine != null) {
                StopCoroutine(smoothingCoroutine);
                smoothingCoroutine = null;
            }
        }
        
        private IEnumerator SmoothOffsetRoutine() {
            var wait = new WaitForSeconds(updateInterval);
            
            while (!NetworkServer.active) {
                // EMA: smoothed = α * target + (1-α) * smoothed
                smoothedOffset = math.lerp(smoothedOffset, synchronizedOffset, emaAlpha);
                yield return wait;
            }
        }
        #endregion

        #region Common Methods (Client & Server)
        /// <summary>
        /// 同期された時刻を取得（クライアント・サーバー共通）
        /// </summary>
        /// <returns>同期された時刻</returns>
        public double GetSynchronizedTime() => GetCurrentTime() + GetEffectiveOffset();
        
        /// <summary>
        /// リモートクライアントのための同期時刻
        /// </summary>
        /// <returns>リモート同期時刻</returns>
        public double GetRemoteSynchronizedTime() => 
            NetworkServer.active ? (core?.GetRemoteSynchronizedTime() ?? 0.0) : GetCurrentTime() + GetEffectiveOffset();
        
        /// <summary>
        /// 現在のオフセット値を取得（クライアントではスムーズ化されたオフセット）
        /// </summary>
        /// <returns>推定されたオフセット値</returns>
        public double GetCurrentOffset() => GetEffectiveOffset();
        
        /// <summary>
        /// 実際に使用されるオフセット値を取得（サーバー：同期オフセット、クライアント：スムーズ化オフセット）
        /// </summary>
        /// <returns>有効なオフセット値</returns>
        private double GetEffectiveOffset() => NetworkServer.active ? synchronizedOffset : smoothedOffset;
        
        /// <summary>
        /// EMAの統計情報を取得（サーバー：内部EMA、クライアント：スムーズ化オフセット）
        /// </summary>
        /// <returns>EMAの値、分散、標準偏差</returns>
        public (double value, double variance, double standardDeviation) GetEmaStatistics() => 
            NetworkServer.active ? (core?.GetEmaStatistics() ?? (synchronizedOffset, 0, 0)) : (smoothedOffset, 0, 0);
        
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
        [CustomEditor(typeof(MasterClockNet))]
        public class MasterClockNetEditor : Editor {
            public override void OnInspectorGUI() {
                DrawDefaultInspector();
                
                MasterClockNet masterClock = (MasterClockNet)target;
                
                if (!masterClock.Settings.showDebugInfo) return;
                
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("=== Master Clock Net Debug Info ===", EditorStyles.boldLabel);
                
                EditorGUILayout.LabelField($"Is Server: {NetworkServer.active}");
                
                // coreが初期化されているかチェック
                if (masterClock.core == null) {
                    EditorGUILayout.LabelField("Core not initialized (component not enabled or started)");
                    return;
                }
                
                EditorGUILayout.LabelField($"Last Input Tick: {masterClock.GetLastInputTick()}");
                EditorGUILayout.LabelField($"Current Tick Time: {masterClock.GetRemoteSynchronizedTime():F4}s");
                EditorGUILayout.LabelField($"Network Predicted Time: {masterClock.GetCurrentTime():F4}s");
                
                if (NetworkServer.active) {
                    EditorGUILayout.LabelField($"Synchronized Offset: {masterClock.synchronizedOffset:F4}s");
                } else {
                    EditorGUILayout.LabelField($"Raw Synchronized Offset: {masterClock.synchronizedOffset:F4}s");
                    EditorGUILayout.LabelField($"EMA Smoothed Offset: {masterClock.smoothedOffset:F4}s");
                    EditorGUILayout.LabelField($"EMA Alpha: {masterClock.emaAlpha:F3}");
                }
                
                EditorGUILayout.LabelField($"Effective Offset: {masterClock.GetCurrentOffset():F4}s");
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

} // namespace Nobnak.MasterClock
