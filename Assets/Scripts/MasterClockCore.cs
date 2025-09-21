using UnityEngine;
using Unity.Mathematics;
using StarOSC;
using Mirror;

/// <summary>
/// MasterClockとMasterClockStandaloneの共通インターフェイス
/// </summary>
public interface IMasterClock
{
    #region Properties
    /// <summary>
    /// 設定への読み取り専用アクセスを提供
    /// </summary>
    MasterClockCore.Config Settings { get; }
    #endregion

    #region Core Methods
    /// <summary>
    /// 外部からtick値を受け取り、時刻同期処理を実行
    /// </summary>
    /// <param name="tickValue">外部から入力されるtick値</param>
    void ProcessTick(uint tickValue);

    /// <summary>
    /// デバッグ用：現在の時刻から推定tick値を生成してProcessTickを呼び出す
    /// </summary>
    void ProcessCurrentTimeTick();

    /// <summary>
    /// EMAオフセットをリセット
    /// </summary>
    void ResetOffset();

    /// <summary>
    /// EMAの期間を動的に変更
    /// </summary>
    /// <param name="newDuration">新しいEMA期間（秒）</param>
    void SetEmaDuration(int newDuration);

    /// <summary>
    /// tickRateを動的に変更
    /// </summary>
    /// <param name="newTickRate">新しいtickRate</param>
    void SetTickRate(int newTickRate);

    /// <summary>
    /// 完全再初期化（設定変更時など）
    /// </summary>
    void Reinitialize();
    #endregion

    #region Query Methods
    /// <summary>
    /// 同期された時刻を取得
    /// </summary>
    /// <returns>同期された時刻</returns>
    double GetSynchronizedTime();

    /// <summary>
    /// 同期された時刻を取得（リモート用の互換メソッド）
    /// </summary>
    /// <returns>同期された時刻</returns>
    double GetRemoteSynchronizedTime();

    /// <summary>
    /// 現在のオフセット値を取得
    /// </summary>
    /// <returns>推定されたオフセット値</returns>
    double GetCurrentOffset();

    /// <summary>
    /// EMAの統計情報を取得
    /// </summary>
    /// <returns>EMAの値、分散、標準偏差</returns>
    (double value, double variance, double standardDeviation) GetEmaStatistics();

    /// <summary>
    /// 最後に入力されたtick値を取得
    /// </summary>
    /// <returns>最後に入力されたtick値</returns>
    uint GetLastInputTick();
    
    /// <summary>
    /// 現在の時刻を取得
    /// </summary>
    /// <returns>現在の時刻</returns>
    double GetCurrentTime();
    
    /// <summary>
    /// 最後に計算されたtick時刻を取得
    /// </summary>
    /// <returns>tick時刻</returns>
    double GetCurrentTickTime();
    #endregion
}

/// <summary>
/// MasterClockの共通ロジックを提供するコアクラス（MonoBehaviour非依存）
/// </summary>
public class MasterClockCore 
{
    [System.Serializable]
    public class Config 
    {
        [Header("時刻同期設定")]
        [SerializeField] public int tickRate = 30; // 1秒に30tick
        [SerializeField] public int emaDuration = 1; // EMAの期間（秒）
        
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
    /// ランタイム状態を管理するクラス
    /// </summary>
    public class Runtime 
    {
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
        public void Initialize(int emaSamples, double currentTime) 
        {
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
        public void Reset() 
        {
            synchronizedOffset = 0.0;
            tickCount = 0;
            currentTickTime = 0.0;
            lastUpdateTime = 0.0;
            offsetEma.Reset();
        }
    }

    /// <summary>
    /// デバッグ情報のコールバック
    /// </summary>
    public delegate void DebugLogCallback(string message);
    
    /// <summary>
    /// オフセット更新のコールバック
    /// </summary>
    public delegate void OffsetUpdatedCallback(double newOffset);

    // コア状態
    public Config config { get; private set; }
    public Runtime runtime { get; private set; }
    
    // コールバック
    public DebugLogCallback OnDebugLog;
    public OffsetUpdatedCallback OnOffsetUpdated;
    
    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="config">設定</param>
    public MasterClockCore(Config config) 
    {
        this.config = config;
        this.runtime = new Runtime();
    }
    
    /// <summary>
    /// 初期化処理
    /// </summary>
    /// <param name="currentTime">現在の時刻</param>
    public void Initialize(double currentTime) 
    {
        config.ValidateSettings();
        
        // ランタイム状態を初期化（期間 × tickRateでサンプル数を計算）
        runtime.Initialize(config.emaDuration * config.tickRate, currentTime);
        
        if (config.showDebugInfo) 
        {
            OnDebugLog?.Invoke("MasterClockCore initialized");
        }
    }
    
    /// <summary>
    /// 外部からtick値を受け取り、時刻同期処理を実行
    /// </summary>
    /// <param name="tickValue">外部から入力されるtick値</param>
    /// <param name="currentTime">現在の基準時刻</param>
    /// <param name="timeSourceName">時刻ソース名（デバッグ用）</param>
    public void ProcessTick(uint tickValue, double currentTime, string timeSourceName = "Time") 
    {
        // 受け取ったtick値を設定
        runtime.tickCount = tickValue;
        
        // tickCountを秒に変換
        runtime.currentTickTime = (double)runtime.tickCount / config.tickRate;
        
        // 基準時刻との差分を計算
        double timeDifference = runtime.currentTickTime - currentTime;
        
        // MirrorのEMAでオフセットを推定
        runtime.offsetEma.Add(timeDifference);
        
        // オフセットを更新
        runtime.synchronizedOffset = runtime.offsetEma.Value;
        OnOffsetUpdated?.Invoke(runtime.synchronizedOffset);
        
        // デバッグ情報を表示
        if (config.showDebugInfo) 
        {
            var debugMessage = $"Input Tick: {tickValue}, " +
                             $"TickTime: {runtime.currentTickTime:F4}s, " +
                             $"{timeSourceName}: {currentTime:F4}s, " +
                             $"Difference: {timeDifference:F4}s, " +
                             $"EMA Offset: {runtime.offsetEma.Value:F4}s, " +
                             $"EMA Variance: {runtime.offsetEma.Variance:F6}, " +
                             $"EMA StdDev: {runtime.offsetEma.StandardDeviation:F4}s";
                             
            OnDebugLog?.Invoke(debugMessage);
        }
        
        runtime.lastUpdateTime = currentTime;
    }
    
    /// <summary>
    /// デバッグ用：現在の時刻から推定tick値を生成してProcessTickを呼び出す
    /// </summary>
    /// <param name="currentTime">現在の時刻</param>
    /// <param name="timeSourceName">時刻ソース名（デバッグ用）</param>
    public void ProcessCurrentTimeTick(double currentTime, string timeSourceName = "Time") 
        => ProcessTick((uint)math.round(currentTime * config.tickRate), currentTime, timeSourceName);
    
    /// <summary>
    /// EMAオフセットをリセット
    /// </summary>
    /// <param name="currentTime">現在の時刻</param>
    public void ResetOffset(double currentTime) 
    {
        // 新しいRuntimeインスタンスを作成して再初期化
        runtime = new Runtime();
        Initialize(currentTime);
        
        if (config.showDebugInfo) 
        {
            OnDebugLog?.Invoke("State reset and reinitialized");
        }
    }
    
    /// <summary>
    /// EMAの期間を動的に変更
    /// </summary>
    /// <param name="newDuration">新しいEMA期間（秒）</param>
    /// <param name="currentTime">現在の時刻</param>
    public void SetEmaDuration(int newDuration, double currentTime) 
    {
        config.emaDuration = math.max(1, newDuration);
        
        // 設定変更後に再初期化
        Initialize(currentTime);
        
        if (config.showDebugInfo) 
        {
            OnDebugLog?.Invoke($"EMA Duration changed to: {config.emaDuration} seconds - reinitialized");
        }
    }
    
    /// <summary>
    /// tickRateを動的に変更
    /// </summary>
    /// <param name="newTickRate">新しいtickRate</param>
    /// <param name="currentTime">現在の時刻</param>
    public void SetTickRate(int newTickRate, double currentTime) 
    {
        config.tickRate = math.max(1, newTickRate);
        
        // 設定変更後に再初期化
        Initialize(currentTime);
        
        if (config.showDebugInfo) 
        {
            OnDebugLog?.Invoke($"Tick Rate changed to: {config.tickRate}Hz - reinitialized");
        }
    }
    
    /// <summary>
    /// 完全再初期化（設定変更時など）
    /// </summary>
    /// <param name="currentTime">現在の時刻</param>
    public void Reinitialize(double currentTime) 
    {
        config.ValidateSettings();
        Initialize(currentTime);
        
        if (config.showDebugInfo) 
        {
            OnDebugLog?.Invoke($"Fully reinitialized with config: {config}");
        }
    }

    /// <summary>
    /// OSC メッセージからtick値を受信し、時刻同期処理を実行
    /// </summary>
    /// <param name="data">受信したOSCデータ</param>
    /// <param name="currentTime">現在の時刻</param>
    /// <param name="timeSourceName">時刻ソース名（デバッグ用）</param>
    /// <returns>処理が成功したかどうか</returns>
    public bool ProcessOscTick(ReceivedOscArguments data, double currentTime, string timeSourceName = "Time") 
    {
        if (!data.TryRead(out int tick)) 
        {
            OnDebugLog?.Invoke("Failed to read tick from OSC message");
            return false;
        }

        ProcessTick((uint)tick, currentTime, timeSourceName);
        return true;
    }
    
    /// <summary>
    /// 同期された時刻を計算
    /// </summary>
    /// <param name="currentTime">現在の基準時刻</param>
    /// <returns>同期された時刻</returns>
    public double GetSynchronizedTime(double currentTime) 
        => currentTime + runtime.synchronizedOffset;
    
    /// <summary>
    /// 同期された時刻を取得（リモート用の互換メソッド）
    /// </summary>
    /// <returns>同期された時刻</returns>
    public double GetRemoteSynchronizedTime() 
        => runtime.currentTickTime;
    
    /// <summary>
    /// 現在のオフセット値を取得
    /// </summary>
    /// <returns>推定されたオフセット値</returns>
    public double GetCurrentOffset() 
        => runtime.synchronizedOffset;
    
    /// <summary>
    /// EMAの統計情報を取得
    /// </summary>
    /// <returns>EMAの値、分散、標準偏差</returns>
    public (double value, double variance, double standardDeviation) GetEmaStatistics() 
        => (runtime.offsetEma.Value, runtime.offsetEma.Variance, runtime.offsetEma.StandardDeviation);
    
    /// <summary>
    /// 最後に入力されたtick値を取得
    /// </summary>
    /// <returns>最後に入力されたtick値</returns>
    public uint GetLastInputTick() 
        => runtime.tickCount;
}
