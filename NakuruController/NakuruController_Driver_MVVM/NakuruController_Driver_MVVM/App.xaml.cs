using Uno.Resizetizer;
using NakuruController_Driver_MVVM.Services;

namespace NakuruController_Driver_MVVM;
public partial class App : Application
{
    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();
    }

    protected Window? MainWindow { get; private set; }
    protected IHost? Host { get; private set; }

    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Load WinUI Resources
        Resources.Build(r => r.Merged(
            new XamlControlsResources()));
        // Load Uno.UI.Toolkit and Material Resources

        Resources.Build(r => r.Merged(
            new MaterialToolkitTheme(
                    new Styles.ColorPaletteOverride(),
                    new Styles.MaterialFontsOverride())));

        // DispatcherQueueServiceのインスタンスを作成
        var dispatcherQueueService = new DispatcherQueueService();

        var builder = this.CreateBuilder(args)
            // Add navigation support for toolkit controls such as TabBar and NavigationView
            .UseToolkitNavigation()
            .Configure(host => host
#if DEBUG
                // Switch to Development environment when running in DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .UseLogging(configure: (context, logBuilder) =>
                {
                    // Configure log levels for different categories of logging
                    logBuilder
                        .SetMinimumLevel(
                            context.HostingEnvironment.IsDevelopment() ?
                                LogLevel.Information :
                                LogLevel.Warning)
                        // Default filters for core Uno Platform namespaces
                        .CoreLogLevel(LogLevel.Warning);
                }, enableUnoLogging: true)
                .UseConfiguration(configure: configBuilder =>
                    configBuilder
                        .EmbeddedSource<App>()
                        .Section<AppConfig>()
                )
                // Enable localization (see appsettings.json for supported languages)
                .UseLocalization()
                .ConfigureServices((context, services) =>
                {
                    // DI注入コンテナにサービスを登録
                    services.AddTransient<ShellViewModel>();
                    services.AddSingleton<ISerialDataService, SerialDataService>();

                    // DispatcherQueueServiceをシングルトンとして登録
                    services.AddSingleton<IDispatcherQueueService>(dispatcherQueueService);
                    services.AddSingleton<ISerialOperateViewModel, SerialOperateViewModel>();
                    services.AddSingleton<IRealTimeChartViewModel, RealTimeChartViewModel>();

                })
                .UseNavigation(RegisterRoutes)
            );

        MainWindow = builder.Window;

        // ここでUIスレッドのDispatcherQueueが確実に利用可能
        dispatcherQueueService.Initialize(
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());

#if DEBUG
        MainWindow.UseStudio();
#endif
        Host = await builder.NavigateAsync<Shell>();
    }

    private static void RegisterRoutes(IViewRegistry views, IRouteRegistry routes)
    {
        views.Register(
            new ViewMap<Shell, ShellViewModel>(),
            new ViewMap<RealTimeChartPage, RealTimeChartViewModel>()
        );

        routes.Register(
            new RouteMap("", View: views.FindByViewModel<ShellViewModel>(),
                Nested:
                [
                    new ("RealTimeChart", View: views.FindByViewModel<RealTimeChartViewModel>(), IsDefault:true)
                ]
            )
        );
    }
}
