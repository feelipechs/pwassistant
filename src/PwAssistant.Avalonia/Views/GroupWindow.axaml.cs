using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PwAssistant.Avalonia.Resources;
using PwAssistant.Avalonia.Services;
using PwAssistant.Avalonia.ViewModels;
using PwAssistant.Core.Models;

namespace PwAssistant.Avalonia.Views;

/// <summary>Paints a group card border with the Primary brush while dragged over.</summary>
public sealed class DragOverBorderConverter : IValueConverter
{
    public static DragOverBorderConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string key = value is true ? "Brush.Primary" : "Brush.Border";
        if (Application.Current?.TryFindResource(key, out object? res) == true && res is IBrush brush)
            return brush;
        return value is true ? Brushes.White : Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public partial class GroupWindow : Window
{
    public GroupViewModel ViewModel { get; }

    private readonly DispatcherTimer _onlinePoller;
    private readonly PresetEditorControl _editor;
    private readonly DragDirectionTracker _direction = new();

    private const string MemberFormat = "application/x-pwassistant-member";
    private static readonly DataFormat<MemberDrag> MemberDataFormat =
        DataFormat.CreateInProcessFormat<MemberDrag>(MemberFormat);
    private const double PressDragThreshold = 4;

    private Point _pressPoint;
    private bool _pressArmed;
    private PointerPressedEventArgs? _pressTrigger;
    private MemberOption? _pressOption;
    private ItemsControl? _pressList;
    private Guid? _pressSourceGroup;

    private Border? _ghost;
    private bool _dropHandled;
    private bool _isDragging;
    private bool _poolTargetOn;

    public GroupWindow(GroupViewModel viewModel, PresetEditorControl editor)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        WindowChrome.ApplyNative(this, ThemeManager.IsDark);
        _editor = editor;
        PresetEditorHost.Content = editor;
        editor.Saved += OnPresetEditorSaved;
        editor.Cancelled += OnPresetEditorCancelled;
        // Independent window (no Owner): owned windows always render above
        // their owner, which trapped Main below Group.
        TitleLabel.Text = Strings.GroupMode;
        PoolLabel.Text = Strings.Ungrouped;
        Loaded += (_, _) => ViewModel.Initialize();
        Activated += (_, _) => ViewModel.Refresh();
        // Event-oriented auto-refresh: picks up client deaths/starts without
        // requiring a window re-activation (cheap no-op when nothing changed).
        _onlinePoller = new DispatcherTimer(
            TimeSpan.FromSeconds(2), DispatcherPriority.Background,
            (_, _) => { if (IsVisible) { _ = ViewModel.RefreshIfOnlineChangedAsync(); } });
        _onlinePoller.Start();
        Closed += (_, _) => _onlinePoller.Stop();
        // Drag sources live inside templates: one tunnel pair at the window
        // finds the MemberOption under the press and starts the async drag.
        AddHandler(PointerPressedEvent, OnAnyPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnAnyPointerMoved, RoutingStrategies.Tunnel);
    }

    /// <summary>In-window confirm sheet (no extra window).</summary>
    public Task<bool> AskConfirmAsync(string title, string message, string confirmLabel) =>
        ConfirmSheetHost.AskAsync(title, message, confirmLabel);

    /// <summary>In-window text prompt sheet (no extra window).</summary>
    public Task<(bool Ok, string Value)> AskPromptAsync(string labelKey, string initial) =>
        PromptSheetHost.AskAsync(labelKey, initial);

    /// <summary>In-window formations panel (no extra window).</summary>
    public void ShowFormationsSheet() => FormationsSheetHost.Show();

    /// <summary>Switches the group view to the presets tab (full-bleed).</summary>
    public void ShowPresetsTab()
    {
        // Membership may have changed behind an open editor (DnD on the
        // cards tab): merge newcomers before showing, rows untouched.
        _editor.RefreshMemberScope();
        RootDock.IsVisible = false;
        PresetTab.IsVisible = true;
    }

    /// <summary>Back to the cards, guarding unsaved editor state.</summary>
    private async void ShowCardsTab()
    {
        if (_editor.HasUnsavedChanges() && !await _editor.ConfirmDiscardAsync())
            return;
        if (_editor.HasUnsavedChanges())
            _editor.Revert();
        RootDock.IsVisible = true;
        PresetTab.IsVisible = false;
    }

    private void OnPresetTabKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || _editor.IsRecordingHotkey) return;
        e.Handled = true;
        ShowCardsTab();
    }

    /// <summary>
    /// Loads a preset into the tab editor, guarding unsaved edits first
    /// (same discard question as leaving the tab).
    /// </summary>
    public async void LoadPresetInTab(Preset preset, bool isNew)
    {
        if (_editor.HasUnsavedChanges() && !await _editor.ConfirmDiscardAsync())
            return;
        _editor.LoadPreset(preset, isNew);
        ShowPresetsTab();
    }

    private async void OnPresetEditorSaved(Preset preset, bool isNew)
    {
        // Stay in the editor for continuous editing: the persist reports
        // progress through StatusMessage/toast instead of holding the tab.
        // The editor re-arms Save via NotifySaveCompleted below.
        try
        {
            await ViewModel.PersistPresetAsync(preset, isNew);
        }
        finally
        {
            _editor.NotifySaveCompleted();
        }
    }

    private void OnPresetEditorCancelled() => ShowCardsTab();

    private void OnCardMenuToggle(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is GroupCard card)
            card.IsMenuOpen = !card.IsMenuOpen;
    }

    private void OnCardMenuClosed(object? sender, EventArgs e)
    {
        if (sender is Popup popup && popup.DataContext is GroupCard card)
            card.IsMenuOpen = false;
    }

    // ---- Member drag (pool <-> cards, Trello LiveMove) ----

    private void OnAnyPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _pressArmed = false;
        _pressOption = null;
        _pressList = null;
        _pressSourceGroup = null;
        if (_isDragging) return;

        Point p = e.GetPosition(this);
        Visual? hit = this.GetVisualsAt(p).FirstOrDefault();
        MemberOption? option = FindDataContext<MemberOption>(hit);
        if (option is null) return;
        ItemsControl? list = FindAncestor<ItemsControl>(hit);
        if (list is null) return;

        _pressPoint = p;
        _pressArmed = true;
        _pressTrigger = e;
        _pressOption = option;
        _pressList = list;
        _pressSourceGroup = FindDataContext<GroupCard>(hit)?.Group.Id;
    }

    private async void OnAnyPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_pressArmed || _isDragging || _pressOption is null || _pressList is null) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Point p = e.GetPosition(this);
        if (Math.Abs(p.X - _pressPoint.X) < PressDragThreshold &&
            Math.Abs(p.Y - _pressPoint.Y) < PressDragThreshold)
            return;

        MemberOption option = _pressOption;
        Guid? sourceGroup = _pressSourceGroup;
        PointerPressedEventArgs? trigger = _pressTrigger;
        _pressArmed = false;
        _pressTrigger = null;
        if (trigger is null) return;
        await BeginMemberDragAsync(trigger, new MemberDrag(option.Account.Id, sourceGroup), option);
    }

    private async Task BeginMemberDragAsync(PointerPressedEventArgs trigger, MemberDrag payload, MemberOption option)
    {
        _ghost = BuildGhost(option);
        GhostLayer.Children.Add(_ghost);
        PositionGhost(null);
        ViewModel.SuppressAutoRefresh = true;
        _isDragging = true;
        _dropHandled = false;
        _direction.Reset();
        try
        {
            var data = new DataTransfer();
            data.Add(DataTransferItem.Create(MemberDataFormat, payload));
            await DragDrop.DoDragDropAsync(trigger, data, DragDropEffects.Move);
        }
        finally
        {
            _isDragging = false;
            GhostLayer.Children.Remove(_ghost);
            _ghost = null;
            SetDragOverCard(null);
            SetPoolTarget(false);
            ViewModel.SuppressAutoRefresh = false;
            // Cancel/ESC/release outside: LiveMove only touched the UI —
            // rebuild from the model so a pool→group hover doesn't stick.
            if (!_dropHandled)
                ViewModel.RebuildAll();
            _dropHandled = false;
        }
    }

    /// <summary>Trello-style lifted card: MiniCard look + drop shadow.</summary>
    private Border BuildGhost(MemberOption option)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        if (!string.IsNullOrEmpty(option.ClassImagePath))
        {
            // A missing class png must never abort the drag — text is enough.
            try
            {
                using Stream stream = AssetLoader.Open(new Uri(option.ClassImagePath));
                row.Children.Add(new Image
                {
                    Width = 16,
                    Height = 16,
                    Margin = new Thickness(0, 0, 8, 0),
                    Source = new Bitmap(stream),
                });
            }
            catch (Exception)
            {
                // Ghost stays text-only; the drag proceeds.
            }
        }
        row.Children.Add(new TextBlock
        {
            Text = option.CharacterName,
            VerticalAlignment = VerticalAlignment.Center,
        });
        IBrush background = Application.Current?.TryFindResource("Brush.Raised", out object? bgRes) == true && bgRes is IBrush bg ? bg : Brushes.Gray;
        IBrush border = Application.Current?.TryFindResource("Brush.Primary", out object? bdRes) == true && bdRes is IBrush bd ? bd : Brushes.White;
        return new Border
        {
            Background = background,
            BorderBrush = border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 6, 10, 6),
            Child = row,
            Opacity = 0.95,
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0,
                OffsetY = 4,
                Blur = 12,
                Color = Color.FromArgb(128, 0, 0, 0),
            }),
        };
    }

    private static T? FindDataContext<T>(Visual? node) where T : class
    {
        while (node is not null)
        {
            if ((node as Control)?.DataContext is T match)
                return match;
            node = node.GetVisualParent();
        }
        return null;
    }

    private static T? FindAncestor<T>(Visual? node) where T : Visual
    {
        while (node is not null)
        {
            if (node is T match)
                return match;
            node = node.GetVisualParent();
        }
        return null;
    }

    /// <summary>Ghost follows every DragOver (live in Avalonia: no modal loop).</summary>
    private void PositionGhost(Point? anchor)
    {
        if (_ghost is null || GhostLayer is null) return;
        Point p = anchor ?? new Point(0, 0);
        Canvas.SetLeft(_ghost, p.X + 14);
        Canvas.SetTop(_ghost, p.Y + 14);
    }

    private void SetDragOverCard(GroupCard? card)
    {
        foreach (GroupCard c in ViewModel.GroupCards)
            c.IsDragOver = c == card;
        SetPoolTarget(card is null && IsOverPool());
    }

    private bool IsOverPool()
    {
        // Called from highlight paths that carry no position: fall back to
        // the pool border state instead of hit-testing without coordinates.
        return _poolTargetOn;
    }

    /// <summary>Pool-column drop-target feedback (border lights up).</summary>
    private void SetPoolTarget(bool on)
    {
        if (_poolTargetOn == on) return;
        _poolTargetOn = on;
        if (Application.Current?.TryFindResource(on ? "Brush.Primary" : "Brush.Border", out object? res) == true && res is IBrush brush)
            PoolBorder.BorderBrush = brush;
    }

    private static GroupCard? CardOf(object? sender) =>
        (sender as Control)?.DataContext as GroupCard;

    private static MemberDrag? DragOf(DragEventArgs e) =>
        e.DataTransfer.TryGetValue(MemberDataFormat);

    private const double DragAutoScrollZone = 32;
    private const double DragAutoScrollStep = 20;

    /// <summary>LiveMove flips the slot ~10% into a row (on touch).</summary>
    private const double LiveSwapFraction = 0.1;

    /// <summary>
    /// Explorer-style edge scrolling during a drag: the innermost scroller
    /// under the cursor that can still move wins; the parent takes over
    /// at the extent.
    /// </summary>
    private static void AutoScrollDuringDrag(DragEventArgs e, Visual start)
    {
        Visual? node = start;
        while (node is not null)
        {
            if (node is ScrollViewer sv && sv.Extent.Height > sv.Viewport.Height)
            {
                Point p = e.GetPosition(sv);
                if (p.Y >= 0 && p.Y <= sv.Viewport.Height)
                {
                    if (p.Y < DragAutoScrollZone && sv.Offset.Y > 0)
                    {
                        sv.Offset = sv.Offset.WithY(Math.Max(0, sv.Offset.Y - DragAutoScrollStep));
                        return;
                    }
                    if (p.Y > sv.Viewport.Height - DragAutoScrollZone &&
                        sv.Offset.Y < sv.Extent.Height - sv.Viewport.Height)
                    {
                        sv.Offset = sv.Offset.WithY(Math.Min(
                            sv.Extent.Height - sv.Viewport.Height, sv.Offset.Y + DragAutoScrollStep));
                        return;
                    }
                }
            }
            node = node.GetVisualParent();
        }
    }

    /// <summary>
    /// The wheel during a drag reroutes to the innermost scroller under the
    /// cursor that can still move (extent yields to the parent).
    /// </summary>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (!_isDragging) return;
        e.Handled = true;
        Visual? hit = this.GetVisualsAt(e.GetPosition(this)).FirstOrDefault();
        bool up = e.Delta.Y > 0;
        Visual? node = hit;
        while (node is not null)
        {
            if (node is ScrollViewer sv)
            {
                bool canUp = sv.Offset.Y > 0;
                bool canDown = sv.Offset.Y < sv.Extent.Height - sv.Viewport.Height;
                if ((up && canUp) || (!up && canDown))
                {
                    double step = 48 * Math.Max(1, (int)Math.Abs(e.Delta.Y));
                    sv.Offset = sv.Offset.WithY(Math.Clamp(
                        sv.Offset.Y + (up ? -step : step), 0,
                        Math.Max(0, sv.Extent.Height - sv.Viewport.Height)));
                    return;
                }
            }
            node = node.GetVisualParent();
        }
    }

    private void OnOuterDragOver(object? sender, DragEventArgs e)
    {
        PositionGhost(e.GetPosition(GhostLayer));
        // Gaps between cards are not a landing spot — no card lights up.
        SetDragOverCard(null);
        if (DragOf(e) is null)
        {
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }
        // Gaps between cards scroll but are not a valid landing spot.
        if (sender is Visual visual)
            AutoScrollDuringDrag(e, visual);
        SetPoolTarget(false);
        e.DragEffects = DragDropEffects.None;
        e.Handled = true;
    }

    private void OnPoolDragOver(object? sender, DragEventArgs e)
    {
        PositionGhost(e.GetPosition(GhostLayer));
        MemberDrag? drag = DragOf(e);
        if (drag is not null && sender is Visual visual)
        {
            AutoScrollDuringDrag(e, visual);
            SetDragOverCard(null);
            SetPoolTarget(true);
            // Preview ungroup: LiveMove may have put the item in a card
            // while hovering — mirror it back to the pool until drop.
            LiveRemove(drag.AccountId);
            e.DragEffects = DragDropEffects.Move;
        }
        else
        {
            SetPoolTarget(false);
            e.DragEffects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private async void OnPoolDrop(object? sender, DragEventArgs e)
    {
        MemberDrag? drag = DragOf(e);
        if (drag is null) return;
        _dropHandled = true;
        SetPoolTarget(false);
        try
        {
            LiveRemove(drag.AccountId);
            await ViewModel.PersistGroupOrderAsync();
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = ex.Message;
        }
    }

    private void OnCardDragOver(object? sender, DragEventArgs e)
    {
        PositionGhost(e.GetPosition(GhostLayer));
        if (CardOf(sender) is GroupCard card && DragOf(e) is not null && sender is Visual visual)
        {
            AutoScrollDuringDrag(e, visual);
            // Sender-based: this handler knows its card — no hit-test,
            // which freezes on the source card after LiveMove churn.
            SetDragOverCard(card);
            SetPoolTarget(false);
            // Header/margins: highlight only — the members list owns the
            // live move; append happens on drop (OnCardDrop).
            e.DragEffects = DragDropEffects.Move;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private async void OnCardDrop(object? sender, DragEventArgs e)
    {
        MemberDrag? drag = DragOf(e);
        if (drag is null) return;
        if (CardOf(sender) is not GroupCard card) return;
        _dropHandled = true;
        SetDragOverCard(null);
        try
        {
            // Drop on header/margins appends; the members list moved live.
            MoveMember(drag, card, card.Members.Count);
            await ViewModel.PersistGroupOrderAsync();
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = ex.Message;
        }
    }

    /// <summary>
    /// Live reorder: the dragged row moves inside the UI collections while
    /// hovering (past the row midpoint), so placement is exact before drop.
    /// The model is untouched until drop; cancel reverts via RebuildAll.
    /// </summary>
    private void OnMembersDragOver(object? sender, DragEventArgs e)
    {
        PositionGhost(e.GetPosition(GhostLayer));
        if (sender is not ItemsControl list || DragOf(e) is not MemberDrag drag)
        {
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }
        GroupCard? card = CardOf(sender);
        if (card is null)
        {
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }
        AutoScrollDuringDrag(e, list);
        // Sender-based highlight (see OnCardDragOver): card is known.
        SetDragOverCard(card);
        SetPoolTarget(false);
        int index = InsertionPreview.IndexAt(list, e.GetPosition(list), out _, LiveSwapFraction, _direction.Track(e, this));
        MoveMember(drag, card, index);
        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
    }

    private async void OnMembersDrop(object? sender, DragEventArgs e)
    {
        // Single drop: the card border below would persist a second time.
        e.Handled = true;
        MemberDrag? drag = DragOf(e);
        if (drag is null) return;
        if (CardOf(sender) is not GroupCard card) return;
        _dropHandled = true;
        SetDragOverCard(null);
        try
        {
            // Drop where released (not append): the hover preview already
            // placed it nearby; the release position decides the slot.
            int index = sender is ItemsControl list
                ? InsertionPreview.IndexAt(list, e.GetPosition(list), out _, LiveSwapFraction, _direction.Track(e, this))
                : card.Members.Count;
            MoveMember(drag, card, index);
            await ViewModel.PersistGroupOrderAsync();
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = ex.Message;
        }
    }

    private void MoveMember(MemberDrag drag, GroupCard targetCard, int index)
    {
        MemberOption? option = ViewModel.Pool
            .FirstOrDefault(m => m.Account.Id == drag.AccountId);
        ObservableCollection<MemberOption>? source = option is not null
            ? ViewModel.Pool
            : null;
        if (option is null)
        {
            foreach (GroupCard card in ViewModel.GroupCards)
            {
                option = card.Members.FirstOrDefault(m => m.Account.Id == drag.AccountId);
                if (option is not null)
                {
                    source = card.Members;
                    break;
                }
            }
        }
        if (source is null || option is null) return;

        var targetList = targetCard.Members;
        if (ReferenceEquals(source, targetList))
        {
            int from = source.IndexOf(option);
            int to = index;
            if (to > from) to--;
            to = Math.Clamp(to, 0, source.Count - 1);
            if (to == from) return;
            source.RemoveAt(from);
            targetList.Insert(to, option);
        }
        else
        {
            source.Remove(option);
            targetList.Insert(Math.Clamp(index, 0, targetList.Count), option);
        }
        ViewModel.RefreshCardCounts();
    }

    /// <summary>
    /// UI-only ungroup: takes the option out of its card and shows it back
    /// in the pool (top). Persist derives "ungrouped" as "in no card", so a
    /// drop just persists; a cancel rebuilds from the model.
    /// </summary>
    private void LiveRemove(Guid accountId)
    {
        if (ViewModel.Pool.Any(m => m.Account.Id == accountId)) return;
        foreach (GroupCard card in ViewModel.GroupCards)
        {
            MemberOption? option = card.Members.FirstOrDefault(m => m.Account.Id == accountId);
            if (option is not null)
            {
                card.Members.Remove(option);
                ViewModel.Pool.Insert(0, option);
                ViewModel.RefreshCardCounts();
                return;
            }
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Hide-cache like Mini: reopening reuses the instance (editor drafts
        // survive hidden). Shutdown closes for real (AppShutdown, not
        // Application.Current — which stays non-null during shutdown, so the
        // old check cancelled the close forever and Shutdown never finished).
        if (!AppShutdown.Requested)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }
}
