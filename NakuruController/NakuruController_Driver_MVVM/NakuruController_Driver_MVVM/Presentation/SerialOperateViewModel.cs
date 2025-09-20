using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Microsoft.UI.Dispatching;
using NakuruController_Driver_MVVM.Services;
using Reactive.Bindings.Extensions;

namespace NakuruController_Driver_MVVM.Presentation;

public interface ISerialOperateViewModel
{
    SerialConnectionState ConnectionState { get; }
    ObservableCollection<string> AvailablePorts { get; }
    string? SelectedPortName { get; set; }
    // コマンド
    IRelayCommand UpdateAvailablePortsCommand { get; }
    IAsyncRelayCommand SendStartCommand { get; }
    IAsyncRelayCommand SendStopCommand { get; }
    IAsyncRelayCommand ConnectCommand { get; }
    IAsyncRelayCommand DisconnectCommand { get; }
}

public partial class SerialOperateViewModel : ObservableObject, ISerialOperateViewModel, IDisposable
{
    private readonly ISerialDataService _serialService;
    private readonly DispatcherQueue _dispatcherQueue;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string? _selectedPortName = null;

    [ObservableProperty]
    private SerialConnectionState _connectionState = SerialConnectionState.Disconnected;

    public ObservableCollection<string> AvailablePorts { get; } = new();
    private CompositeDisposable _disposables = new();

    public SerialOperateViewModel(
        ISerialDataService serialService,
        IDispatcherQueueService dispatcherQueueService)
    {
        _serialService = serialService;

        UpdateAvailablePorts();
        _dispatcherQueue = dispatcherQueueService.DispatcherQueue;

        _serialService
            .ObserveProperty(x => x.ConnectionState)
            .Subscribe(OnConnectionChanged)
            .AddTo(_disposables);
    }

    private void OnConnectionChanged(SerialConnectionState state)
    {
        ConnectionState = state;

        // [NotifyCanExecuteChangedFor(nameof(xxxx))]を使いたかったが
        // UIスレッド以外から呼び出されるときに動作しないため。
        _dispatcherQueue.TryEnqueue(() =>
        {
            // CanExecuteの更新
            ConnectCommand.NotifyCanExecuteChanged();
            DisconnectCommand.NotifyCanExecuteChanged();
            SendStartCommand.NotifyCanExecuteChanged();
            SendStopCommand.NotifyCanExecuteChanged();
        });
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
    }
    private bool CanExecuteDisconnect() => SerialConnectionHelper.IsDisconnected(ConnectionState) == false;

    public void Dispose()
    {
        _disposables.Dispose();
    }
}
