using UnityEngine;

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
    /// デバッグ情報を取得
    /// </summary>
    /// <returns>デバッグ情報文字列</returns>
    public static string GetDebugInfo() 
    {
        lock (_lock)
        {
            var info = System.Diagnostics.Stopwatch.Frequency;
            return $"ThreadSafeTime Debug Info:\n" +
                   $"  Initialized: {_initialized}\n" +
                   $"  Start Offset: {_startTimeOffset:F6}s\n" +
                   $"  Current Time: {realtimeSinceStartupAsDouble:F6}s\n" +
                   $"  Elapsed: {ElapsedSeconds:F6}s\n" +
                   $"  Stopwatch Frequency: {info:N0} Hz\n" +
                   $"  High Resolution: {System.Diagnostics.Stopwatch.IsHighResolution}";
        }
    }
}

