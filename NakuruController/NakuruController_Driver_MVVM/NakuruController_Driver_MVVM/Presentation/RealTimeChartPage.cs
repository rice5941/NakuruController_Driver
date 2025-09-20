using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WinUI;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using static UI;

public sealed partial class RealTimeChartPage : Page
{
    private const int RealTimeMaxDataPoints = 2000; // 最大データポイント数

    private readonly CartesianChart _myCartesianChart;
    private readonly SerialOperateView _serialOperateView;

    public RealTimeChartPage()
    {
        _myCartesianChart = CreateMyCartesianChart(RealTimeMaxDataPoints);
        _serialOperateView = CreateSerialOperateView();

        this.DataContext<IRealTimeChartViewModel>((page, vm) =>
        {
            page.Background(Theme.Brushes.Background.Default)
                .Content(
                    MyGrid
                        .RowDefinitions("Auto,Auto,*")
                        .Children(
                            TextBlock("Logging Analog Value ")
                                .Grid(row: 0),
                            _serialOperateView
                                .Grid(row: 1),
                            _myCartesianChart
                                .Grid(row: 2)
                    )
                );
        });
    }

    private static SerialOperateView CreateSerialOperateView()
    {
        var serialOperateView = new SerialOperateView();

        // DataContextのバインディングを設定
        var binding = new Binding
        {
            Path = new PropertyPath(nameof(IRealTimeChartViewModel.SerialOperateViewModel)),
            Mode = BindingMode.OneWay
        };
        serialOperateView.SetBinding(FrameworkElement.DataContextProperty, binding);
        return serialOperateView;
    }

    private static CartesianChart CreateMyCartesianChart(int maxLimit)
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
                    MaxLimit = maxLimit,// maxLimit ms分のデータを表示
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
