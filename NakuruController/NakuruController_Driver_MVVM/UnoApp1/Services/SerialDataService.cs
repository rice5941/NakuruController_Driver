using System.IO.Ports;
using System.Text;
using System.Text.Json;

namespace UnoApp1.Services;

public interface ISerialDataService
{
    /// <summary>
    /// 接続状態を取得します。
    /// </summary>
    SerialConnectionState ConnectionState { get; }

    /// <summary>
    /// 使用可能なシリアルポートの一覧を取得します。
    /// </summary>
    /// <returns>使用可能なシリアルポート一覧.</returns>
    string[] GetAvailablePorts();

    /// <summary>
    /// 指定ポートに接続します。
    /// </summary>
    /// <param name="portName">ポート番号</param>
    Task ConnectAsync(string portName);

    /// <summary>
    /// 切断します。
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// アナログ値の送信を開始するコマンドを送信します。
    /// </summary>
    Task SendStartAnalogMessageAsync();

    /// <summary>
    /// アナログ値の送信を停止するコマンドを送信します。
    /// </summary>
    Task SendStopAnalogMessageAsync();

    /// <summary>
    /// アナログ値受診時のイベント.
    /// </summary>
    event Action<AnalogValueMessage> AnalogValueReceived;

    /// <summary>
    /// ステータス受診時のイベント.
    /// </summary>
    event Action<StatusMessage> StatusReceived;
}

public partial class SerialDataService : ObservableObject, ISerialDataService
{
    private SerialPort? _serialPort;
    private CancellationTokenSource? _heartbeatCts;
    private Task? _heartbeatTask;
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);

    public event Action<AnalogValueMessage>? AnalogValueReceived;

    public event Action<StatusMessage>? StatusReceived;

    [ObservableProperty]
    private SerialConnectionState _connectionState = SerialConnectionState.Disconnected;

    private SerialDataReceivedEventHandler? _dataReceivedHandler;
    public string[] GetAvailablePorts()
    {
        try
        {
            return SerialPort.GetPortNames();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public async Task ConnectAsync(string portName)
    {
        if (ConnectionState != SerialConnectionState.Disconnected)
        {
            await DisconnectAsync();
        }

        ArgumentNullException.ThrowIfNull(portName);

        _serialPort = new SerialPort
        {
            PortName = portName,
            BaudRate = 115200,
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
            Handshake = Handshake.None,
            ReadTimeout = 500,
            WriteTimeout = 500
        };

        // ラムダ式をフィールドに保存
        _dataReceivedHandler = (sender, e) => CreateDataReceivedHandler();
        _serialPort.DataReceived += _dataReceivedHandler;

        try
        {
            _serialPort.Open();

            ConnectionState = SerialConnectionState.Stop;
            StartHeartbeat();
        }
        catch (Exception ex)
        {

            System.Diagnostics.Debug.WriteLine(ex.Message); 
            CleanupSerialPort();
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        if (SerialConnectionHelper.IsDisconnected(ConnectionState))
        {
            return; // 既に切断済み
        }

        // アナログ値受信中の場合は停止コマンドを送信
        if (SerialConnectionHelper.IsReceivingAnalogValue(ConnectionState))
        {
            try
            {
                await SendStopAnalogMessageAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Stop command error: {ex.Message}");
            }
        }

        await StopHeartbeatAsync();
        CleanupSerialPort();
        ConnectionState = SerialConnectionState.Disconnected;
    }

    private void CleanupSerialPort()
    {
        if (_serialPort != null)
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }
            _serialPort.DataReceived -= _dataReceivedHandler;
            _serialPort.Dispose();
            _serialPort = null;
        }
    }

    private void StartHeartbeat()
    {
        _heartbeatCts = new CancellationTokenSource();
        _heartbeatTask = HeartbeatLoopAsync(_heartbeatCts.Token);
    }

    private async Task StopHeartbeatAsync()
    {
        if (_heartbeatCts != null)
        {
            _heartbeatCts.Cancel();

            if (_heartbeatTask != null)
            {
                try
                {
                    await _heartbeatTask;
                }
                catch (OperationCanceledException)
                {
                    // 正常なキャンセル
                }
            }

            _heartbeatCts.Dispose();
            _heartbeatCts = null;
            _heartbeatTask = null;
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        try
        {
            while (!cancellationToken.IsCancellationRequested && await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    await SendCommandAsync(SerialCommands.Heartbeat);
                    System.Diagnostics.Debug.WriteLine("[HEARTBEAT] Sent");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[HEARTBEAT] Error: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("[HEARTBEAT] Stopped");
        }
    }


    public async Task SendStartAnalogMessageAsync()
    {
        await SendCommandAsync(SerialCommands.StartAnalog);
        // 成功したら状態を更新
        if (SerialConnectionHelper.IsConnected(ConnectionState))
        {
            ConnectionState = SerialConnectionState.ReceivingAnalogValue;
        }
    }
    public async Task SendStopAnalogMessageAsync()
    {
        await SendCommandAsync(SerialCommands.StopAnalog);
        // 成功したら状態を更新
        if (SerialConnectionHelper.IsConnected(ConnectionState))
        {
            ConnectionState = SerialConnectionState.Stop;
        }
    }

    private async Task SendCommandAsync(string command)
    {
        await _sendSemaphore.WaitAsync();
        try
        {
            if (_serialPort?.IsOpen == true)
            {
                var data = Encoding.UTF8.GetBytes(command + "\n");
                await _serialPort.BaseStream.WriteAsync(data, 0, data.Length);
                await _serialPort.BaseStream.FlushAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SendCommand error: {ex.Message}");
            throw;
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }

    public Action CreateDataReceivedHandler()
    {
        StringBuilder receiveBuffer = new();

        void ProcessReceivedLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            try
            {
                // JSONメッセージの開始と終了を確認
                if (line.StartsWith("{") && line.EndsWith("}"))
                {
                    // アナログ値メッセージかどうかを判定
                    if (line.Contains("\"type\":\"analog_values\""))
                    {
                        try
                        {
                            var message = JsonSerializer.Deserialize<AnalogValueMessage>(line);
                            if (message != null)
                            {
                                if (message.Keys != null && message.Keys.Count > 0)
                                {
                                    AnalogValueReceived?.Invoke(message);
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("[SERIAL] Deserialized message is null");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[SERIAL] Parse exception: {ex.Message}");
                            throw;
                        }
                    }
                    // ステータスメッセージかどうかを判定.
                    else if (line.Contains("\"status\""))
                    {
                        System.Diagnostics.Debug.WriteLine($"[SERIAL] Parsing status message...: { line}");
                        var message = JsonSerializer.Deserialize<StatusMessage>(line);
                        if (message != null)
                        {
                            StatusReceived?.Invoke(message);
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[SERIAL] Unknown JSON message: {line}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[SERIAL] Non-JSON message: {line}");
                }
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SERIAL] JSON parse error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[SERIAL] Failed line: {line}");
            }
        }

        void ReceiveData()
        {
            try
            {
                if (_serialPort == null || !_serialPort.IsOpen) return;

                var data = _serialPort.ReadExisting();
                receiveBuffer.Append(data);

                var bufferString = receiveBuffer.ToString();
                var lines = bufferString.Split('\n');

                for (int i = 0; i < lines.Length - 1; i++)
                {
                    ProcessReceivedLine(lines[i].Trim());
                }

                receiveBuffer.Clear();
                if (lines.Length > 0)
                {
                    // 不完全な行をバッファ末尾に保持.
                    receiveBuffer.Append(lines[^1]);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        return ReceiveData;
    }

    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
        _sendSemaphore?.Dispose();
    }

}

public enum SerialConnectionState
{
    Disconnected,
    ReceivingAnalogValue,
    Stop,
}

public static class SerialConnectionHelper
{
    public static bool IsConnected(SerialConnectionState state)
    {
        return state switch
        {
            SerialConnectionState.ReceivingAnalogValue or 
            SerialConnectionState.Stop => true,
            _ => false,
        };
    }

    public static bool IsDisconnected(SerialConnectionState state) => state == SerialConnectionState.Disconnected;

    public static bool IsReceivingAnalogValue(SerialConnectionState state) => state == SerialConnectionState.ReceivingAnalogValue;

    public static bool IsStop(SerialConnectionState state) => state == SerialConnectionState.Stop;
}



