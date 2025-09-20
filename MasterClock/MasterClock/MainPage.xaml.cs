using MasterClock.Services;

namespace MasterClock
{
    public partial class MainPage : ContentPage
    {
        private readonly MasterClockService _clockService;
        private readonly OSCBroadcastService _oscBroadcast;

        public MainPage()
        {
            InitializeComponent();
            _clockService = new MasterClockService();
            _clockService.TickChanged += OnTickChanged;
            _oscBroadcast = new OSCBroadcastService();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _clockService.Start();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _clockService.Stop();
            
            // リソースをクリーンアップ
            if (_clockService != null)
            {
                _clockService.TickChanged -= OnTickChanged;
                _clockService.Dispose();
            }
            
            _oscBroadcast?.Dispose();
        }

        private void OnTickChanged(object? sender, TickEventArgs e)
        {
            // OSC /clock/tick ブロードキャスト送信
            _oscBroadcast.SendTickBroadcast(e.Tick);
            
            // UIを更新するためにメインスレッドで実行
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Tick.Text = e.Tick.ToString();
                Seconds.Text = e.Seconds.ToString("F2");
            });
        }
    }
}
