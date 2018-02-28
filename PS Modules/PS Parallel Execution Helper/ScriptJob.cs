using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace ParallelExecutionHelper
{
    internal class ScriptJob
    {
        public object ID { get; private set; }
        public string ScriptBlock { get; private set; }
        public IEnumerable<object> Args { get; private set; }

        public PowerShell Pipe { get; private set; }
        public IAsyncResult AsyncResult { get; private set; }

        public bool IsResultAvailable { get { return ((!this.IsResultProcessed) && (this.AsyncResult != null) && (this.AsyncResult.IsCompleted)); } }
        public bool IsResultProcessed { get; private set; }

        /// <summary>
        /// Begin Invocation of a new script job. You can get the result using the GetResult() method. Use the IsResultAvailable property to check if the invocation has completed.
        /// Make sure to call GetResult() or Stop(reason) before exiting to avoid memory leaks.
        /// </summary>
        /// <param name="id">An ID object that you can later use to identify the job in a list.</param>
        /// <param name="runspacePool">The Runspace Pool where this job is to be invoked.</param>
        /// <param name="scriptBlock">The string representation of the ScriptBlock that should be invoked.</param>
        /// <param name="args">The arguments that should be sent to the ScriptBlock.</param>
        /// <param name="useLocalScope">Whether the script should be created in the local PowerShell scope.</param>
        public static ScriptJob StartNew(object id, RunspacePool runspacePool, string scriptBlock, IEnumerable<object> args = null, bool useLocalScope = false)
        {
            return new ScriptJob(id, runspacePool, scriptBlock, args, useLocalScope);
        }

        private ScriptJob(object id, RunspacePool runspacePool, string scriptBlock, IEnumerable<object> args = null, bool useLocalScope = false)
        {
            if (runspacePool == null) { throw new ArgumentNullException("RunspacePool"); }
            if (String.IsNullOrEmpty(scriptBlock)) { throw new ArgumentNullException("ScriptBlock"); }

            this.ID = id;
            this.ScriptBlock = scriptBlock;
            this.Args = args;

            this.Pipe = PowerShell.Create().AddScript(this.ScriptBlock, useLocalScope);

            if(this.Args != null)
            {
                foreach (var arg in this.Args)
                {
                    this.Pipe = this.Pipe.AddArgument(arg);
                }
            }
            this.Pipe.RunspacePool = runspacePool;
            this.AsyncResult = this.Pipe.BeginInvoke();
        }

        public ScriptJobResult GetResult()
        {
            if (!this.AsyncResult.IsCompleted) { throw new InvalidOperationException("Cannot get result because the operation has not completed yet."); }
            
            var result = new ScriptJobResult(this.ID, this.ScriptBlock, this.Args);

            try
            {
                result.Result = (this.Pipe.EndInvoke(this.AsyncResult));
            }
            catch (Exception ex)
            {
                result.Error = ex;
            }

            // Dispose
            Pipe.Dispose();
            AsyncResult = null;

            this.IsResultProcessed = true;
            return result;
        }

        public ScriptJobResult Stop(string reason)
        {
            if (this.AsyncResult.IsCompleted)
            {
                return this.GetResult();
            }

            var result = new ScriptJobResult(this.ID, this.ScriptBlock, this.Args);

            try
            {
                this.Pipe.Stop();
                result.Error = reason;
            }
            catch (Exception ex)
            {
                result.Error = (ex.Message + reason);
            }

            // Dispose
            Pipe.Dispose();
            AsyncResult = null;

            this.IsResultProcessed = true;
            return result;
        }
    }
}
