using static UI;

public sealed partial class SerialOperateView : UserControl
{
    public SerialOperateView()
    {
        this.DataContext<ISerialOperateViewModel>((userControl, vm) =>
            userControl.Background(Theme.Brushes.Background.Default)
                .Content(
                    MyGrid
                        .RowDefinitions("Auto,Auto")
                        .Children(
                            StackPanel(
                                ComboBox()
                                    .ItemsSource(() => vm.AvailablePorts)
                                    .SelectedItem(x => x.Binding(() => vm.SelectedPortName).TwoWay())
                                    .Width(200),
                                Button("Reflesh").Command(() => vm.UpdateAvailablePortsCommand))
                                .Orientation(Orientation.Horizontal)
                                .Grid(row: 0),
                            StackPanel(
                                StackPanel(
                                    Button("Connect").Command(() => vm.ConnectCommand),
                                    Button("Disconnect").Command(() => vm.DisconnectCommand))
                                .Orientation(Orientation.Horizontal),
                                StackPanel(
                                    Button("Start").Command(() => vm.SendStartCommand),
                                    Button("Stop").Command(() => vm.SendStopCommand))
                                .Orientation(Orientation.Horizontal)
                                .HorizontalAlignment(HorizontalAlignment.Left))
                                .Grid(row: 1)
                        )
                )
        );
    }
}
