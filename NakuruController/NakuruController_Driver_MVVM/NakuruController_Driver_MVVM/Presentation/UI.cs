using System.Collections.ObjectModel;

public static partial class UI
{
    public static Grid MyGrid => new Grid();

    // 他のよく使うコントロールも同様に定義しておくと便利
    public static StackPanel StackPanel(params UIElement[] children) => new StackPanel().Children(children);

    public static TextBlock TextBlock(string text) => new TextBlock().Text(text);

    public static Button Button(string content) =>
        new Button()
            .Content(content)
            .Style(StaticResource.Get<Style>("DefaultButtonStyle"))  // WinUIデフォルト
            .CornerRadius(20)
            .Padding(new Thickness(22, 10))
            .Margin(new Thickness(10))
            .Foreground(new SolidColorBrush(Color.FromArgb(255, 224, 255, 255)))  // Light Cyan
            .Resources(config => config
                // 通常時 - ベースカラー
                .Add("ButtonBackground", new SolidColorBrush(Color.FromArgb(255, 0, 151, 167)))
                // マウスオーバー時 - 少し暗く
                .Add("ButtonBackgroundPointerOver", new SolidColorBrush(Color.FromArgb(255, 0, 121, 134)))
                .Add("ButtonForegroundPointerOver", new SolidColorBrush(Colors.White))
                // 押下時 - 中間の暗さ（押している感）
                .Add("ButtonBackgroundPressed", new SolidColorBrush(Color.FromArgb(255, 0, 100, 111)))
                // 無効時 - 最も暗く、半透明
                .Add("ButtonBackgroundDisabled", new SolidColorBrush(Color.FromArgb(200, 0, 60, 67)))
                .Add("ButtonForegroundDisabled", new SolidColorBrush(Color.FromArgb(128, 180, 180, 180)))
            );
    public static ComboBox ComboBox() =>
        new ComboBox()
            .Margin(new Thickness(10))
            .HorizontalAlignment(HorizontalAlignment.Left);
}
