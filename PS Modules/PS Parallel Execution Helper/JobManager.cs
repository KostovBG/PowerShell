using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace ParallelExecutionHelper
{
    public static class JobManager
    {
        /// <summary>
        /// Invokes multiple instances of a script in parallel.
        /// </summary>
        public static IEnumerable<ScriptJobResult> Invoke(IEnumerable<ScriptBlock> scriptBlock, IEnumerable<object> inputParams, int maxThreads, bool useLocalScope, InvocationMode invocationMode, TimeSpan? maxExecutionTime = null)
        {
            if (scriptBlock == null) { throw new ArgumentNullException("ScriptBlock"); }
            if (scriptBlock.Count() == 0) { throw new ArgumentOutOfRangeException("ScriptBlock", "The ScriptBlock Array does not contain any data."); }
            if (inputParams == null) { throw new ArgumentNullException("InputParams"); }
            if (inputParams.Count() == 0) { throw new ArgumentOutOfRangeException("InputParams", "The InputParams Array does not contain any data."); }
            if ((maxThreads < 1) || (maxThreads > 64)) { throw new ArgumentOutOfRangeException("MaxThreads", "The MaxThreads value can be between 1 and 64."); }
            if ((invocationMode == InvocationMode.MatchScriptToArgumentByIndex) && (scriptBlock.Count() != inputParams.Count()))
            {
                throw new ArgumentException("When InvocationMode is MatchScriptToArgumentByIndex both ScriptBlock and InputParams arrays must be of the same length.");
            }

            var result = new List<ScriptJobResult>(inputParams.Count());
            using (var runspacePool = RunspaceFactory.CreateRunspacePool(1, maxThreads))
            {
                runspacePool.Open();

                // START JOBS
                var JobsList = StartScriptJobs(scriptBlock, inputParams, invocationMode, runspacePool, useLocalScope);

                // WAIT FOR RESULTS
                var stopWatch = System.Diagnostics.Stopwatch.StartNew();
                while (JobsList.Any(x => !x.IsResultProcessed))
                {
                    for (int i = 0; i < JobsList.Length; i++)
                    {
                        // Get the results from all jobs that have completed (successfully or not)
                        if (JobsList[i].IsResultAvailable)
                        {
                            result.Add(JobsList[i].GetResult());
                        }
                    }

                    if (JobsList.Any(x => !x.IsResultProcessed))
                    {
                        // Check if a timeout period has been reached
                        if (maxExecutionTime.HasValue && (stopWatch.ElapsedTicks > maxExecutionTime.Value.Ticks))
                        {
                            // Operation Timed Out
                            for (int i = 0; i < JobsList.Length; i++)
                            {
                                // Stop all jobs that have not yet completed
                                if (!JobsList[i].IsResultProcessed)
                                {
                                    result.Add(JobsList[i].Stop($"The operation timed out. The maximum execution time of {TimeSpanToString(maxExecutionTime.Value)} has been reached."));
                                }
                            }
                        }
                        else
                        {
                            System.Threading.Thread.Sleep(117);
                        }
                    }
                }
                stopWatch.Stop();
            }

            return result;
        }


        internal static ScriptJob[] StartScriptJobs(IEnumerable<ScriptBlock> scriptBlock, IEnumerable<object> inputParams, InvocationMode invocationMode, RunspacePool runspacePool, bool useLocalScope)
        {
            // These parameters should already be validated in the caller method, but we check them here again just in case
            if (scriptBlock == null) { throw new ArgumentNullException("ScriptBlock"); }
            if (scriptBlock.Count() == 0) { throw new ArgumentOutOfRangeException("ScriptBlock", "The ScriptBlock Array does not contain any data."); }
            if (inputParams == null) { throw new ArgumentNullException("InputParams"); }
            if (inputParams.Count() == 0) { throw new ArgumentOutOfRangeException("InputParams", "The InputParams Array does not contain any data."); }
            if ((invocationMode == InvocationMode.MatchScriptToArgumentByIndex) && (scriptBlock.Count() != inputParams.Count()))
            {
                throw new ArgumentException("When InvocationMode is MatchScriptToArgumentByIndex both ScriptBlock and InputParams arrays must be of the same length.");
            }

            ScriptJob[] JobsList = null;

            if (invocationMode == InvocationMode.AllArgumentsToEachScriptBlock)
            {
                JobsList = new ScriptJob[scriptBlock.Count()];

                var sIdx = 0;
                foreach (var script in scriptBlock)
                {
                    JobsList[sIdx] = ScriptJob.StartNew(sIdx, runspacePool, script.ToString(), inputParams, useLocalScope);
                    sIdx++;
                }
            }
            else if (invocationMode == InvocationMode.OneArgumentToEachScriptBlock)
            {
                JobsList = new ScriptJob[(scriptBlock.Count() * inputParams.Count())];
                var id = 0;
                foreach (var script in scriptBlock)
                {
                    foreach (var arg in inputParams)
                    {
                        JobsList[id] = ScriptJob.StartNew(id, runspacePool, script.ToString(), new object[] { arg }, useLocalScope);
                        id++;
                    }
                }
            }
            else if (invocationMode == InvocationMode.MatchScriptToArgumentByIndex)
            {
                JobsList = new ScriptJob[scriptBlock.Count()];
                var paramArray = inputParams.ToArray();

                var spIdx = 0;
                foreach (var script in scriptBlock)
                {
                    JobsList[spIdx] = ScriptJob.StartNew(spIdx, runspacePool, script.ToString(), new object[] { paramArray[spIdx] }, useLocalScope);
                    spIdx++;
                }
            }
            else
            {
                throw new ArgumentException("Unknown InvocationMode.");
            }

            return JobsList;
        }

        internal static string TimeSpanToString(TimeSpan ts)
        {
            var maxETstr = "";
            if (ts.TotalHours > 1)
            {
                if (Math.Floor(ts.TotalHours) == ts.TotalHours)
                {
                    maxETstr = $"{ts.TotalHours} hours";
                }
                else
                {
                    maxETstr = $"{Math.Floor(ts.TotalHours)} hours and {ts.Minutes} minutes";
                }
            }
            else if (ts.TotalMinutes > 1)
            {
                maxETstr = $"{Math.Floor(ts.TotalMinutes)} minutes";
            }
            else if (ts.TotalSeconds > 1)
            {
                maxETstr = $"{Math.Floor(ts.TotalSeconds)} seconds";
            }
            else
            {
                maxETstr = $"{Math.Floor(ts.TotalMilliseconds)} milliseconds";
            }

            return maxETstr;
        }
    }
    public enum InvocationMode
    {
        /// <summary>
        /// If multiple input arguments are provided, each scriptblock will receive each of the arguments.
        /// </summary>
        AllArgumentsToEachScriptBlock,

        /// <summary>
        /// If multiple input arguments are provided, each scriptblock will receive only one of the arguments.
        /// </summary>
        OneArgumentToEachScriptBlock,

        /// <summary>
        /// If multiple scripts and input parameters are provided, they will be matched by index:
        /// ScriptBlock 1 receives Arg 1,
        /// ScriptBlock 2 receives Arg 2, etc
        /// </summary>
        MatchScriptToArgumentByIndex
    }
}
