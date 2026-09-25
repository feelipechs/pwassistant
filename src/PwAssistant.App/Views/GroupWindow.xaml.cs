using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using PwAssistant.App.Resources;
using PwAssistant.App.Services;
using PwAssistant.App.ViewModels;
using PwAssistant.Core.Models;

namespace PwAssistant.App.Views;

public partial class GroupWindow : Window
{
    public GroupViewModel ViewModel { get; }

    private readonly System.Windows.Threading.DispatcherTimer _onlinePoller;
    private Point _dragStartPoint;
    private bool _isDragging;
    private readonly PresetEditorControl _editor;

    public GroupWindow(GroupViewModel viewModel, PresetEditorControl editor)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        _editor = editor;
        PresetEditorHost.Content = editor;
        editor.Saved += OnPresetEditorSaved;
        editor.Cancelled += OnPresetEditorCancelled;
        DialogOwner.Own(this);
        TitleLabel.Text = Strings.GroupMode;
        PoolLabel.Text = Strings.Ungrouped;
        Loaded += (_, _) => ViewModel.Initialize();
        Activated += (_, _) => ViewModel.Refresh();
        // Event-oriented auto-refresh: picks up client deaths/starts without
        // requiring a window re-activation (cheap no-op when nothing changed).
        _onlinePoller = new System.Windows.Threading.DispatcherTimer(
            TimeSpan.FromSeconds(2),
            System.Windows.Threading.DispatcherPriority.Background,
            (_, _) => ViewModel.RefreshIfOnlineChanged(),
            Dispatcher);
        _onlinePoller.Start();
        Closed += (_, _) => _onlinePoller.Stop();
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
        RootDock.Visibility = Visibility.Collapsed;
        PresetTab.Visibility = Visibility.Visible;
    }

    /// <summary>Back to the cards, guarding unsaved editor state.</summary>
    private async void ShowCardsTab()
    {
        if (_editor.HasUnsavedChanges() && !await _editor.ConfirmDiscardAsync())
            return;
        if (_editor.HasUnsavedChanges())
            _editor.Revert();
        RootDock.Visibility = Visibility.Visible;
        PresetTab.Visibility = Visibility.Collapsed;
    }

    private void OnPresetTabKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || _editor.IsRecordingHotkey) return;
        e.Handled = true;
        ShowCardsTab();
    }

    /// <summary>Loads a preset into the tab editor.</summary>
    public void LoadPresetInTab(Preset preset, bool isNew)
    {
        _editor.LoadPreset(preset, isNew);
        ShowPresetsTab();
    }

    private async void OnPresetEditorSaved(Preset preset, bool isNew)
    {
        await ViewModel.PersistPresetAsync(preset, isNew);
        ShowCardsTab();
    }

    private void OnPresetEditorCancelled() => ShowCardsTab();

    private void OnCardMenuToggle(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is GroupCard card)
            card.IsMenuOpen = !card.IsMenuOpen;
    }

    private void OnCardMenuClosed(object? sender, EventArgs e)
    {
        if (sender is System.Windows.Controls.Primitives.Popup popup
            && popup.DataContext is GroupCard card)
            card.IsMenuOpen = false;
    }

    private Border? _ghost;
    private bool _dropHandled;
    private readonly DragDirectionTracker _direction = new();

    private void OnPoolPreviewMouseDown(object sender, MouseButtonEventArgs e) =>
        _dragStartPoint = e.GetPosition(null);

    private void OnPoolPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (sender is not ItemsControl list) return;
        Point current = e.GetPosition(null);
        if (Math.Abs(current.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;
        MemberOption? option = FindDataContext<MemberOption>(
            list.InputHitTest(e.GetPosition(list)) as DependencyObject);
        if (option is null) return;
        BeginMemberDrag(list, new MemberDrag(option.Account.Id, null), option);
    }

    private void OnMemberPreviewMouseDown(object sender, MouseButtonEventArgs e) =>
        _dragStartPoint = e.GetPosition(null);

    private void OnMemberPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (sender is not ItemsControl list) return;
        Point current = e.GetPosition(null);
        if (Math.Abs(current.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;
        MemberOption? option = FindDataContext<MemberOption>(list.InputHitTest(e.GetPosition(list)) as DependencyObject);
        GroupCard? card = FindDataContext<GroupCard>(list);
        if (option is null || card is null) return;
        BeginMemberDrag(list, new MemberDrag(option.Account.Id, card.Group.Id), option);
    }

    private void BeginMemberDrag(FrameworkElement source, MemberDrag payload, MemberOption option)
    {
        // Canvas overlay (not AdornerLayer): DoDragDrop's modal loop does
        // not reliably repaint adorners — the ghost never showed.
        _ghost = BuildGhost(option);
        GhostLayer.Children.Add(_ghost);
        PositionGhost();
        source.GiveFeedback += OnDragFeedback;
        ViewModel.SuppressAutoRefresh = true;
        _isDragging = true;
        _dropHandled = false;
        _direction.Reset();
        try
        {
            DragDrop.DoDragDrop(source, payload, DragDropEffects.Move);
        }
        finally
        {
            _isDragging = false;
            source.GiveFeedback -= OnDragFeedback;
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
                row.Children.Add(new Image
                {
                    Width = 16,
                    Height = 16,
                    Margin = new Thickness(0, 0, 8, 0),
                    Source = new System.Windows.Media.Imaging.BitmapImage(
                        new Uri(option.ClassImagePath, UriKind.Relative))
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
            VerticalAlignment = VerticalAlignment.Center
        });
        return new Border
        {
            Background = (Brush)FindResource("Brush.Raised"),
            BorderBrush = (Brush)FindResource("Brush.Primary"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 6, 10, 6),
            Child = row,
            Opacity = 0.95,
            Effect = new DropShadowEffect
            {
                BlurRadius = 12,
                ShadowDepth = 4,
                Opacity = 0.5,
                Color = Colors.Black
            }
        };
    }

    private static T? FindDataContext<T>(DependencyObject? node) where T : class
    {
        while (node is not null)
        {
            if ((node as FrameworkElement)?.DataContext is T match)
                return match;
            node = VisualTreeHelper.GetParent(node);
        }
        return null;
    }

    private void OnDragFeedback(object? sender, GiveFeedbackEventArgs e)
    {
        PositionGhost();
        UpdateDragOverFromCursor();
        e.UseDefaultCursors = true;
        e.Handled = true;
    }

    /// <summary>
    /// Ghost tracking, driven from GiveFeedback AND every DragOver.
    /// Event coordinates are preferred: Mouse.GetPosition is unreliable
    /// inside DoDragDrop's modal loop; DragEventArgs positions come fresh
    /// from COM on every move. GiveFeedback carries no position — Mouse
    /// fallback there.
    /// </summary>
    private void PositionGhost(Point? anchor = null)
    {
        if (_ghost is null) return;
        Point p = anchor ?? Mouse.GetPosition(GhostLayer);
        Canvas.SetLeft(_ghost, p.X + 14);
        Canvas.SetTop(_ghost, p.Y + 14);
    }

    /// <summary>
    /// Highlight follows the cursor via hit-test (GiveFeedback/DragOver)
    /// instead of Enter/Leave pairs, which flapped the border on every
    /// target change. A null hit-test mid-drag means the visual tree is
    /// churning (LiveMove) — keep the current highlight, never flash off.
    /// </summary>
    private void UpdateDragOverFromCursor()
    {
        if (InputHitTest(Mouse.GetPosition(this)) is not DependencyObject hit)
            return;
        SetDragOverCard(FindDataContext<GroupCard>(hit));
    }

    private void SetDragOverCard(GroupCard? card)
    {
        foreach (GroupCard c in ViewModel.GroupCards)
            c.IsDragOver = c == card;
        SetPoolTarget(card is null && IsOverPool());
    }

    private bool IsOverPool() =>
        InputHitTest(Mouse.GetPosition(this)) is DependencyObject hit
        && IsDescendantOf(hit, PoolBorder);

    private static bool IsDescendantOf(DependencyObject node, DependencyObject ancestor)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, ancestor)) return true;
            node = VisualTreeHelper.GetParent(node);
        }
        return false;
    }

    private bool _poolTargetOn;

    /// <summary>Pool-column drop-target feedback (border lights up).</summary>
    private void SetPoolTarget(bool on)
    {
        if (_poolTargetOn == on) return;
        _poolTargetOn = on;
        PoolBorder.BorderBrush = (Brush)FindResource(on ? "Brush.Primary" : "Brush.Border");
    }

    private static GroupCard? CardOf(object? sender) =>
        (sender as FrameworkElement)?.DataContext as GroupCard;

    private static MemberDrag? DragOf(DragEventArgs e) =>
        e.Data.GetData(typeof(MemberDrag)) as MemberDrag;

    private const double DragAutoScrollZone = 32;
    private const double DragAutoScrollStep = 20;

    /// <summary>LiveMove flips the slot ~10% into a row (on touch).</summary>
    private const double LiveSwapFraction = 0.1;

    /// <summary>
    /// Explorer-style edge scrolling: <c>DoDragDrop</c> holds the mouse
    /// capture, so the wheel never reaches the ScrollViewers during a drag.
    /// Walks the ancestor scrollers — the innermost that can still move in
    /// the pointer direction wins; at the extent the parent takes over.
    /// </summary>
    private static void AutoScrollDuringDrag(DragEventArgs e, DependencyObject start)
    {
        DependencyObject? node = start;
        while (node is not null)
        {
            if (node is ScrollViewer sv && sv.ScrollableHeight > 0)
            {
                Point p = e.GetPosition(sv);
                if (p.Y >= 0 && p.Y <= sv.ViewportHeight)
                {
                    if (p.Y < DragAutoScrollZone && sv.VerticalOffset > 0)
                    {
                        sv.ScrollToVerticalOffset(sv.VerticalOffset - DragAutoScrollStep);
                        return;
                    }
                    if (p.Y > sv.ViewportHeight - DragAutoScrollZone &&
                        sv.VerticalOffset < sv.ScrollableHeight)
                    {
                        sv.ScrollToVerticalOffset(sv.VerticalOffset + DragAutoScrollStep);
                        return;
                    }
                }
            }
            node = VisualTreeHelper.GetParent(node);
        }
    }

    /// <summary>
    /// The wheel during a drag: capture keeps it from the ScrollViewers, so
    /// the window reroutes it to the innermost scroller under the cursor
    /// that can still move (extent yields to the parent, as outside drags).
    /// </summary>
    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        base.OnPreviewMouseWheel(e);
        if (!_isDragging) return;
        e.Handled = true;
        if (InputHitTest(e.GetPosition(this)) is not DependencyObject hit) return;
        bool up = e.Delta > 0;
        int lines = SystemParameters.WheelScrollLines;
        if (lines <= 0) lines = 3;
        int steps = Math.Max(1, Math.Abs(e.Delta) / 120) * lines;
        DependencyObject? node = hit;
        while (node is not null)
        {
            if (node is ScrollViewer sv)
            {
                bool canUp = sv.VerticalOffset > 0;
                bool canDown = sv.VerticalOffset < sv.ScrollableHeight;
                if ((up && canUp) || (!up && canDown))
                {
                    for (int i = 0; i < steps; i++)
                    {
                        if (up) sv.LineUp();
                        else sv.LineDown();
                    }
                    return;
                }
            }
            node = VisualTreeHelper.GetParent(node);
        }
    }

    private void OnOuterDragOver(object sender, DragEventArgs e)
    {
        PositionGhost(e.GetPosition(GhostLayer));
        // Gaps between cards are not a landing spot — no card lights up.
        SetDragOverCard(null);
        if (DragOf(e) is null)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }
        // Gaps between cards scroll but are not a valid landing spot.
        AutoScrollDuringDrag(e, (DependencyObject)sender);
        SetPoolTarget(false);
        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void OnPoolDragOver(object sender, DragEventArgs e)
    {
        PositionGhost(e.GetPosition(GhostLayer));
        MemberDrag? drag = DragOf(e);
        if (drag is not null)
        {
            AutoScrollDuringDrag(e, (DependencyObject)sender);
            SetDragOverCard(null);
            SetPoolTarget(true);
            // Preview ungroup: LiveMove may have put the item in a card
            // while hovering — mirror it back to the pool until drop.
            LiveRemove(drag.AccountId);
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            SetPoolTarget(false);
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private async void OnPoolDrop(object sender, DragEventArgs e)
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

    private void OnCardDragOver(object sender, DragEventArgs e)
    {
        PositionGhost(e.GetPosition(GhostLayer));
        if (CardOf(sender) is GroupCard card && DragOf(e) is not null)
        {
            AutoScrollDuringDrag(e, (DependencyObject)sender);
            // Sender-based: this handler knows its card — no hit-test,
            // which freezes on the source card after LiveMove churn.
            SetDragOverCard(card);
            SetPoolTarget(false);
            // Header/margins: highlight only — the members list owns the
            // live move; append happens on drop (OnCardDrop).
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private async void OnCardDrop(object sender, DragEventArgs e)
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
    private void OnMembersDragOver(object sender, DragEventArgs e)
    {
        PositionGhost(e.GetPosition(GhostLayer));
        if (sender is not ItemsControl list || DragOf(e) is not MemberDrag drag)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }
        GroupCard? card = CardOf(sender);
        if (card is null)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }
        AutoScrollDuringDrag(e, list);
        // Sender-based highlight (see OnCardDragOver): card is known.
        SetDragOverCard(card);
        SetPoolTarget(false);
        int index = InsertionPreview.IndexAt(list, e.GetPosition(list), out _, LiveSwapFraction, _direction.Track(e, this));
        MoveMember(drag, card, index);
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private async void OnMembersDrop(object sender, DragEventArgs e)
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
}
