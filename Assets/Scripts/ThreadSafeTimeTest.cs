using UnityEngine;
using System.Threading.Tasks;

/// <summary>
/// ThreadSafeTimeクラスのテストと使用例を示すコンポーネント
/// </summary>
public class ThreadSafeTimeTest : MonoBehaviour
{
    [Header("Test Settings")]
    public bool runMultiThreadTest = true;
    public bool showContinuousInfo = true;
    public int backgroundTaskCount = 3;

    private void Start()
    {
        if (runMultiThreadTest)
        {
            StartMultiThreadTest();
        }
    }

    private void Update()
    {
        if (showContinuousInfo && Time.frameCount % 60 == 0) // 1秒ごとに表示
        {
            ShowTimeComparison();
        }
    }

    /// <summary>
    /// Unity TimeとThreadSafeTimeの比較表示
    /// </summary>
    private void ShowTimeComparison()
    {
        double unityTime = Time.realtimeSinceStartupAsDouble;
        double threadSafeTime = ThreadSafeTime.realtimeSinceStartupAsDouble;
        double drift = ThreadSafeTime.GetDriftFromUnityTime();

        Debug.Log($"[ThreadSafeTimeTest] Unity: {unityTime:F6}s, ThreadSafe: {threadSafeTime:F6}s, Drift: {drift * 1000:F3}ms");
    }

    /// <summary>
    /// マルチスレッドでのThreadSafeTime使用テスト
    /// </summary>
    private async void StartMultiThreadTest()
    {
        Debug.Log("[ThreadSafeTimeTest] Starting multi-thread test...");

        // 複数のバックグラウンドタスクでThreadSafeTimeにアクセス
        Task[] tasks = new Task[backgroundTaskCount];
        
        for (int i = 0; i < backgroundTaskCount; i++)
        {
            int taskId = i;
            tasks[i] = Task.Run(async () =>
            {
                for (int j = 0; j < 10; j++)
                {
                    try
                    {
                        double time = ThreadSafeTime.realtimeSinceStartupAsDouble;
                        double timeMs = ThreadSafeTime.realtimeSinceStartupAsMilliseconds;
                        
                        // メインスレッド以外からのアクセスをログに記録
                        Debug.Log($"[Thread-{taskId}] Iteration {j}: {time:F6}s ({timeMs:F3}ms)");
                        
                        await Task.Delay(100); // 100ms待機
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[Thread-{taskId}] Error accessing ThreadSafeTime: {ex.Message}");
                    }
                }
            });
        }

        // すべてのタスクの完了を待つ
        try
        {
            await Task.WhenAll(tasks);
            Debug.Log("[ThreadSafeTimeTest] Multi-thread test completed successfully!");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ThreadSafeTimeTest] Multi-thread test failed: {ex.Message}");
        }
    }

    /// <summary>
    /// ThreadSafeTimeの詳細情報をログ出力
    /// </summary>
    [ContextMenu("Log ThreadSafeTime Debug Info")]
    public void LogDebugInfo()
    {
        Debug.Log(ThreadSafeTime.GetDebugInfo());
    }

    /// <summary>
    /// Unity TimeとThreadSafeTimeを再同期
    /// </summary>
    [ContextMenu("Synchronize with Unity Time")]
    public void SynchronizeTime()
    {
        ThreadSafeTime.SynchronizeWithUnityTime();
        ShowTimeComparison();
    }

    /// <summary>
    /// パフォーマンステスト
    /// </summary>
    [ContextMenu("Performance Test")]
    public void PerformanceTest()
    {
        const int iterations = 100000;
        
        // Unity Time access test (メインスレッドのみ)
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            var _ = Time.realtimeSinceStartupAsDouble;
        }
        stopwatch.Stop();
        double unityTimeMs = stopwatch.Elapsed.TotalMilliseconds;

        // ThreadSafeTime access test
        stopwatch.Restart();
        for (int i = 0; i < iterations; i++)
        {
            var _ = ThreadSafeTime.realtimeSinceStartupAsDouble;
        }
        stopwatch.Stop();
        double threadSafeTimeMs = stopwatch.Elapsed.TotalMilliseconds;

        Debug.Log($"[ThreadSafeTimeTest] Performance Test ({iterations:N0} iterations):\n" +
                  $"  Unity Time: {unityTimeMs:F2}ms ({unityTimeMs / iterations * 1000000:F2}ns per call)\n" +
                  $"  ThreadSafe Time: {threadSafeTimeMs:F2}ms ({threadSafeTimeMs / iterations * 1000000:F2}ns per call)\n" +
                  $"  Ratio: {threadSafeTimeMs / unityTimeMs:F2}x");
    }
}

