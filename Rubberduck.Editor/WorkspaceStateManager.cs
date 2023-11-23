﻿using Microsoft.Extensions.Logging;
using Rubberduck.InternalApi.Model;
using Rubberduck.SettingsProvider;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace Rubberduck.Editor
{
    public class WorkspaceStateManager : ServiceBase
    {
        private readonly ConcurrentDictionary<Uri, ConcurrentQueue<WorkspaceFileInfo>> _workspaceFiles = [];

        public WorkspaceStateManager(ILogger<WorkspaceStateManager> logger, 
            RubberduckSettingsProvider settings, PerformanceRecordAggregator performance)
            : base(logger, settings, performance)
        {
        }

        public bool TryGetWorkspaceFile(Uri uri, out WorkspaceFileInfo? fileInfo)
        {
            if (_workspaceFiles.TryGetValue(uri, out var cache))
            {
                return cache.TryPeek(out fileInfo);
            }

            fileInfo = default;
            return false;
        }

        public bool TryGetWorkspaceFile(Uri uri, int version, out WorkspaceFileInfo? fileInfo)
        {
            if (_workspaceFiles.TryGetValue(uri, out var cache))
            {
                fileInfo = cache.SingleOrDefault(e => e.Version == version);
                return fileInfo != null;
            }

            fileInfo = default;
            return false;
        }

        public bool CloseWorkspaceFile(Uri uri, out WorkspaceFileInfo? fileInfo)
        {
            if (_workspaceFiles.TryGetValue(uri, out var cache)
                && cache.TryPeek(out fileInfo))
            {
                if (!fileInfo.IsOpened)
                {
                    fileInfo.IsOpened = false;
                    return true;
                }
            }

            fileInfo = default;
            return false;
        }

        public bool LoadWorkspaceFile(WorkspaceFileInfo file, int cacheCapacity = 3)
        {
            if (!_workspaceFiles.TryGetValue(file.Uri, out var existingCache))
            {
                var queue = new ConcurrentQueue<WorkspaceFileInfo>();
                queue.Enqueue(file);
                _workspaceFiles[file.Uri] = queue;
                return true;
            }
            else if (existingCache.TryPeek(out var existing))
            {
                if (file.Version > existing.Version)
                {
                    if (file.Content != existing.Content)
                    {
                        var cached = _workspaceFiles[file.Uri];
                        while (cached.Count >= cacheCapacity)
                        {
                            cached.TryDequeue(out _);
                        }
                        cached.Enqueue(file);
                        return true;
                    }
                    // else: same content, skip this version.
                }
                // else: old version, skip.
            }
            else
            {
                // invalid but recoverable state: no content in cache but URI is legitimate
                existingCache.Enqueue(file);
            }
            return false;
        }

        public bool RenameWorkspaceFile(Uri oldUri, Uri newUri)
        {
            if (_workspaceFiles.TryGetValue(newUri, out var existingCache))
            {
                // new URI already exists... TODO check for a name collision
                return false;
            }

            if (_workspaceFiles.TryGetValue(oldUri, out var oldCache) && oldCache.TryPeek(out var oldFileInfo))
            {
                // keep the old cache key around but point it to the updated uri as a newer version
                var version = oldFileInfo.Version + 1;
                var newFileInfo = oldFileInfo with { Uri = newUri, Version = version };
                oldCache.Enqueue(newFileInfo);

                var newCache = new ConcurrentQueue<WorkspaceFileInfo>([newFileInfo]);
                _workspaceFiles[newUri] = newCache;
            }

            return false;
        }

        public bool UnloadWorkspaceFile(Uri uri)
        {
            if (_workspaceFiles.TryGetValue(uri, out var cache))
            {
                cache.Clear();
                return _workspaceFiles.TryRemove(uri, out _);
            }

            return false;
        }

        public void UnloadWorkspace()
        {
            _workspaceFiles.Clear();
            // should this force a GC? does it matter?
        }
    }
}