using UnityEngine;
using Unity.Mathematics;
using System;

/// <summary>
/// 独自実装の指数移動平均（Exponential Moving Average）スレッドセーフクラス
/// N-day EMA implementation for calculating exponential moving average
/// https://en.wikipedia.org/wiki/Moving_average#Exponential_moving_average
/// </summary>
public class ExponentialMovingAverage
{
    private readonly object _lock = new object();
    readonly double alpha;
    bool initialized;

    public double Value { get; private set; }
    public double Variance { get; private set; }
    public double StandardDeviation { get; private set; } // absolute value

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="n">EMAの期間（サンプル数）</param>
    public ExponentialMovingAverage(int n)
    {
        // standard N-day EMA alpha calculation
        alpha = 2.0 / (n + 1);
        initialized = false;
        Value = 0;
        Variance = 0;
        StandardDeviation = 0;
    }

    /// <summary>
    /// 新しい値をEMAに追加
    /// </summary>
    /// <param name="newValue">追加する値</param>
    public void Add(double newValue)
    {
        lock (_lock)
        {
            // simple algorithm for EMA described here:
            // https://en.wikipedia.org/wiki/Moving_average#Exponentially_weighted_moving_variance_and_standard_deviation
            if (initialized)
            {
                double delta = newValue - Value;
                Value += alpha * delta;
                Variance = (1 - alpha) * (Variance + alpha * delta * delta);
                StandardDeviation = Math.Sqrt(Variance);
            }
            else
            {
                Value = newValue;
                initialized = true;
            }
        }
    }

    /// <summary>
    /// EMAの状態をリセット
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            initialized = false;
            Value = 0;
            Variance = 0;
            StandardDeviation = 0;
        }
    }

    /// <summary>
    /// EMAの統計情報をスレッドセーフに取得
    /// </summary>
    /// <returns>値、分散、標準偏差のタプル</returns>
    public (double value, double variance, double standardDeviation) GetStatistics()
    {
        lock (_lock)
        {
            return (Value, Variance, StandardDeviation);
        }
    }
}

/// <summary>
/// MasterClockのクエリ専用インターフェイス（時刻取得系メソッド）
/// </summary>
public interface IMasterClockQuery
{
    #region Properties
    /// <summary>
    /// 設定への読み取り専用アクセスを提供
    /// </summary>
    MasterClock.Config Settings { get; }
    string name { get; }
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
/// MasterClockとMasterClockStandaloneの共通インターフェイス（操作系メソッド + クエリインターフェイス）
/// </summary>
public interface IMasterClock : IMasterClockQuery
{
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
}

/// <summary>
/// MasterClockの共通ロジックを提供するスレッドセーフコアクラス（MonoBehaviour非依存）
/// </summary>
public class MasterClock 
{
    /// <summary>
    /// グローバルアクセス用のstatic変数
    /// </summary>
    private static IMasterClockQuery globalInstance;
    
    /// <summary>
    /// グローバルなMasterClockインスタンスを取得
    /// </summary>
    public static IMasterClockQuery Global => globalInstance;
    
    /// <summary>
    /// グローバルインスタンスを設定（内部使用）
    /// </summary>
    internal static void SetGlobalInstance(IMasterClockQuery instance) => globalInstance = instance;
    
    /// <summary>
    /// グローバルインスタンスをクリア（内部使用）
    /// </summary>
    internal static void ClearGlobalInstance(IMasterClockQuery instance) {
        if (globalInstance == instance) {
            globalInstance = null;
        }
    }
    private readonly object _lock = new object();
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
    public MasterClock(Config config) 
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
        lock (_lock)
        {
            config.ValidateSettings();
            
            // ランタイム状態を初期化（期間 × tickRateでサンプル数を計算）
            runtime.Initialize(config.emaDuration * config.tickRate, currentTime);
            
            if (config.showDebugInfo) 
            {
                OnDebugLog?.Invoke("MasterClock initialized");
            }
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
        lock (_lock)
        {
            // 受け取ったtick値を設定
            runtime.tickCount = tickValue;
            
            // tickCountを秒に変換
            runtime.currentTickTime = (double)runtime.tickCount / config.tickRate;
            
            // 基準時刻との差分を計算
            double timeDifference = runtime.currentTickTime - currentTime;
            
            // MirrorのEMAでオフセットを推定
            runtime.offsetEma.Add(timeDifference);
            
            // オフセットを更新（スレッドセーフに統計情報を取得）
            var emaStats = runtime.offsetEma.GetStatistics();
            runtime.synchronizedOffset = emaStats.value;
            OnOffsetUpdated?.Invoke(runtime.synchronizedOffset);
            
            // デバッグ情報を表示
            if (config.showDebugInfo) 
            {
                var debugMessage = $"Input Tick: {tickValue}, " +
                                 $"TickTime: {runtime.currentTickTime:F4}s, " +
                                 $"{timeSourceName}: {currentTime:F4}s, " +
                                 $"Difference: {timeDifference:F4}s, " +
                                 $"EMA Offset: {emaStats.value:F4}s, " +
                                 $"EMA Variance: {emaStats.variance * 1000000:F3}ms², " +
                                 $"EMA StdDev: {emaStats.standardDeviation * 1000:F3}ms";
                                 
                OnDebugLog?.Invoke(debugMessage);
            }
            
            runtime.lastUpdateTime = currentTime;
        }
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
        lock (_lock)
        {
            // 新しいRuntimeインスタンスを作成して再初期化
            runtime = new Runtime();
            
            // ロック内でInitializeの処理を実行（再帰ロックを避けるため直接実行）
            config.ValidateSettings();
            runtime.Initialize(config.emaDuration * config.tickRate, currentTime);
            
            if (config.showDebugInfo) 
            {
                OnDebugLog?.Invoke("State reset and reinitialized");
            }
        }
    }
    
    /// <summary>
    /// EMAの期間を動的に変更
    /// </summary>
    /// <param name="newDuration">新しいEMA期間（秒）</param>
    /// <param name="currentTime">現在の時刻</param>
    public void SetEmaDuration(int newDuration, double currentTime) 
    {
        lock (_lock)
        {
            config.emaDuration = math.max(1, newDuration);
            
            // ロック内で初期化処理を実行
            config.ValidateSettings();
            runtime.Initialize(config.emaDuration * config.tickRate, currentTime);
            
            if (config.showDebugInfo) 
            {
                OnDebugLog?.Invoke($"EMA Duration changed to: {config.emaDuration} seconds - reinitialized");
            }
        }
    }
    
    /// <summary>
    /// tickRateを動的に変更
    /// </summary>
    /// <param name="newTickRate">新しいtickRate</param>
    /// <param name="currentTime">現在の時刻</param>
    public void SetTickRate(int newTickRate, double currentTime) 
    {
        lock (_lock)
        {
            config.tickRate = math.max(1, newTickRate);
            
            // ロック内で初期化処理を実行
            config.ValidateSettings();
            runtime.Initialize(config.emaDuration * config.tickRate, currentTime);
            
            if (config.showDebugInfo) 
            {
                OnDebugLog?.Invoke($"Tick Rate changed to: {config.tickRate}Hz - reinitialized");
            }
        }
    }
    
    /// <summary>
    /// 完全再初期化（設定変更時など）
    /// </summary>
    /// <param name="currentTime">現在の時刻</param>
    public void Reinitialize(double currentTime) 
    {
        lock (_lock)
        {
            config.ValidateSettings();
            runtime.Initialize(config.emaDuration * config.tickRate, currentTime);
            
            if (config.showDebugInfo) 
            {
                OnDebugLog?.Invoke($"Fully reinitialized with config: {config}");
            }
        }
    }

    
    /// <summary>
    /// 同期された時刻を計算
    /// </summary>
    /// <param name="currentTime">現在の基準時刻</param>
    /// <returns>同期された時刻</returns>
    public double GetSynchronizedTime(double currentTime) 
    {
        lock (_lock)
        {
            return currentTime + runtime.synchronizedOffset;
        }
    }
    
    /// <summary>
    /// 同期された時刻を取得（リモート用の互換メソッド）
    /// </summary>
    /// <returns>同期された時刻</returns>
    public double GetRemoteSynchronizedTime() 
    {
        lock (_lock)
        {
            return runtime.currentTickTime;
        }
    }
    
    /// <summary>
    /// 現在のオフセット値を取得
    /// </summary>
    /// <returns>推定されたオフセット値</returns>
    public double GetCurrentOffset() 
    {
        lock (_lock)
        {
            return runtime.synchronizedOffset;
        }
    }
    
    /// <summary>
    /// EMAの統計情報を取得
    /// </summary>
    /// <returns>EMAの値、分散、標準偏差</returns>
    public (double value, double variance, double standardDeviation) GetEmaStatistics() 
    {
        lock (_lock)
        {
            return runtime.offsetEma.GetStatistics();
        }
    }
    
    /// <summary>
    /// 最後に入力されたtick値を取得
    /// </summary>
    /// <returns>最後に入力されたtick値</returns>
    public uint GetLastInputTick() 
    {
        lock (_lock)
        {
            return runtime.tickCount;
        }
    }
}