using UnityEngine;

namespace Nobnak.MasterClock {

/// <summary>
/// 誤差検証の詳細情報を格納する構造体
/// </summary>
public readonly struct ValidationInfo
{
    public readonly double unityTime;
    public readonly double threadSafeTime;
    public readonly double driftSeconds;
    public readonly double driftMilliseconds;
    public readonly bool isValid;
    public readonly string message;

    public ValidationInfo(double unityTime, double threadSafeTime, double driftSeconds, double driftMs, bool isValid, string message)
    {
        this.unityTime = unityTime;
        this.threadSafeTime = threadSafeTime;
        this.driftSeconds = driftSeconds;
        this.driftMilliseconds = driftMs;
        this.isValid = isValid;
        this.message = message;
    }

    public override string ToString()
        => $"ValidationInfo: Unity={unityTime:F6}s, ThreadSafe={threadSafeTime:F6}s, Drift={driftMilliseconds:F3}ms, Valid={isValid}, Message={message}";
}

/// <summary>
/// 統計情報を格納する構造体
/// </summary>
public readonly struct StatisticalInfo
{
    public readonly int sampleCount;
    public readonly double minDrift;
    public readonly double maxDrift;
    public readonly double meanDrift;
    public readonly double variance;
    public readonly double standardDeviation;

    public StatisticalInfo(int count, double min, double max, double mean, double variance, double stdDev)
    {
        sampleCount = count;
        minDrift = min;
        maxDrift = max;
        meanDrift = mean;
        this.variance = variance;
        standardDeviation = stdDev;
    }

    public override string ToString()
        => $"Statistical Analysis (n={sampleCount}):\n" +
           $"  Drift Range: {minDrift * 1000:F3}ms to {maxDrift * 1000:F3}ms\n" +
           $"  Mean Drift: {meanDrift * 1000:F3}ms\n" +
           $"  Standard Deviation: {standardDeviation * 1000:F3}ms\n" +
           $"  Variance: {variance * 1000000:F6}ms²";
}

/// <summary>
/// スレッドセーフな静的時刻取得クラス
/// RuntimeInitializeOnLoadMethodを使って初期化し、Stopwatchベースで高精度な時刻を提供
/// メインスレッド以外からもアクセス可能
/// </summary>
public static class ThreadSafeTime
{
    private static readonly object _lock = new object();
    private static readonly System.Diagnostics.Stopwatch _stopwatch = new System.Diagnostics.Stopwatch();
    private static double _startTimeOffset = 0.0;
    private static bool _initialized = false;

    /// <summary>
    /// Unity起動時に自動的に初期化される
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        lock (_lock)
        {
            if (_initialized)
                return;

            // Unity起動時の基準時刻を取得
            _startTimeOffset = Time.realtimeSinceStartupAsDouble;
            
            // Stopwatchを開始
            _stopwatch.Start();
            
            _initialized = true;
            
            Debug.Log($"[ThreadSafeTime] Initialized at Unity time: {_startTimeOffset:F6}s");
        }
    }

    /// <summary>
    /// 現在の時刻を取得（秒単位、double精度）
    /// Unity Time.realtimeSinceStartupAsDoubleと互換性がある
    /// </summary>
    /// <returns>起動からの経過時間（秒）</returns>
    public static double realtimeSinceStartupAsDouble
    {
        get
        {
            lock (_lock)
            {
                if (!_initialized)
                {
                    // 初期化されていない場合は強制初期化
                    ForceInitialize();
                }
                
                // Stopwatchの経過時間を秒に変換してオフセットに加算
                return _startTimeOffset + _stopwatch.Elapsed.TotalSeconds;
            }
        }
    }

    /// <summary>
    /// 現在の時刻を取得（ミリ秒単位、double精度）
    /// </summary>
    /// <returns>起動からの経過時間（ミリ秒）</returns>
    public static double realtimeSinceStartupAsMilliseconds
    {
        get
        {
            lock (_lock)
            {
                if (!_initialized)
                {
                    ForceInitialize();
                }
                
                return (_startTimeOffset * 1000.0) + _stopwatch.Elapsed.TotalMilliseconds;
            }
        }
    }

    /// <summary>
    /// Stopwatchの生の経過時間を取得（秒単位）
    /// </summary>
    /// <returns>Stopwatch開始からの経過時間（秒）</returns>
    public static double ElapsedSeconds
    {
        get
        {
            lock (_lock)
            {
                return _stopwatch.Elapsed.TotalSeconds;
            }
        }
    }

    /// <summary>
    /// 初期化状態を取得
    /// </summary>
    public static bool IsInitialized
    {
        get
        {
            lock (_lock)
            {
                return _initialized;
            }
        }
    }

    /// <summary>
    /// Unity基準時刻（Time.realtimeSinceStartupAsDouble）とのオフセットを取得
    /// </summary>
    public static double StartTimeOffset
    {
        get
        {
            lock (_lock)
            {
                return _startTimeOffset;
            }
        }
    }

    /// <summary>
    /// Stopwatchの精度情報を取得
    /// </summary>
    public static (long frequency, bool isHighResolution) StopwatchInfo
    {
        get
        {
            return (System.Diagnostics.Stopwatch.Frequency, System.Diagnostics.Stopwatch.IsHighResolution);
        }
    }

    /// <summary>
    /// 強制初期化（通常は自動で初期化されるため使用不要）
    /// </summary>
    public static void ForceInitialize()
    {
        lock (_lock)
        {
            if (_initialized)
                return;

            try
            {
                _startTimeOffset = Time.realtimeSinceStartupAsDouble;
            }
            catch (System.Exception)
            {
                // メインスレッド以外から呼ばれた場合、Timeにアクセスできないので0とする
                _startTimeOffset = 0.0;
                Debug.LogWarning("[ThreadSafeTime] Force initialized from non-main thread. StartTimeOffset set to 0.");
            }

            _stopwatch.Start();
            _initialized = true;
        }
    }

    /// <summary>
    /// Unity Time.realtimeSinceStartupAsDoubleとの差異を計算
    /// メインスレッドからのみ呼び出し可能
    /// </summary>
    /// <returns>ThreadSafeTime.realtimeSinceStartupAsDoubleとUnity Timeの差（秒）</returns>
    public static double GetDriftFromUnityTime()
    {
        if (!Application.isPlaying)
            return 0.0;

        try
        {
            double unityTime = Time.realtimeSinceStartupAsDouble;
            double threadSafeTime = realtimeSinceStartupAsDouble;
            return threadSafeTime - unityTime;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ThreadSafeTime] Could not access Unity Time (probably not on main thread): {ex.Message}");
            return 0.0;
        }
    }

    /// <summary>
    /// 詳細な誤差検証情報を取得
    /// </summary>
    /// <returns>誤差検証情報</returns>
    public static ValidationInfo GetValidationInfo()
    {
        if (!Application.isPlaying)
            return new ValidationInfo(0, 0, 0, 0, false, "Application not playing");

        try
        {
            double unityTime = Time.realtimeSinceStartupAsDouble;
            double threadSafeTime = realtimeSinceStartupAsDouble;
            double drift = threadSafeTime - unityTime;
            double driftMs = drift * 1000.0;

            return new ValidationInfo(unityTime, threadSafeTime, drift, driftMs, true, "Success");
        }
        catch (System.Exception ex)
        {
            return new ValidationInfo(0, 0, 0, 0, false, ex.Message);
        }
    }

    /// <summary>
    /// 一定時間の測定による統計的誤差検証を実行
    /// </summary>
    /// <param name="sampleCount">サンプル数（デフォルト1000）</param>
    /// <param name="intervalMs">測定間隔（ミリ秒、デフォルト1ms）</param>
    /// <returns>統計情報</returns>
    public static System.Collections.IEnumerator RunValidationTest(int sampleCount = 1000, int intervalMs = 1)
    {
        var samples = new System.Collections.Generic.List<double>();
        
        for (int i = 0; i < sampleCount; i++)
        {
            var info = GetValidationInfo();
            if (info.isValid)
            {
                samples.Add(info.driftSeconds);
            }
            
            yield return new WaitForSeconds(intervalMs / 1000.0f);
        }

        if (samples.Count > 0)
        {
            var stats = CalculateStatistics(samples);
            Debug.Log($"[ThreadSafeTime] Validation Test Results:\n{stats.ToString()}");
        }
        else
        {
            Debug.LogWarning("[ThreadSafeTime] No valid samples collected during validation test");
        }
    }

    /// <summary>
    /// 統計情報を計算
    /// </summary>
    private static StatisticalInfo CalculateStatistics(System.Collections.Generic.List<double> samples)
    {
        double sum = 0;
        double min = double.MaxValue;
        double max = double.MinValue;

        foreach (var sample in samples)
        {
            sum += sample;
            if (sample < min) min = sample;
            if (sample > max) max = sample;
        }

        double mean = sum / samples.Count;
        double varianceSum = 0;

        foreach (var sample in samples)
        {
            double diff = sample - mean;
            varianceSum += diff * diff;
        }

        double variance = varianceSum / samples.Count;
        double standardDeviation = System.Math.Sqrt(variance);

        return new StatisticalInfo(samples.Count, min, max, mean, variance, standardDeviation);
    }

    /// <summary>
    /// Unity Timeと再同期（メインスレッドからのみ呼び出し可能）
    /// </summary>
    public static void SynchronizeWithUnityTime()
    {
        lock (_lock)
        {
            try
            {
                double currentUnityTime = Time.realtimeSinceStartupAsDouble;
                double currentElapsed = _stopwatch.Elapsed.TotalSeconds;
                
                // 新しいオフセットを計算
                _startTimeOffset = currentUnityTime - currentElapsed;
                
                Debug.Log($"[ThreadSafeTime] Synchronized with Unity Time. New offset: {_startTimeOffset:F6}s");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ThreadSafeTime] Could not synchronize with Unity Time: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// デバッグ情報を取得（誤差検証情報を含む）
    /// </summary>
    /// <returns>デバッグ情報文字列</returns>
    public static string GetDebugInfo() 
    {
        lock (_lock)
        {
            var info = System.Diagnostics.Stopwatch.Frequency;
            var validation = GetValidationInfo();
            
            var debugInfo = $"ThreadSafeTime Debug Info:\n" +
                           $"  Initialized: {_initialized}\n" +
                           $"  Start Offset: {_startTimeOffset:F6}s\n" +
                           $"  Current Time: {realtimeSinceStartupAsDouble:F6}s\n" +
                           $"  Elapsed: {ElapsedSeconds:F6}s\n" +
                           $"  Stopwatch Frequency: {info:N0} Hz\n" +
                           $"  High Resolution: {System.Diagnostics.Stopwatch.IsHighResolution}\n" +
                           $"  Validation: {validation}";

            return debugInfo;
        }
    }

    /// <summary>
    /// 簡単な誤差検証テストを実行（即座に結果を取得）
    /// </summary>
    /// <param name="sampleCount">サンプル数（デフォルト100）</param>
    /// <returns>統計情報</returns>
    public static StatisticalInfo QuickValidationTest(int sampleCount = 100)
    {
        var samples = new System.Collections.Generic.List<double>();
        
        for (int i = 0; i < sampleCount; i++)
        {
            var info = GetValidationInfo();
            if (info.isValid)
            {
                samples.Add(info.driftSeconds);
            }
        }

        if (samples.Count > 0)
        {
            return CalculateStatistics(samples);
        }

        return new StatisticalInfo(0, 0, 0, 0, 0, 0);
    }
}

} // namespace Nobnak.MasterClock

