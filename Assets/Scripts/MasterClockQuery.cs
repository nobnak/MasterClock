using UnityEngine;

/// <summary>
/// クロックの種類を指定するenum
/// </summary>
public enum ClockType {
    Standalone,
    Networked
}

/// <summary>
/// MasterClockとMasterClockStandaloneの参照を保持し、
/// enum値によってIMasterClockQueryが返す値の参照先を切り替えるクエリ専用クラス
/// </summary>
public class MasterClockQuery : MonoBehaviour, IMasterClockQuery {
    
    [SerializeField] private ClockType clockType = ClockType.Standalone;
    [SerializeField] private MasterClockStandalone standaloneClockReference;
    [SerializeField] private MasterClock networkedClockReference;
    
    /// <summary>
    /// 現在選択されているクロックのクエリインターフェース参照を取得
    /// </summary>
    private IMasterClockQuery CurrentClockQuery => clockType == ClockType.Standalone ? 
        (IMasterClockQuery)standaloneClockReference : 
        (IMasterClockQuery)networkedClockReference;
        
    /// <summary>
    /// 現在のクロックタイプを取得・設定
    /// </summary>
    public ClockType CurrentType {
        get => clockType;
        set => clockType = value;
    }

    #region IMasterClockQuery Properties
    /// <summary>
    /// 設定への読み取り専用アクセスを提供
    /// </summary>
    public MasterClockCore.Config Settings => CurrentClockQuery?.Settings;

    /// <summary>
    /// 現在選択されているクロックの名前を取得
    /// </summary>
    public new string name => CurrentClockQuery?.name ?? "No Clock Selected";
    #endregion

    #region IMasterClockQuery Methods
    /// <summary>
    /// 同期された時刻を取得
    /// </summary>
    /// <returns>同期された時刻</returns>
    public double GetSynchronizedTime() {
        return CurrentClockQuery?.GetSynchronizedTime() ?? 0.0;
    }

    /// <summary>
    /// 同期された時刻を取得（リモート用の互換メソッド）
    /// </summary>
    /// <returns>同期された時刻</returns>
    public double GetRemoteSynchronizedTime() {
        return CurrentClockQuery?.GetRemoteSynchronizedTime() ?? 0.0;
    }

    /// <summary>
    /// 現在のオフセット値を取得
    /// </summary>
    /// <returns>推定されたオフセット値</returns>
    public double GetCurrentOffset() {
        return CurrentClockQuery?.GetCurrentOffset() ?? 0.0;
    }

    /// <summary>
    /// EMAの統計情報を取得
    /// </summary>
    /// <returns>EMAの値、分散、標準偏差</returns>
    public (double value, double variance, double standardDeviation) GetEmaStatistics() {
        return CurrentClockQuery?.GetEmaStatistics() ?? (0.0, 0.0, 0.0);
    }

    /// <summary>
    /// 最後に入力されたtick値を取得
    /// </summary>
    /// <returns>最後に入力されたtick値</returns>
    public uint GetLastInputTick() {
        return CurrentClockQuery?.GetLastInputTick() ?? 0;
    }

    /// <summary>
    /// 現在の時刻を取得
    /// </summary>
    /// <returns>現在の時刻</returns>
    public double GetCurrentTime() {
        return CurrentClockQuery?.GetCurrentTime() ?? 0.0;
    }

    /// <summary>
    /// 最後に計算されたtick時刻を取得
    /// </summary>
    /// <returns>tick時刻</returns>
    public double GetCurrentTickTime() {
        return CurrentClockQuery?.GetCurrentTickTime() ?? 0.0;
    }
    #endregion
}