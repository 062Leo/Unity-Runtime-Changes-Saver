using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;



[Serializable]
public class ComponentSnapshot
{
    public string componentType;
    public Dictionary<string, object> properties = new Dictionary<string, object>();
}
