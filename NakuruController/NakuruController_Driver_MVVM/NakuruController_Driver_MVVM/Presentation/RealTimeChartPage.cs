using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WinUI;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using static UI;

public sealed partial class RealTimeChartPage : Page
{
    private static readonly CartesianChart _myCartesianChart = CreateMyCartesianChart();

    public RealTimeChartPage()
    {
        this.DataContext<IRealTimeChartViewModel>((page, vm) =>
            page.Background(Theme.Brushes.Background.Default)
                .Content(
                    MyGrid
                        .RowDefinitions("Auto,Auto,Auto,*")
                        .Children(
                            TextBlock("Logging Analog Value ")
                                .Grid(row: 0),
                            StackPanel(
                                ComboBox()
                                    .ItemsSource(() => vm.AvailablePorts)
                                    .SelectedItem(x => x.Binding(() => vm.SelectedPortName).TwoWay())
                                    .Width(200),
                                Button("Reflesh").Command(() => vm.UpdateAvailablePortsCommand))
                                .Orientation(Orientation.Horizontal)
                                .Grid(row: 1),
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
                                .Grid(row: 2),
                            _myCartesianChart
                                .Grid(row: 3)
                        )
                )
        );
    }

    private static CartesianChart CreateMyCartesianChart()
    {
        // 変数として一度チャートを作成
        var cartesianChart = new CartesianChart 
        {
            ZoomMode = LiveChartsCore.Measure.ZoomAndPanMode.X,
            
            XAxes = new[]
            {
                new Axis
                {
                    Name = "Time",
                    Labeler = value =>
                    {
                        double millisecond = value;
                        return $"{millisecond:F0}ms";
                    },
                    MinLimit = 0,
                    MaxLimit = 2000,  // 5秒分のウィンドウ
                    // 表示間隔を動的に調整
                    SeparatorsPaint = new SolidColorPaint(SKColors.LightGray.WithAlpha(50)),
                    SeparatorsAtCenter = false,
                    MinStep = 1,

                    AnimationsSpeed = TimeSpan.FromMilliseconds(0),
                }
            },

            YAxes = new[]
            {
                new Axis
                {
                    Name = "Analog Value",
                    MinLimit = 260,
                    MaxLimit = 510,
                    // Y軸も見やすく
                    ForceStepToMin = true,
                    MinStep = 20,

                    AnimationsSpeed = TimeSpan.FromMilliseconds(0),
                }
            },
        };

        // SetBinding を実行
        cartesianChart.SetBinding(
            CartesianChart.SeriesProperty,
            new Binding { Path = new PropertyPath(nameof(IRealTimeChartViewModel.Series)) }
        );

        // 作成した変数を返す
        return cartesianChart;
    }
}
