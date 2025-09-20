using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO.Ports;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.UI.Dispatching;
using SkiaSharp;
using NakuruController_Driver_MVVM.Collections;
using NakuruController_Driver_MVVM.Services;

namespace NakuruController_Driver_MVVM.Presentation;
public interface IRealTimeChartViewModel
{
    // 読み取り専用プロパティ
    SerialConnectionState ConnectionState { get; }
    ISeries[] Series { get; }
    ObservableCollection<string> AvailablePorts { get; }
    string? SelectedPortName { get; set; }
    // コマンド
    IRelayCommand UpdateAvailablePortsCommand { get; }
    IAsyncRelayCommand SendStartCommand { get; }
    IAsyncRelayCommand SendStopCommand { get; }
    IAsyncRelayCommand ConnectCommand { get; }
    IAsyncRelayCommand DisconnectCommand { get; }
}

public partial class RealTimeChartViewModel : ObservableObject, IRealTimeChartViewModel
{
    private readonly Subject<AnalogValueMessage> _dataSubject = new();
    private IDisposable? _dataSubscription;
    private const int MaxDataPoints = 2000; // 最大データポイント数

    public ObservableCollection<string> AvailablePorts { get; } = new();

    [ObservableProperty]
    private ISeries[] _series = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string? _selectedPortName = null;

    // センサーデータを動的に管理
    private readonly List<BatchObservableCollection<double>> _sensorDataList = new();
    private readonly Dictionary<string, int> _sensorKeyToIndex = new();

    private ISerialDataService _serialService;

    private readonly DispatcherQueue _dispatcherQueue;
    // 色のプール
    private readonly SKColor[] _colorPool =
    {
        SKColors.Blue, SKColors.Red, SKColors.Green, SKColors.Orange,
        SKColors.Purple, SKColors.Brown, SKColors.Pink, SKColors.Cyan,
        SKColors.Magenta, SKColors.Yellow, SKColors.Lime, SKColors.Indigo
    };

    [ObservableProperty]
    private SerialConnectionState _connectionState= SerialConnectionState.Disconnected;

    public RealTimeChartViewModel(
        ISerialDataService serialService,
        IDispatcherQueueService dispatcherQueueService)
    {
        Series = Array.Empty<ISeries>();

        _serialService = serialService;
        _dispatcherQueue = dispatcherQueueService.DispatcherQueue;
        UpdateAvailablePorts();

        // 変更通知の購読
        if (_serialService is INotifyPropertyChanged notifyService)
        {
            notifyService.PropertyChanged += OnServicePropertyChanged;
        }

        // 16.7ms（60fps）間隔でバッファリング
        _dataSubscription = _dataSubject
            .Buffer(TimeSpan.FromMilliseconds(16.7))
            .Where(buffer => buffer.Count > 0)
            .Subscribe(ProcessBatchedData);
    }

    private void OnDataReceived(AnalogValueMessage value) => _dataSubject.OnNext(value);

    private void ProcessBatchedData(IList<AnalogValueMessage> messages)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            var dataByIndex = new Dictionary<int, List<double>>();
            bool isFirstData = _sensorDataList.Count == 0;

            // データを集約
            foreach (var message in messages)
            {
                foreach (var key in message.Keys)
                {
                    var sensorKey = key.Id.ToString();

                    if (_sensorKeyToIndex.ContainsKey(sensorKey) == false)
                    {
                        AddNewSensor(sensorKey);
                    }

                    var index = _sensorKeyToIndex[sensorKey];
                    if (dataByIndex.ContainsKey(index) == false)
                    {
                        dataByIndex[index] = new List<double>();
                    }

                    dataByIndex[index].Add(key.AnalogValue);
                }
            }

            // 初回データの場合は特別な処理
            if (isFirstData && dataByIndex.Count > 0)
            {
                foreach (var kvp in dataByIndex)
                {
                    var index = kvp.Key;
                    var values = kvp.Value;

                    // 初回は通知を抑制せずに追加
                    foreach (var value in values)
                    {
                        _sensorDataList[index].Add(value);
                    }
                }
            }
            else
            {
                // 2回目以降は元の処理
                foreach (var kvp in dataByIndex)
                {
                    var index = kvp.Key;
                    var values = kvp.Value;

                    using (_sensorDataList[index].SuspendNotifications())
                    {
                        // 古いデータを削除
                        var removeCount = Math.Max(0,
                            _sensorDataList[index].Count + values.Count - MaxDataPoints);

                        for (int i = 0; i < removeCount; i++)
                        {
                            _sensorDataList[index].RemoveAt(0);
                        }

                        // 新しいデータを追加
                        foreach (var value in values)
                        {
                            _sensorDataList[index].Add(value);
                        }
                    }
                }
            }
        });
    }

    private void AddNewSensor(string sensorKey)
    {
        var newIndex = _sensorDataList.Count;
        var newDataCollection = new BatchObservableCollection<double>();
        _sensorDataList.Add(newDataCollection);
        _sensorKeyToIndex[sensorKey] = newIndex;

        // 色を選択（プールから循環使用）
        var color = _colorPool[newIndex % _colorPool.Length];

        // 新しいSeriesを作成
        var newSeries = new LineSeries<double>
        {
            Values = newDataCollection,
            Fill = null,
            LineSmoothness = 0,
            GeometrySize = 0,
            Name = $"Key {sensorKey}",
            Stroke = new SolidColorPaint(color) { StrokeThickness = 2 },
            DataPadding = new LiveChartsCore.Drawing.LvcPoint(0, 0),

            // アニメーションを無効化
            AnimationsSpeed = TimeSpan.Zero,

        };

        // Seriesを更新（配列を再作成）
        var seriesList = Series.ToList();
        seriesList.Add(newSeries);
        Series = seriesList.ToArray();
    }

    private void OnServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ISerialDataService.ConnectionState))
        {
            ConnectionState = _serialService.ConnectionState;

            _dispatcherQueue.TryEnqueue(() =>
            {
                // CanExecuteの更新
                ConnectCommand.NotifyCanExecuteChanged();
                DisconnectCommand.NotifyCanExecuteChanged();
                SendStartCommand.NotifyCanExecuteChanged();
                SendStopCommand.NotifyCanExecuteChanged();
            });
        }
    }

    [RelayCommand]
    private void UpdateAvailablePorts()
    {
        _serialService.GetAvailablePorts();
        var newPorts = _serialService.GetAvailablePorts();

        // 差分更新（追加と削除のみ）
        // 削除
        for (int i = AvailablePorts.Count - 1; i >= 0; i--)
        {
            if (newPorts.Contains(AvailablePorts[i]) == false)
            {
                AvailablePorts.RemoveAt(i);
            }
        }

        // 追加
        foreach (var port in newPorts)
        {
            if (AvailablePorts.Contains(port) == false)
            {
                AvailablePorts.Add(port);
            }
        }
    }

    // Disposeを忘れずに
    public void Dispose()
    {
        _dataSubscription?.Dispose();
        _dataSubject?.Dispose();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteSendStart))]
    private async Task SendStartAsync() => await _serialService.SendStartAnalogMessageAsync();
    private bool CanExecuteSendStart() => SerialConnectionHelper.IsStop(ConnectionState) && 
                                          SerialConnectionHelper.IsConnected(ConnectionState);

    [RelayCommand(CanExecute = nameof(CanExecuteSendStop))]
    private async Task SendStopAsync() => await _serialService.SendStopAnalogMessageAsync();
    private bool CanExecuteSendStop() => SerialConnectionHelper.IsReceivingAnalogValue(ConnectionState) &&
                                         SerialConnectionHelper.IsConnected(ConnectionState);

    [RelayCommand(CanExecute = nameof(CanExecuteConnect))]
    private async Task ConnectAsync()
    {
        // イベントを購読してデータをコレクションに追加
        _serialService.AnalogValueReceived += OnDataReceived;
        if (SelectedPortName != null)
        {
            await _serialService.ConnectAsync(SelectedPortName);
        }
        else
        {
            // ポートが見つからなかった場合の処理をここに書く
            // 例: ユーザーにエラーメッセージを表示する、ログを出力するなど
            System.Diagnostics.Debug.WriteLine("利用可能なシリアルポートが見つかりませんでした。");
        }
    }

    private bool CanExecuteConnect() => SelectedPortName != null &&
                                        SerialConnectionHelper.IsConnected(ConnectionState) == false;

    [RelayCommand(CanExecute = nameof(CanExecuteDisconnect))]
    private async Task DisconnectAsync()
    {
        await _serialService.DisconnectAsync();
        _serialService.AnalogValueReceived -= OnDataReceived;
    }
    private bool CanExecuteDisconnect() => SerialConnectionHelper.IsDisconnected(ConnectionState) == false;
}
