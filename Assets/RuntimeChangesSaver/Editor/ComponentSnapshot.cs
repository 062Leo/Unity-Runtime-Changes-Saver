using System;
using System.Collections.Generic;

namespace RuntimeChangesSaver.Editor
{
    [Serializable]
    public class ComponentSnapshot
    {
        public string componentType; 
        public Dictionary<string, object> properties = new Dictionary<string, object>();
    }
}
