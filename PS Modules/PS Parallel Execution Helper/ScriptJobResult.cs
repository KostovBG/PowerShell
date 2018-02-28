using System;
using System.Collections.Generic;
using System.Management.Automation;

namespace ParallelExecutionHelper
{
    public class ScriptJobResult
    {
        public object ID { get; set; }
        public string Script { get; set; }
        public IEnumerable<object> Args { get; set; }
        public PSDataCollection<PSObject> Result { get; set; }
        public object Error { get; set; }

        public ScriptJobResult(object id, string script, IEnumerable<object> args)
        {
            this.ID = id;
            this.Script = script;
            this.Args = args;
        }
    }

}
