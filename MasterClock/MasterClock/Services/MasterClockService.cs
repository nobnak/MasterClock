using System.Timers;
using System.Diagnostics;

namespace MasterClock.Services
{
    public class MasterClockService : IDisposable
    {
        private readonly System.Timers.Timer _timer;
        private readonly Stopwatch _stopwatch;
        private readonly object _lockObject = new object();
        private uint _lastTick = 0;
        private bool _isRunning = false;
        private uint _startTick = 0;  // 開始時のtick値
        
        // チェック間隔（短い間隔でtick変更をチェック）
        private const double TIMER_INTERVAL = 5.0; // 5ms
        
        // 1秒間に30tick
        private const int TICKS_PER_SECOND = 30;

        public event EventHandler<TickEventArgs>? TickChanged;
        
        public uint CurrentTick 
        { 
            get 
            { 
                lock (_lockObject) 
                { 
                    if (!_isRunning) return _lastTick;
                    
                    // Stopwatchの経過時間からtickを計算（開始時のtickを加算）
                    double elapsedSeconds = _stopwatch.Elapsed.TotalSeconds;
                    return _startTick + (uint)(elapsedSeconds * TICKS_PER_SECOND);
                } 
            } 
        }
        
        public double CurrentSeconds 
        { 
            get 
            { 
                lock (_lockObject) 
                { 
                    if (!_isRunning) return _lastTick / (double)TICKS_PER_SECOND;
                    
                    // Stopwatchの経過時間を直接返す（開始時の秒数を加算）
                    return (_startTick / (double)TICKS_PER_SECOND) + _stopwatch.Elapsed.TotalSeconds;
                } 
            } 
        }
        
        public bool IsRunning 
        { 
            get 
            { 
                lock (_lockObject) 
                { 
                    return _isRunning; 
                } 
            } 
        }

        public MasterClockService()
        {
            _stopwatch = new Stopwatch();
            _timer = new System.Timers.Timer(TIMER_INTERVAL);
            _timer.Elapsed += OnTimerElapsed;
            _timer.AutoReset = true;
        }

        public void Start()
        {
            lock (_lockObject)
            {
                if (!_isRunning)
                {
                    _isRunning = true;
                    _stopwatch.Start();
                    _timer.Start();
                }
            }
        }

        public void Stop()
        {
            lock (_lockObject)
            {
                if (_isRunning)
                {
                    _isRunning = false;
                    _stopwatch.Stop();
                    _timer.Stop();
                    
                    // 停止時の現在のtickを記録
                    _lastTick = CurrentTick;
                }
            }
        }

        public void Reset()
        {
            ResetFromTick(0);
        }

        public void ResetFromTick(uint startTick)
        {
            lock (_lockObject)
            {
                bool wasRunning = _isRunning;
                if (wasRunning)
                {
                    _timer.Stop();
                    _stopwatch.Stop();
                }
                
                _stopwatch.Reset();
                _startTick = startTick;
                _lastTick = startTick;
                
                if (wasRunning)
                {
                    _stopwatch.Start();
                    _timer.Start();
                }
            }
        }

        public void ResetFromSeconds(double startSeconds)
        {
            uint startTick = (uint)(startSeconds * TICKS_PER_SECOND);
            ResetFromTick(startTick);
        }

        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            uint currentTick;
            double currentSeconds;
            bool tickChanged = false;
            
            lock (_lockObject)
            {
                if (!_isRunning) return;
                
                // Stopwatchの経過時間からtickを計算（開始時のtickを加算）
                double elapsedSeconds = _stopwatch.Elapsed.TotalSeconds;
                currentTick = _startTick + (uint)(elapsedSeconds * TICKS_PER_SECOND);
                currentSeconds = (_startTick / (double)TICKS_PER_SECOND) + elapsedSeconds;
                
                // tickが変更された場合のみイベントを発生
                if (currentTick != _lastTick)
                {
                    _lastTick = currentTick;
                    tickChanged = true;
                }
            }

            // lockの外でイベントを発生（デッドロック回避）
            if (tickChanged)
            {
                TickChanged?.Invoke(this, new TickEventArgs(currentTick, currentSeconds));
            }
        }

        public void Dispose()
        {
            Stop();
            _timer?.Dispose();
        }
    }

    public class TickEventArgs : EventArgs
    {
        public uint Tick { get; }
        public double Seconds { get; }

        public TickEventArgs(uint tick, double seconds)
        {
            Tick = tick;
            Seconds = seconds;
        }
    }
}
