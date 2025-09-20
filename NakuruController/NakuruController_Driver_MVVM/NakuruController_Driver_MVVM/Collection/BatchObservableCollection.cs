using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace NakuruController_Driver_MVVM.Collections;

public class BatchObservableCollection<T> : ObservableCollection<T>
{
    private bool _suppressNotification = false;

    public IDisposable SuspendNotifications()
    {
        _suppressNotification = true;
        return new DisposableAction(() =>
        {
            _suppressNotification = false;
            // PropertyChangedも通知
            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Reset));
        });
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressNotification)
            base.OnCollectionChanged(e);
    }

    public void AddRange(IEnumerable<T> items)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));

        using (SuspendNotifications())
        {
            foreach (var item in items)
            {
                Items.Add(item);
            }
        }
    }

    public void Reset(IEnumerable<T> items)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));

        using (SuspendNotifications())
        {
            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item);
            }
        }
    }

    private class DisposableAction : IDisposable
    {
        private readonly Action _action;

        public DisposableAction(Action action)
        {
            _action = action;
        }

        public void Dispose()
        {
            _action?.Invoke();
        }
    }
}
