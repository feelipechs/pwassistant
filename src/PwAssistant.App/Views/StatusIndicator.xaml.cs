using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PwAssistant.App.Views;

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
    public static readonly DependencyProperty KindProperty =
        DependencyProperty.Register(
            nameof(Kind), typeof(StatusKind), typeof(StatusIndicator),
            new PropertyMetadata(StatusKind.Offline, OnKindChanged));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(
            nameof(Label), typeof(string), typeof(StatusIndicator),
            new PropertyMetadata(string.Empty, OnLabelChanged));

    public StatusKind Kind
    {
        get => (StatusKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public StatusIndicator()
    {
        InitializeComponent();
        Refresh();
        LabelText.Text = Label;
    }

    private static void OnKindChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StatusIndicator indicator)
            indicator.Refresh();
    }

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StatusIndicator indicator)
            indicator.LabelText.Text = (string)e.NewValue;
    }

    private void Refresh()
    {
        Brush foreground = (Brush)FindResource("Brush.MutedForeground");
        Dot.Fill = foreground;
        Dot.Stroke = null;
        LabelText.Foreground = foreground;
        switch (Kind)
        {
            case StatusKind.Online:
                Dot.Fill = (Brush)FindResource("Brush.Success");
                break;
            case StatusKind.Offline:
                Dot.Fill = Brushes.Transparent;
                Dot.Stroke = foreground;
                Dot.StrokeThickness = 1.5;
                break;
            case StatusKind.Pending:
                Dot.Fill = (Brush)FindResource("Brush.Warning");
                break;
            case StatusKind.Failed:
                Dot.Fill = (Brush)FindResource("Brush.Destructive");
                break;
        }
    }
}
