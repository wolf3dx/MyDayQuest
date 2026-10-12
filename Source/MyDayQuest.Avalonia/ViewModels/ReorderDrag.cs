using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace MyDayQuest.Avalonia.ViewModels;

/// <summary>
/// Перетаскивание строк мышью — одинаково для ленты листов и для подзаданий.
///
/// Сделано на простых событиях указателя, а не на системном перетаскивании:
/// переносим внутри одного списка, обмен данными с другими окнами не нужен,
/// а порог сдвига и поиск строки под указателем всё равно пришлось бы писать.
/// Указатель намеренно не захватываем: без захвата события продолжают
/// приходить от той строки, над которой мышь сейчас находится, — по ней и
/// видно, куда метят.
/// </summary>
public sealed class ReorderDrag<T>(
    Func<Control, bool> canStart,
    Action<T, bool> setDropTarget,
    Action clearDropTargets,
    Func<T, T, Task> moveAsync)
    where T : class
{
    private const double Threshold = 8;

    private T? _candidate;
    private T? _dragged;
    private Point _start;

    /// <summary>Подписаться на события списка. Перехватываем на пути вниз,
    /// иначе кнопка-закладка съела бы нажатие раньше нас.</summary>
    public void Attach(Control host)
    {
        host.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
        host.AddHandler(InputElement.PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel);
        host.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Tunnel);
    }

    private static T? ItemOf(object? source)
    {
        var c = source as Control;
        while (c is not null)
        {
            if (c.DataContext is T item) return item;
            c = c.Parent as Control;
        }
        return null;
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        Reset();
        if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed) return;
        if (e.Source is not Control c || !canStart(c)) return;

        _candidate = ItemOf(c);
        _start = e.GetPosition(null);
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (_candidate is null) return;

        // Кнопку отпустили мимо списка — считаем перенос отменённым.
        if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
        {
            Reset();
            return;
        }

        if (_dragged is null)
        {
            var d = e.GetPosition(null) - _start;
            if (Math.Abs(d.X) < Threshold && Math.Abs(d.Y) < Threshold) return;
            _dragged = _candidate;
        }

        clearDropTargets();
        if (ItemOf(e.Source) is { } over && !ReferenceEquals(over, _dragged))
            setDropTarget(over, true);
    }

    private async void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        var moved = _dragged;
        var target = ItemOf(e.Source);
        Reset();
        if (moved is null) return;

        // Нажатие закончилось переносом — обычный щелчок по строке не нужен.
        e.Handled = true;
        if (target is not null && !ReferenceEquals(target, moved))
            await moveAsync(moved, target);
    }

    private void Reset()
    {
        _candidate = null;
        _dragged = null;
        clearDropTargets();
    }
}
