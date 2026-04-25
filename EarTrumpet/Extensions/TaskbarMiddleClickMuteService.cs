using EarTrumpet.Interop;
using EarTrumpet.Interop.Helpers;
using EarTrumpet.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Forms;
using System.Windows.Threading;

namespace EarTrumpet.Extensions
{
    public class TaskbarMiddleClickMuteService : IDisposable
    {
        private readonly MouseHook _mouseHook;
        private readonly DeviceCollectionViewModel _collectionViewModel;
        private readonly AppSettings _settings;
        private bool _disposed;

        public TaskbarMiddleClickMuteService(DeviceCollectionViewModel collectionViewModel, AppSettings settings)
        {
            _collectionViewModel = collectionViewModel;
            _settings = settings;
            _mouseHook = new MouseHook();
            _mouseHook.MiddleClickEvent += OnMiddleClick;
            _mouseHook.MiddleClickUpEvent += OnMiddleClickUp;
            _mouseHook.SetHook();
        }

        private int OnMiddleClick(object sender, MouseEventArgs e)
        {
            if (!_settings.UseTaskbarMiddleClickMute)
            {
                return 0;
            }

            try
            {
                if (!IsClickOnTaskbar(e.X, e.Y))
                {
                    return 0;
                }

                var candidates = GetTaskbarButtonCandidatesAtPoint(e.X, e.Y);
                if (candidates.Count == 0)
                {
                    return 0;
                }

                ScheduleToggleMute(candidates);
                return 1;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private int OnMiddleClickUp(object sender, MouseEventArgs e) => 0;

        private void ScheduleToggleMute(List<string> candidates)
        {
            if (System.Windows.Application.Current?.Dispatcher == null)
            {
                return;
            }

            var dispatcher = System.Windows.Application.Current.Dispatcher;
            var timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(100),
            };

            timer.Tick += (_, __) =>
            {
                timer.Stop();

                try
                {
                    ToggleMuteForApp(candidates);
                }
                catch (Exception)
                {
                }
            };

            timer.Start();
        }

        private bool IsClickOnTaskbar(int x, int y)
        {
            try
            {
                var taskbarState = WindowsTaskbar.Current;
                var point = new System.Drawing.Point(x, y);
                var rect = taskbarState.Size;
                var bounds = new System.Drawing.Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
                return bounds.Contains(point);
            }
            catch
            {
                return false;
            }
        }

        private List<string> GetTaskbarButtonCandidatesAtPoint(int x, int y)
        {
            var candidates = new List<string>();

            try
            {
                var element = TryFindTaskbarButtonElement(x, y);
                if (element != null)
                {
                    CollectCandidatesFromElement(candidates, element, TreeWalker.RawViewWalker);
                }
            }
            catch (Exception)
            {
            }

            if (candidates.Count == 0)
            {
                TryCollectCandidatesFromPoint(candidates, x, y);
            }

            return candidates;
        }

        private void TryCollectCandidatesFromPoint(List<string> candidates, int x, int y)
        {
            try
            {
                var point = new Point(x, y);
                var element = AutomationElement.FromPoint(point);
                if (element == null)
                {
                    return;
                }

                CollectCandidatesFromElement(candidates, element, TreeWalker.ControlViewWalker);
            }
            catch (Exception)
            {
            }
        }

        private AutomationElement TryFindTaskbarButtonElement(int x, int y)
        {
            var taskbar = AutomationElement.FromHandle(WindowsTaskbar.GetHwnd());
            if (taskbar == null)
            {
                return null;
            }

            var condition = new OrCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Custom));

            var elements = taskbar.FindAll(TreeScope.Descendants, condition);
            AutomationElement bestMatch = null;
            double bestArea = double.MaxValue;
            var point = new Point(x, y);

            foreach (AutomationElement element in elements)
            {
                var rect = element.Current.BoundingRectangle;
                if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0 || !rect.Contains(point))
                {
                    continue;
                }

                var area = rect.Width * rect.Height;
                if (area < bestArea)
                {
                    bestArea = area;
                    bestMatch = element;
                }
            }

            return bestMatch;
        }

        private void CollectCandidatesFromElement(List<string> candidates, AutomationElement element, TreeWalker walker)
        {
            var current = element;
            const int maxDepth = 10;
            int depth = 0;

            while (current != null && depth < maxDepth)
            {
                TryAddCandidate(candidates, current.Current.Name);
                TryAddCandidate(candidates, current.Current.AutomationId);
                TryAddCandidate(candidates, current.Current.HelpText);
                TryAddCandidate(candidates, current.Current.ItemStatus);

                try
                {
                    current = walker.GetParent(current);
                    depth++;
                }
                catch
                {
                    break;
                }
            }
        }

        private void TryAddCandidate(List<string> candidates, string rawValue)
        {
            foreach (var candidate in ExpandCandidates(rawValue))
            {
                if (!candidates.Any(c => string.Equals(c, candidate, StringComparison.OrdinalIgnoreCase)))
                {
                    candidates.Add(candidate);
                }
            }
        }

        private IEnumerable<string> ExpandCandidates(string rawValue)
        {
            var cleanValue = CleanAppName(rawValue);
            if (string.IsNullOrWhiteSpace(cleanValue))
            {
                yield break;
            }

            yield return cleanValue;

            foreach (var separator in new[] { " - ", " \u2013 ", " \u2014 " })
            {
                if (!cleanValue.Contains(separator))
                {
                    continue;
                }

                foreach (var part in cleanValue.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p)))
                {
                    yield return part;
                }

                var rightMost = cleanValue.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .LastOrDefault();
                if (!string.IsNullOrWhiteSpace(rightMost))
                {
                    yield return rightMost;
                }
            }
        }

        private string CleanAppName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var cleanName = name.Trim();

            if (string.Equals(cleanName, "Taskbar", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cleanName, "Running applications", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cleanName, "System tray", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cleanName, "Notification area", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            cleanName = System.Text.RegularExpressions.Regex.Replace(cleanName, @"\s*-\s*\d+\s*.*$", "");
            cleanName = System.Text.RegularExpressions.Regex.Replace(cleanName, @"\s*\(\d+\)\s*$", "");

            return cleanName.Trim();
        }

        private bool ToggleMuteForApp(List<string> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return false;
            }

            foreach (var device in _collectionViewModel.AllDevices)
            {
                foreach (var app in device.Apps)
                {
                    if (MatchesAnyCandidate(app, candidates))
                    {
                        app.IsMuted = !app.IsMuted;
                        return true;
                    }
                }
            }

            return false;
        }

        private bool MatchesAnyCandidate(IAppItemViewModel app, List<string> candidates)
        {
            var displayName = app.DisplayName?.ToLowerInvariant() ?? "";
            var exeName = (app.ExeName ?? "").ToLowerInvariant().Replace(".exe", "");
            var appId = (app.AppId ?? "").ToLowerInvariant();

            foreach (var candidate in candidates.Select(c => c.ToLowerInvariant()))
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                if (displayName.Contains(candidate) ||
                    candidate.Contains(displayName) ||
                    exeName.Contains(candidate) ||
                    candidate.Contains(exeName) ||
                    appId.Contains(candidate) ||
                    candidate.Contains(appId))
                {
                    return true;
                }
            }

            if (app.ChildApps != null)
            {
                foreach (var childApp in app.ChildApps)
                {
                    if (MatchesAnyCandidate(childApp, candidates))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _mouseHook.MiddleClickEvent -= OnMiddleClick;
                    _mouseHook.MiddleClickUpEvent -= OnMiddleClickUp;
                    _mouseHook.UnHook();
                }

                _disposed = true;
            }
        }

        ~TaskbarMiddleClickMuteService()
        {
            Dispose(false);
        }
    }
}
