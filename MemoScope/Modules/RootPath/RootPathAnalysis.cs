using MemoScope.Core;
using MemoScope.Core.Data;
using MemoScope.Core.Helpers;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WinFwk.UIMessages;
using WinFwk.UIModules;

namespace MemoScope.Modules.RootPath
{
    public static class RootPathAnalysis
    {
        static Logger logger = LogManager.GetLogger(typeof(RootPathAnalysis).FullName);

        public static List<RootPathInformation> AnalyzeRootPath(MessageBus msgBus, ClrDumpObject clrDumpObject)
        {
            ClrDump clrDump = clrDumpObject.ClrDump;
            ulong address = clrDumpObject.Address;
            CancellationTokenSource token = new CancellationTokenSource();
            msgBus.BeginTask("Analysing Root Path...", token);
            if( token.IsCancellationRequested)
            {
                msgBus.EndTask("Root Path analysis: cancelled.");
                return null;
            }

            msgBus.Status("Analysing Root Path: collecting root instances...");
            var roots = new HashSet<ulong>(clrDump.EnumerateClrRoots.Select(clrRoot => clrRoot.Object));
            if (logger.IsDebugEnabled)
            {
                logger.Debug("Roots: " + Str(roots));
            }
            List<ulong> bestPath = null;
            var currentPath = new List<ulong>();
            bool result = FindShortestPath(address, currentPath, ref bestPath, clrDump.GetReferers, roots.Contains, new Dictionary<ulong,List<ulong>>());
            if (!result)
            {
                currentPath = new List<ulong>();
                FindShortestPath(address, currentPath, ref bestPath, clrDump.GetReferers, (addr) => !clrDump.HasReferers(addr), new Dictionary<ulong, List<ulong>>());
            }
            List<RootPathInformation> path = new List<RootPathInformation>();
            ulong prevAddress = address;
            if (bestPath != null)
            {
                foreach (var refAddress in bestPath)
                {
                    var refClrDumpObject = new ClrDumpObject(clrDump, clrDump.GetObjectType(refAddress), refAddress);
                    var fieldName = refAddress == address ? " - " : clrDump.GetFieldNameReference(prevAddress, refAddress);
                    fieldName = TypeHelpers.RealName(fieldName);
                    path.Add(new RootPathInformation(refClrDumpObject, fieldName));
                    prevAddress = refAddress;
                }
                msgBus.EndTask("Root Path found.");
            }
            else
            {
                msgBus.EndTask("Root Path NOT found.");
            }
            return path;
        }

        public static bool FindShortestPath(ulong myaddress, List<ulong> currentPath, ref List<ulong> bestPath, Func<ulong, IEnumerable<ulong>> getReferers, Func<ulong, bool> isroot, IDictionary<ulong,List<ulong>> shortest_paths)
        {
            //if(logger.IsDebugEnabled) logger.Debug("FindShortestPath: currentPath: " + Str(currentPath)+", best: "+Str(bestPath));
            if( bestPath != null && currentPath.Count >= bestPath.Count - 1)
            {
                return false;
            }
            bool res = false;
            currentPath.Add(myaddress);
            foreach (var refAddress in getReferers(myaddress))
            {
                if(currentPath.Contains(refAddress))
                {
                    continue; // cycle detected 
                }
                //if (logger.IsDebugEnabled) logger.Debug($"Visiting: {refAddress:X}");
                if (shortest_paths.TryGetValue(refAddress, out var path))
                {
                    if (path == null) continue; // no path to root ???
                    if (path.Count + currentPath.Count < bestPath.Count)
                    {
                        bestPath = new List<ulong>(currentPath);
                        bestPath.AddRange(path);
                    }
                    continue;
                }
                if (isroot(refAddress))
                {
                    bestPath = new List<ulong>(currentPath);
                    currentPath.Add(refAddress);
                    if (logger.IsDebugEnabled) logger.Debug("Root found !, best path: "+Str(bestPath));
                    return true;
                }

                res |= FindShortestPath(refAddress, currentPath, ref bestPath, getReferers, isroot, shortest_paths);
            }
            currentPath.RemoveAt(currentPath.Count - 1);

            List<ulong> mybestpath = null;
            if (bestPath!=null)
            {
                mybestpath = new List<ulong>(bestPath.Skip(currentPath.Count));
            }
            shortest_paths[myaddress] = mybestpath;
            return res;
        }

        private static string Str(IEnumerable<ulong> ulongEnum)
        {
            if ( ulongEnum == null || ! ulongEnum.Any()) 
            {
                return "[]";
            }
            var s = "[";
            int n = 0;
            foreach(var u in ulongEnum)
            {
                s += u.ToString("X") + ", ";
                if( ++n % 128 == 0)
                {
                    s += "..., ";
                    break;
                }
            }
            return s.Substring(0, s.Length-2)+"]";
        }
    }
}
