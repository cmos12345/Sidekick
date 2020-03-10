using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Sidekick.Core.Natives;
using Sidekick.UI.Views;
using MyCursor = System.Windows.Forms.Cursor;

namespace Sidekick.Windows
{
    public abstract class BaseWindow : Window, ISidekickView
    {
        private readonly IKeybindEvents keybindEvents;

        public BaseWindow(IServiceProvider serviceProvider)
        {
            keybindEvents = serviceProvider.GetService<IKeybindEvents>();

            MouseLeftButtonDown += Window_MouseLeftButtonDown;
            SizeChanged += EnsureBounds;
            IsVisibleChanged += EnsureBounds;
            Loaded += EnsureBounds;
            keybindEvents.OnCloseWindow += KeybindEvents_OnCloseWindow;
        }

        private Task<bool> KeybindEvents_OnCloseWindow()
        {
            keybindEvents.OnCloseWindow -= KeybindEvents_OnCloseWindow;
            MouseLeftButtonDown -= Window_MouseLeftButtonDown;
            IsVisibleChanged -= EnsureBounds;
            SizeChanged -= EnsureBounds;
            Loaded -= EnsureBounds;
            Close();
            return Task.FromResult(true);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                DragMove();
            }
            catch (InvalidOperationException) { }
        }

        public new void Show()
        {
            base.Show();
            EnsureBounds();
        }

        protected void SetWindowPositionFromBottomRight(double x, double y)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => SetWindowPositionFromBottomRight(x, y));
                return;
            }

            SetWindowPosition(x - Width, y - Height);
        }

        protected void SetWindowPosition(double x, double y)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => SetWindowPosition(x, y));
                return;
            }

            var screenRect = Screen.FromPoint(MyCursor.Position).Bounds;
            SetWindowPositionPercent(x / screenRect.Width, y / screenRect.Height);
        }

        protected void SetWindowPositionPercent(double x, double y)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => SetWindowPositionPercent(x, y));
                return;
            }

            if (x > 1) { x /= 100; }
            if (y > 1) { y /= 100; }

            var screenRect = Screen.FromPoint(MyCursor.Position).Bounds;

            var desiredX = screenRect.X + (screenRect.Width * x);
            var desiredY = screenRect.Y + (screenRect.Height * y);

            Left = (int)desiredX;
            Top = (int)desiredY;
            EnsureBounds();
        }

        protected void EnsureBounds()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => EnsureBounds());
                return;
            }

            if (IsVisible)
            {
                var screenRect = Screen.FromPoint(MyCursor.Position).Bounds;

                // Is off to the right
                if (Left + GetWidth() > screenRect.X + screenRect.Width)
                {
                    Left = screenRect.X + screenRect.Width - GetWidth();
                }

                // Is off to the left
                if (Left < screenRect.X)
                {
                    Left = screenRect.X;
                }

                // Is off to the top
                if (Top < screenRect.Y)
                {
                    Top = screenRect.Y;
                }

                // Is off to the bottom
                if (Top + GetHeight() > screenRect.Y + screenRect.Height)
                {
                    Top = screenRect.Y + screenRect.Height - GetHeight();
                }
            }
        }
        private void EnsureBounds(object sender, DependencyPropertyChangedEventArgs e) => EnsureBounds();
        private void EnsureBounds(object sender, EventArgs e) => EnsureBounds();

        protected double GetWidth()
        {
            if (!Dispatcher.CheckAccess())
            {
                return Dispatcher.Invoke(() => GetWidth());
            }

            return ActualWidth;
        }

        protected double GetHeight()
        {
            if (!Dispatcher.CheckAccess())
            {
                return Dispatcher.Invoke(() => GetHeight());
            }

            return ActualHeight;
        }
    }
}
