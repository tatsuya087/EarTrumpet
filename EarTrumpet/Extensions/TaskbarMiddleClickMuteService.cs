using EarTrumpet.Interop.Helpers;
using EarTrumpet.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Automation;
using System.Windows.Forms;

namespace EarTrumpet.Extensions
{
    public class TaskbarMiddleClickMuteService : IDisposable
    {
        private readonly MouseHook _mouseHook;
        private readonly DeviceCollectionViewModel _collectionViewModel;
        private readonly AppSettings _settings;
        private bool _suppressNextMiddleClickUp;
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

                var candidates = GetTaskbarButtonCandidates(e.X, e.Y);
                if (candidates.Count == 0)
                {
                    return 0;
                }

                if (ToggleMuteForApp(candidates))
                {
                    _suppressNextMiddleClickUp = true;
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"TaskbarMiddleClickMuteService OnMiddleClick error: {ex.Message}");
            }

            return 0;
        }

        private int OnMiddleClickUp(object sender, MouseEventArgs e)
        {
            if (_suppressNextMiddleClickUp)
            {
                _suppressNextMiddleClickUp = false;
                return 1;
            }

            return 0;
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

        private List<string> GetTaskbarButtonCandidates(int x, int y)
        {
            var candidates = new List<string>();

            try
            {
                var point = new System.Windows.Point(x, y);
                var element = AutomationElement.FromPoint(point);

                if (element != null)
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
                            current = TreeWalker.ControlViewWalker.GetParent(current);
                            depth++;
                        }
                        catch
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"TaskbarMiddleClickMuteService GetTaskbarButtonCandidates error: {ex.Message}");
            }

            if (candidates.Count == 0)
            {
                TryCollectCandidatesFromTaskbarTree(candidates, x, y);
            }

            return candidates;
        }

        private void TryCollectCandidatesFromTaskbarTree(List<string> candidates, int x, int y)
        {
            try
            {
                var taskbar = AutomationElement.FromHandle(WindowsTaskbar.GetHwnd());
                if (taskbar == null)
                {
                    return;
                }

                var condition = new OrCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Custom));

                var elements = taskbar.FindAll(TreeScope.Descendants, condition);
                AutomationElement bestMatch = null;
                double bestArea = double.MaxValue;
                var point = new System.Windows.Point(x, y);

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

                if (bestMatch == null)
                {
                    return;
                }

                var current = bestMatch;
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
                        current = TreeWalker.RawViewWalker.GetParent(current);
                        depth++;
                    }
                    catch
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"TaskbarMiddleClickMuteService fallback tree search error: {ex.Message}");
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
