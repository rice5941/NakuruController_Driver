using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO.Ports;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.UI.Dispatching;
using NakuruController_Driver_MVVM.Collections;
using NakuruController_Driver_MVVM.Services;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using SkiaSharp;

namespace NakuruController_Driver_MVVM.Presentation;
public interface IRealTimeChartViewModel
{
    ISeries[] Series { get; }

    ISerialOperateViewModel SerialOperateViewModel { get; }
}

public partial class RealTimeChartViewModel : ObservableObject, IRealTimeChartViewModel, IDisposable
{
    private readonly Subject<AnalogValueMessage> _dataSubject = new();
    private IDisposable? _dataSubscription;
    private const int MaxDataPoints = 2000; // 最大データポイント数

    [ObservableProperty]
    private ISeries[] _series = [];

    private readonly ISerialOperateViewModel _serialOperateViewModel;

    // センサーデータを動的に管理
    private readonly List<BatchObservableCollection<double>> _sensorDataList = new();
    private readonly Dictionary<string, int> _sensorKeyToIndex = new();

    private ISerialDataService _serialService;

    private readonly DispatcherQueue _dispatcherQueue;

    private CompositeDisposable _disposables = new();

    // 色のプール
    private readonly SKColor[] _colorPool =
    {
        SKColors.Blue, SKColors.Red, SKColors.Green, SKColors.Orange,
        SKColors.Purple, SKColors.Brown, SKColors.Pink, SKColors.Cyan,
        SKColors.Magenta, SKColors.Yellow, SKColors.Lime, SKColors.Indigo
    };

    public RealTimeChartViewModel(
        ISerialDataService serialService,
        IDispatcherQueueService dispatcherQueueService,
        ISerialOperateViewModel serialOperateViewModel)
    {
        Series = Array.Empty<ISeries>();

        _serialService = serialService;
        _dispatcherQueue = dispatcherQueueService.DispatcherQueue;

        _serialOperateViewModel = serialOperateViewModel;

        // 16.7ms（60fps）間隔でバッファリング
        _dataSubscription = _dataSubject
            .Buffer(TimeSpan.FromMilliseconds(16.7))
            .Where(buffer => buffer.Count > 0)
            .Subscribe(ProcessBatchedData)
            .AddTo(_disposables);

        _serialService
            .ObserveProperty(x => x.ConnectionState)
            .Select(state =>
            {
                if (SerialConnectionHelper.IsReceivingAnalogValue(state))
                {
                    return Observable.FromEvent<AnalogValueMessage>(
                        h => _serialService.AnalogValueReceived += h,
                        h => _serialService.AnalogValueReceived -= h);
                }
                return Observable.Empty<AnalogValueMessage>();
            })
            .Switch() // 自動的に前の購読を解除
            .Subscribe(OnDataReceived)
            .AddTo(_disposables);
    }

    public ISerialOperateViewModel SerialOperateViewModel => _serialOperateViewModel;

    private void OnDataReceived(AnalogValueMessage value) => _dataSubject.OnNext(value);

    private void ProcessBatchedData(IList<AnalogValueMessage> messages)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            var dataByIndex = new Dictionary<int, List<double>>();

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

    public void Dispose()
    {
        _dataSubscription?.Dispose();
        _dataSubject?.Dispose();

        _disposables?.Dispose(); 
    }
}
