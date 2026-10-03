using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PwAssistant.Avalonia.Views;

/// <summary>Status kinds with fixed dot + text language (never color alone).</summary>
public enum StatusKind
{
    Online,
    Offline,
    Pending,
    Failed,
}

/// <summary>Dot + text status indicator bound to <see cref="Kind"/>.</summary>
public partial class StatusIndicator : UserControl
{
    public static readonly StyledProperty<StatusKind> KindProperty =
        AvaloniaProperty.Register<StatusIndicator, StatusKind>(nameof(Kind), StatusKind.Offline);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<StatusIndicator, string>(nameof(Label), string.Empty);

    static StatusIndicator()
    {
        KindProperty.Changed.AddClassHandler<StatusIndicator>((x, _) => x.Refresh());
        LabelProperty.Changed.AddClassHandler<StatusIndicator>(
            (x, e) => x.SetLabel(e.NewValue as string ?? string.Empty));
    }

    public StatusKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public StatusIndicator()
    {
        InitializeComponent();
        Refresh();
        LabelText.Text = Label;
    }

    private void SetLabel(string text)
    {
        if (LabelText is not null)
            LabelText.Text = text;
    }

    private void Refresh()
    {
        if (Dot is null || LabelText is null)
            return;
        IBrush foreground = FindBrush("Brush.MutedForeground", Brushes.Gray);
        Dot.Fill = foreground;
        Dot.Stroke = null;
        LabelText.Foreground = foreground;
        switch (Kind)
        {
            case StatusKind.Online:
                Dot.Fill = FindBrush("Brush.Success", Brushes.Green);
                break;
            case StatusKind.Offline:
                Dot.Fill = Brushes.Transparent;
                Dot.Stroke = foreground;
                Dot.StrokeThickness = 1.5;
                break;
            case StatusKind.Pending:
                Dot.Fill = FindBrush("Brush.Warning", Brushes.Orange);
                break;
            case StatusKind.Failed:
                Dot.Fill = FindBrush("Brush.Destructive", Brushes.Red);
                break;
        }
    }

    private IBrush FindBrush(string key, IBrush fallback) =>
        this.TryFindResource(key, out object? value) && value is IBrush brush
            ? brush
            : fallback;
}
