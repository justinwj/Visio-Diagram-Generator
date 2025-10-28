using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.CSharp.RuntimeBinder;
using VisioDiagramGenerator.Algorithms;

namespace VDG.CLI
{
    /// <summary>
    /// Tracks Visio pages created during rendering and aggregates simple metrics for diagnostics.
    /// </summary>
    internal sealed class VisioPageManager
    {
        private readonly dynamic _pages;
        private readonly Dictionary<int, PageInfo> _pageLookup = new Dictionary<int, PageInfo>();
        private readonly List<PageInfo> _orderedPages = new List<PageInfo>();
        private readonly List<SkippedConnectorInfo> _skippedConnectors = new List<SkippedConnectorInfo>();
        private readonly List<PagePlan> _pagePlans = new List<PagePlan>();

        public VisioPageManager(dynamic pages)
        {
            _pages = pages ?? throw new ArgumentNullException(nameof(pages));
        }

        public IReadOnlyList<PageInfo> Pages => _orderedPages;

        public IReadOnlyList<SkippedConnectorInfo> SkippedConnectors => _skippedConnectors;

        public IReadOnlyList<PagePlan> PagePlans => _pagePlans;

        public void SetPagePlans(IEnumerable<PagePlan>? plans)
        {
            _pagePlans.Clear();
            if (plans == null)
            {
                return;
            }

            foreach (var plan in plans)
            {
                if (plan != null)
                {
                    _pagePlans.Add(plan);
                }
            }
        }

        public bool TryGetPageInfo(int index, out PageInfo pageInfo)
        {
            return _pageLookup.TryGetValue(index, out pageInfo!);
        }

        public PageInfo EnsurePage(int index, string? name = null)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (_pageLookup.TryGetValue(index, out var existing))
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    TryRename(existing.Page, name!);
                }

                return existing;
            }

            var page = GetOrCreatePage(index);
            if (page == null)
            {
                throw new COMException($"Failed to obtain Visio page at index {index}.");
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                TryRename(page, name!);
            }

            var info = new PageInfo(index, page);
            _pageLookup[index] = info;
            InsertOrdered(info);
            return info;
        }

        public void RegisterConnectorSkipped(string? connectorId, string? sourceId, string? targetId, int pageIndex, string? reason)
        {
            if (pageIndex < 0)
            {
                pageIndex = 0;
            }

            if (!_pageLookup.TryGetValue(pageIndex, out var pageInfo))
            {
                pageInfo = EnsurePage(pageIndex);
            }

            pageInfo.IncrementSkippedConnectorCount();

            var record = new SkippedConnectorInfo(
                connectorId ?? string.Empty,
                sourceId ?? string.Empty,
                targetId ?? string.Empty,
                pageIndex,
                reason ?? string.Empty);

            _skippedConnectors.Add(record);
        }

        private void InsertOrdered(PageInfo info)
        {
            var position = _orderedPages.FindIndex(p => p.Index > info.Index);
            if (position >= 0)
            {
                _orderedPages.Insert(position, info);
            }
            else
            {
                _orderedPages.Add(info);
            }
        }

        private dynamic? GetOrCreatePage(int index)
        {
            var pages = _pages;
            if (pages == null)
            {
                throw new COMException("Visio pages collection is not available.");
            }

            var count = GetPageCount(pages);
            dynamic? lastPage = null;

            if (index < count)
            {
                lastPage = TryGetPageByIndex(pages, index + 1);
            }
            else
            {
                while (count <= index)
                {
                    lastPage = AddPage(pages);
                    count++;
                }
            }

            return lastPage;
        }

        private static int GetPageCount(dynamic pages)
        {
            try
            {
                var countObj = pages.Count;
                if (countObj is int i)
                {
                    return i;
                }
                if (countObj is short s)
                {
                    return s;
                }
                if (countObj is double d)
                {
                    return Convert.ToInt32(Math.Round(d, MidpointRounding.AwayFromZero), CultureInfo.InvariantCulture);
                }
                return Convert.ToInt32(countObj, CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
            }
        }

        private static dynamic? AddPage(dynamic pages)
        {
            try
            {
                return pages.Add();
            }
            catch (RuntimeBinderException)
            {
                return pages.Add();
            }
        }

        private static dynamic? TryGetPageByIndex(dynamic pages, int comIndex)
        {
            try
            {
                return pages[comIndex];
            }
            catch (RuntimeBinderException)
            {
                try
                {
                    return pages.Item[comIndex];
                }
                catch (RuntimeBinderException)
                {
                    try
                    {
                        return pages.Item(comIndex);
                    }
                    catch (RuntimeBinderException)
                    {
                        try
                        {
                            return pages.get_Item(comIndex);
                        }
                        catch (RuntimeBinderException)
                        {
                            try
                            {
                                return ((object)pages).GetType().InvokeMember(
                                    "Item",
                                    BindingFlags.GetProperty,
                                    binder: null,
                                    target: pages,
                                    args: new object[] { comIndex });
                            }
                            catch
                            {
                                return null;
                            }
                        }
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private static void TryRename(dynamic page, string name)
        {
            if (page is null || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            try { page.Name = name; }
            catch { /* ignore */ }

            try { page.NameU = name; }
            catch { /* ignore */ }
        }

        internal sealed class PageInfo
        {
            internal PageInfo(int index, dynamic page)
            {
                Index = index;
                Page = page;
            }

            public int Index { get; }

            public dynamic Page { get; }

            public int NodeCount { get; private set; }

            public int ConnectorCount { get; private set; }

            public int SkippedConnectorCount { get; private set; }

            public bool HasOverflow { get; private set; }

            internal Dictionary<int, dynamic> LayerObjects { get; } = new Dictionary<int, dynamic>();

            public void IncrementNodeCount() => NodeCount++;

            public void IncrementConnectorCount() => ConnectorCount++;

            internal void IncrementSkippedConnectorCount() => SkippedConnectorCount++;

            public void MarkOverflow() => HasOverflow = true;
        }

        internal sealed class SkippedConnectorInfo
        {
            public SkippedConnectorInfo(string connectorId, string sourceId, string targetId, int pageIndex, string reason)
            {
                ConnectorId = connectorId;
                SourceId = sourceId;
                TargetId = targetId;
                PageIndex = pageIndex;
                Reason = reason;
            }

            public string ConnectorId { get; }

            public string SourceId { get; }

            public string TargetId { get; }

            public int PageIndex { get; }

            public string Reason { get; }
        }
    }
}
