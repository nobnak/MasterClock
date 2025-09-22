using UnityEngine;

/// <summary>
/// MasterClockの同期時刻を使用して時計の針（時、分、秒、ミリ秒）を回転させるコントローラー
/// </summary>
public class ClockHandsController : MonoBehaviour {
    
    [Header("Clock Hands")]
    [SerializeField] private Transform hourHand;
    [SerializeField] private Transform minuteHand;
    [SerializeField] private Transform secondHand;
    [SerializeField] private Transform milliHand;
    
    [Header("Clock Settings")]
    [SerializeField] private MasterClockQuery masterClock;
    [SerializeField] private bool useLocalRotation = true;
    [SerializeField] private bool invertRotation = true; // 時計回り（負の角度）
    
    [Header("Rotation Axes")]
    [SerializeField] private Vector3 rotationAxis = Vector3.forward; // Z軸回転がデフォルト
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    
    // 回転角度の定数（1単位あたりの度数）
    private const float DEGREES_PER_HOUR = 30.0f;     // 12時間で360度
    private const float DEGREES_PER_MINUTE = 6.0f;    // 60分で360度
    private const float DEGREES_PER_SECOND = 6.0f;    // 60秒で360度
    private const float DEGREES_PER_MILLI = 0.36f;    // 1000ミリ秒で360度
    
    private void Start() {
        // MasterClockが指定されていない場合は自動検索
        if (masterClock == null) {
            masterClock = FindAnyObjectByType<MasterClockQuery>();
            if (masterClock == null) {
                Debug.LogError("[ClockHandsController] MasterClockQuery not found!");
                return;
            }
        }
        
        if (showDebugInfo) {
            Debug.Log($"[ClockHandsController] Initialized with MasterClock: {masterClock.name}");
        }
    }
    
    private void Update() {
        if (masterClock == null) return;
        
        // MasterClockから同期された時刻を取得
        double synchronizedTime = masterClock.GetSynchronizedTime();
        
        // 時刻を時、分、秒、ミリ秒に分解
        var timeComponents = ExtractTimeComponents(synchronizedTime);
        
        // 各針の回転角度を計算
        float hourAngle = CalculateHourAngle(timeComponents.hours, timeComponents.minutes);
        float minuteAngle = CalculateMinuteAngle(timeComponents.minutes, timeComponents.seconds);
        float secondAngle = CalculateSecondAngle(timeComponents.seconds, timeComponents.milliseconds);
        float milliAngle = CalculateMilliAngle(timeComponents.milliseconds);
        
        // 針を回転させる
        RotateHand(hourHand, hourAngle);
        RotateHand(minuteHand, minuteAngle);
        RotateHand(secondHand, secondAngle);
        RotateHand(milliHand, milliAngle);
        
        // デバッグ情報表示
        if (showDebugInfo) {
            Debug.Log($"[ClockHandsController] Time: {timeComponents.hours:D2}:{timeComponents.minutes:D2}:{timeComponents.seconds:D2}.{timeComponents.milliseconds:D3}, " +
                     $"Angles: H={hourAngle:F1}°, M={minuteAngle:F1}°, S={secondAngle:F1}°, Ms={milliAngle:F1}°");
        }
    }
    
    /// <summary>
    /// 同期時刻から時、分、秒、ミリ秒を抽出
    /// </summary>
    /// <param name="synchronizedTime">同期された時刻（秒）</param>
    /// <returns>時刻コンポーネント</returns>
    private (int hours, int minutes, int seconds, int milliseconds) ExtractTimeComponents(double synchronizedTime) {
        // 負の時間を扱えるように調整
        double adjustedTime = synchronizedTime % 86400.0; // 24時間でループ
        if (adjustedTime < 0) adjustedTime += 86400.0;
        
        int totalSeconds = (int)adjustedTime;
        int hours = (totalSeconds / 3600) % 12; // 12時間表示
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;
        int milliseconds = (int)((adjustedTime - totalSeconds) * 1000.0);
        
        return (hours, minutes, seconds, milliseconds);
    }
    
    /// <summary>
    /// 時針の角度を計算（分も考慮した滑らかな動き）
    /// </summary>
    private float CalculateHourAngle(int hours, int minutes) {
        float angle = hours * DEGREES_PER_HOUR + (minutes / 60.0f) * DEGREES_PER_HOUR;
        return invertRotation ? -angle : angle;
    }
    
    /// <summary>
    /// 分針の角度を計算（秒も考慮した滑らかな動き）
    /// </summary>
    private float CalculateMinuteAngle(int minutes, int seconds) {
        float angle = minutes * DEGREES_PER_MINUTE + (seconds / 60.0f) * DEGREES_PER_MINUTE;
        return invertRotation ? -angle : angle;
    }
    
    /// <summary>
    /// 秒針の角度を計算（ミリ秒も考慮した滑らかな動き）
    /// </summary>
    private float CalculateSecondAngle(int seconds, int milliseconds) {
        float angle = seconds * DEGREES_PER_SECOND + (milliseconds / 1000.0f) * DEGREES_PER_SECOND;
        return invertRotation ? -angle : angle;
    }
    
    /// <summary>
    /// ミリ秒針の角度を計算
    /// </summary>
    private float CalculateMilliAngle(int milliseconds) {
        float angle = milliseconds * DEGREES_PER_MILLI;
        return invertRotation ? -angle : angle;
    }
    
    /// <summary>
    /// 指定されたTransformを回転させる
    /// </summary>
    /// <param name="hand">回転させるTransform</param>
    /// <param name="angle">回転角度（度）</param>
    private void RotateHand(Transform hand, float angle) {
        if (hand == null) return;
        
        Quaternion rotation = Quaternion.AngleAxis(angle, rotationAxis);
        
        if (useLocalRotation) {
            hand.localRotation = rotation;
        } else {
            hand.rotation = rotation;
        }
    }
    
    /// <summary>
    /// 現在の同期時刻を取得（デバッグ用）
    /// </summary>
    public double GetCurrentSynchronizedTime() => masterClock?.GetSynchronizedTime() ?? 0.0;
    
    /// <summary>
    /// 各針の現在の角度を取得（デバッグ用）
    /// </summary>
    public (float hour, float minute, float second, float milli) GetCurrentAngles() {
        if (masterClock == null) return (0, 0, 0, 0);
        
        double synchronizedTime = masterClock.GetSynchronizedTime();
        var timeComponents = ExtractTimeComponents(synchronizedTime);
        
        return (
            CalculateHourAngle(timeComponents.hours, timeComponents.minutes),
            CalculateMinuteAngle(timeComponents.minutes, timeComponents.seconds),
            CalculateSecondAngle(timeComponents.seconds, timeComponents.milliseconds),
            CalculateMilliAngle(timeComponents.milliseconds)
        );
    }
    
    /// <summary>
    /// MasterClockを動的に設定
    /// </summary>
    /// <param name="newMasterClock">新しいMasterClock</param>
    public void SetMasterClock(MasterClockQuery newMasterClock) {
        masterClock = newMasterClock;
        
        if (showDebugInfo) {
            Debug.Log($"[ClockHandsController] MasterClock changed to: {masterClock?.name ?? "null"}");
        }
    }
}
