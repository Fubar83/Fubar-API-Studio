using System.Collections;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace Fubar.Studio.UI.Controls;

/// <summary>
/// Attached to the same TextBoxes as <see cref="VariableTooltip"/> (URL bar, header key/value
/// cells): while the caret sits inside an unterminated <c>{{partial</c> token, shows a filterable
/// popup listing the active environment's variable names, navigable with the mouse or Up/Down/Enter/
/// Escape, that completes the token to <c>{{name}}</c> in place.
/// </summary>
public static partial class VariableIntellisense
{
    public static readonly AttachedProperty<VariableTooltipContext?> ContextProperty =
        AvaloniaProperty.RegisterAttached<TextBox, VariableTooltipContext?>("Context", typeof(VariableIntellisense));

    public static void SetContext(TextBox element, VariableTooltipContext? value) => element.SetValue(ContextProperty, value);
    public static VariableTooltipContext? GetContext(TextBox element) => element.GetValue(ContextProperty);

    private static readonly AttachedProperty<Popup?> PopupProperty =
        AvaloniaProperty.RegisterAttached<TextBox, Popup?>("IntellisensePopup", typeof(VariableIntellisense));

    private static readonly AttachedProperty<ListBox?> ListProperty =
        AvaloniaProperty.RegisterAttached<TextBox, ListBox?>("IntellisenseList", typeof(VariableIntellisense));

    static VariableIntellisense()
    {
        ContextProperty.Changed.AddClassHandler<TextBox>((box, _) => EnsureAttached(box));
    }

    private static void EnsureAttached(TextBox box)
    {
        if (box.GetValue(PopupProperty) is not null)
        {
            return;
        }

        var list = new ListBox
        {
            Focusable = false,
            Background = Brushes.Transparent,
        };

        var border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(2),
            MaxHeight = 180,
            MinWidth = 160,
            Child = list,
        };
        border.Bind(Border.BackgroundProperty, new DynamicResourceExtension("BgHeader"));
        border.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("BorderSubtle"));

        var popup = new Popup
        {
            PlacementTarget = box,
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            IsLightDismissEnabled = true,
            Child = border,
        };

        ((ISetLogicalParent)popup).SetParent(box);

        box.SetValue(PopupProperty, popup);
        box.SetValue(ListProperty, list);

        box.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty || e.Property == TextBox.CaretIndexProperty)
            {
                Refresh(box);
            }
        };
        box.AddHandler(InputElement.KeyDownEvent, (_, e) => OnKeyDown(box, e), RoutingStrategies.Tunnel);
        box.LostFocus += (_, _) => popup.IsOpen = false;
        list.PointerReleased += (_, _) =>
        {
            if (list.SelectedItem is string name)
            {
                Commit(box, name);
            }
        };
    }

    private static void Refresh(TextBox box)
    {
        var popup = box.GetValue(PopupProperty);
        var list = box.GetValue(ListProperty);
        if (popup is null || list is null)
        {
            return;
        }

        var context = GetContext(box);
        var text = box.Text ?? "";
        var caret = Math.Clamp(box.CaretIndex, 0, text.Length);
        var prefix = TryGetPartialToken(text, caret);

        if (context?.ActiveEnvironment is null || prefix is null)
        {
            popup.IsOpen = false;
            return;
        }

        var matches = context.ActiveEnvironment.Variables
            .Select(v => v.Key)
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (matches.Count == 0)
        {
            popup.IsOpen = false;
            return;
        }

        list.ItemsSource = matches;
        list.SelectedIndex = 0;
        popup.IsOpen = true;
    }

    /// <summary>
    /// If the caret sits right after an unterminated <c>{{</c> (no <c>}}</c> or whitespace between
    /// it and the caret), returns the partial variable name typed so far - possibly empty right
    /// after typing <c>{{</c>. Returns null when the caret isn't inside such a token.
    /// </summary>
    private static string? TryGetPartialToken(string text, int caret)
    {
        var searchStart = caret - 1;
        if (searchStart < 0 || searchStart >= text.Length)
        {
            searchStart = Math.Min(caret - 1, text.Length - 1);
        }

        if (caret < 2)
        {
            return null;
        }

        var openIdx = text.LastIndexOf("{{", Math.Min(searchStart, text.Length - 1), StringComparison.Ordinal);
        if (openIdx < 0)
        {
            return null;
        }

        var between = text[(openIdx + 2)..caret];
        return WordPrefixRegex().IsMatch(between) ? between : null;
    }

    private static void Commit(TextBox box, string name)
    {
        var text = box.Text ?? "";
        var caret = Math.Clamp(box.CaretIndex, 0, text.Length);
        var searchStart = Math.Min(caret - 1, text.Length - 1);
        var openIdx = text.LastIndexOf("{{", Math.Max(searchStart, 0), StringComparison.Ordinal);
        if (openIdx < 0)
        {
            return;
        }

        var replacement = $"{{{{{name}}}}}";
        var newText = text[..openIdx] + replacement + text[caret..];
        box.Text = newText;
        box.CaretIndex = openIdx + replacement.Length;

        if (box.GetValue(PopupProperty) is { } popup)
        {
            popup.IsOpen = false;
        }
    }

    private static void OnKeyDown(TextBox box, KeyEventArgs e)
    {
        var popup = box.GetValue(PopupProperty);
        var list = box.GetValue(ListProperty);
        if (popup is not { IsOpen: true } || list is null)
        {
            return;
        }

        var count = (list.ItemsSource as ICollection)?.Count ?? 0;
        switch (e.Key)
        {
            case Key.Down:
                list.SelectedIndex = count == 0 ? -1 : Math.Min(list.SelectedIndex + 1, count - 1);
                e.Handled = true;
                break;
            case Key.Up:
                list.SelectedIndex = count == 0 ? -1 : Math.Max(list.SelectedIndex - 1, 0);
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Tab:
                if (list.SelectedItem is string name)
                {
                    Commit(box, name);
                    e.Handled = true;
                }
                break;
            case Key.Escape:
                popup.IsOpen = false;
                e.Handled = true;
                break;
        }
    }

    [GeneratedRegex(@"^\w*$")]
    private static partial Regex WordPrefixRegex();
}
