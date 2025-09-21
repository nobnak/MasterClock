using Rug.Osc;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MasterClock.Services
{
    public class OSCBroadcastService : IDisposable
    {
        private readonly OscSender _oscSender;
        private readonly string _oscAddress;
        private bool _disposed = false;
        
        // デフォルトのブロードキャストポート
        private const int DEFAULT_OSC_PORT = 8000;
        
        // デフォルトのOSCアドレス
        private const string DEFAULT_OSC_ADDRESS = "/clock/tick";
        
        public OSCBroadcastService(int port = DEFAULT_OSC_PORT, string oscAddress = DEFAULT_OSC_ADDRESS)
        {
            // OSCアドレスを設定
            _oscAddress = oscAddress;

            // OSC送信者を初期化（ブロードキャスト用）
            var remote = IPAddress.Broadcast;
            var local = (remote.AddressFamily == AddressFamily.InterNetworkV6) ? IPAddress.IPv6Any : IPAddress.Any;
            _oscSender = new OscSender(local, 0, remote, port, 8, 600, 2048);
            _oscSender.Connect();
        }
        
        /// <summary>
        /// 指定されたOSCアドレスにtick値をint形式でブロードキャスト送信
        /// </summary>
        /// <param name="tick">送信するtick値</param>
        public void SendTickBroadcast(uint tick)
        {
            if (_disposed) return;
            
            try
            {
                // OSCメッセージを作成: 設定されたアドレスにint形式のtick値を付加
                var message = new OscMessage(_oscAddress, (int)tick);
                
                // ブロードキャスト送信
                _oscSender.Send(message);
            }
            catch (Exception ex)
            {
                // ログ出力（実際のアプリではloggingフレームワークを使用）
                System.Diagnostics.Debug.WriteLine($"OSC broadcast failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 指定されたアドレスとポートに直接送信
        /// </summary>
        /// <param name="tick">送信するtick値</param>
        /// <param name="targetAddress">送信先IPアドレス</param>
        /// <param name="targetPort">送信先ポート</param>
        public void SendTickToTarget(uint tick, IPAddress targetAddress, int targetPort)
        {
            if (_disposed) return;
            
            try
            {
                using var targetSender = new OscSender(targetAddress, targetPort);
                
                var message = new OscMessage(_oscAddress, (int)tick);
                targetSender.Send(message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OSC direct send failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 利用可能なネットワークインターフェースを取得（デバッグ用）
        /// </summary>
        /// <returns>ネットワークインターフェース情報のリスト</returns>
        public static List<string> GetNetworkInterfaces()
        {
            var interfaces = new List<string>();
            
            try
            {
                foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (networkInterface.OperationalStatus == OperationalStatus.Up)
                    {
                    foreach (var address in networkInterface.GetIPProperties().UnicastAddresses)
                    {
                        if (address.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && 
                            !IPAddress.IsLoopback(address.Address))
                        {
                            interfaces.Add($"{networkInterface.Name}: {address.Address}");
                        }
                    }
                    }
                }
            }
            catch (Exception ex)
            {
                interfaces.Add($"Error retrieving network interfaces: {ex.Message}");
            }
            
            return interfaces;
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _oscSender?.Dispose();
                _disposed = true;
            }
        }
    }
}
