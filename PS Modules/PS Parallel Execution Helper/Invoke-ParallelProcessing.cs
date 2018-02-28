using System;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace ParallelExecutionHelper
{
    [Cmdlet("Invoke", "ParallelProcessing")]
    [OutputType(typeof(ScriptJobResult))]
    public class Invoke_ParallelProcessing : PSCmdlet
    {
        #region PARAM
        // ScriptBlock
        [Parameter(Position = 0, Mandatory = true)]
        public ScriptBlock[] ScriptBlock { get; set; }

        // InputObject
        [Parameter(Position = 1, Mandatory = true)]
        public object[] ArgumentList { get; set; }

        // MaxThreads
        [Parameter(Mandatory = false)]
        [ValidateRange(1, 64)]
        public int MaxThreads { get; set; } = 4;

        // UseLocalScope
        [Parameter(Mandatory = false)]
        public SwitchParameter UseLocalScope { get; set; }

        // InvocationMode
        [Parameter(Mandatory = false)]
        public InvocationMode InvocationMode { get; set; } = InvocationMode.OneArgumentToEachScriptBlock;

        // MaxExecutionTime
        [Parameter(Mandatory = false)]
        public TimeSpan? MaxExecutionTime { get; set; }

        #endregion

        // BEGIN
        protected override void BeginProcessing()
        {
            base.BeginProcessing();
        }

        // PROCESS
        protected override void ProcessRecord()
        {
            try
            {
                if (this.ScriptBlock == null) { throw new ArgumentNullException("ScriptBlock"); }
                if (this.ScriptBlock.Length == 0) { throw new ArgumentOutOfRangeException("ScriptBlock", "The ScriptBlock Array does not contain any data."); }
                if (this.ArgumentList == null) { throw new ArgumentNullException("ArgumentList"); }
                if (this.ArgumentList.Length == 0) { throw new ArgumentOutOfRangeException("ArgumentList", "The ArgumentList does not contain any data."); }
                if ((this.MaxThreads < 1) || (this.MaxThreads > 64)) { throw new ArgumentOutOfRangeException("MaxThreads", "The MaxThreads value can be between 1 and 64."); }
                if ((this.InvocationMode == InvocationMode.MatchScriptToArgumentByIndex) && (this.ScriptBlock.Length != this.ArgumentList.Length))
                {
                    throw new ArgumentException("When InvocationMode is MatchScriptToArgumentByIndex both ScriptBlock and ArgumentList arrays must be of the same length.");
                }

                WriteVerbose("Opening a Runspace pool");
                using (var runspacePool = RunspaceFactory.CreateRunspacePool(1, this.MaxThreads))
                {
                    runspacePool.Open();

                    // START JOBS
                    var JobsList = ParallelExecutionHelper.JobManager.StartScriptJobs(this.ScriptBlock, this.ArgumentList, this.InvocationMode, runspacePool, this.UseLocalScope.IsPresent);

                    // WAIT FOR RESULTS
                    WriteVerbose("Waiting for async jobs to complete");
                    double completed = 0;
                    var prog = new ProgressRecord(0, "Running ScriptBlock in Parallel Instances", "Waiting for results");
                    prog.PercentComplete = 0;
                    WriteProgress(prog);

                    var stopWatch = System.Diagnostics.Stopwatch.StartNew();
                    while (JobsList.Any(x => !x.IsResultProcessed))
                    {
                        for (int i = 0; i < JobsList.Length; i++)
                        {
                            // Get the results from all jobs that have completed (successfully or not)
                            if (JobsList[i].IsResultAvailable)
                            {
                                // Update Progress
                                completed += 1;
                                prog.PercentComplete = (Math.Min(100, (int)((completed / JobsList.Length) * 100)));
                                prog.CurrentOperation = String.Format("{0}/{1} completed", completed, JobsList.Length);
                                WriteProgress(prog);

                                // Output Result
                                this.WriteObject(JobsList[i].GetResult());
                            }
                        }

                        if (JobsList.Any(x => !x.IsResultProcessed))
                        {
                            // Check if a timeout period has been reached
                            if (this.MaxExecutionTime.HasValue && (stopWatch.ElapsedTicks > this.MaxExecutionTime.Value.Ticks))
                            {
                                // Operation Timed Out
                                for (int i = 0; i < JobsList.Length; i++)
                                {
                                    // Stop all jobs that have not yet completed
                                    if (!JobsList[i].IsResultProcessed)
                                    {
                                        // Output Result
                                        this.WriteObject(JobsList[i].Stop($"The operation timed out. The maximum execution time of {ParallelExecutionHelper.JobManager.TimeSpanToString(this.MaxExecutionTime.Value)} has been reached."));
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

                    prog.PercentComplete = 100;
                    WriteProgress(prog);
                }
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex, "0", ErrorCategory.NotSpecified, this.MyInvocation.MyCommand.Name));
            }
        }

        // END
        protected override void EndProcessing()
        {
            base.EndProcessing();
        }
    }
}
