using System;
using UnityEngine;

[PreferBinarySerialization]
public sealed class ExecutionGeometry : ScriptableObject
{
    [Serializable]
    public sealed class Section
    {
        public Mesh original;
        public Mesh retained;
        public Mesh separated;
        public Material[] cutMaterials;
        public int[] retainedJoints;
        public int[] separatedJoints;
    }

    public Section[] leon;
    public Section[] head;
    public Section[] leftArm;
    public Section[] rightArm;
    public Material interior;
    public GameObject bloodMark;

    private static ExecutionGeometry loaded;

    private static ResourceRequest pending;

    internal static void Preload()
    {
        if (loaded == null && pending == null)
        {
            pending = Resources.LoadAsync<ExecutionGeometry>("Combat/ExecutionGeometry");
        }
    }

    internal static ExecutionGeometry Load()
    {
        if (loaded == null && pending != null && pending.isDone)
        {
            loaded = pending.asset as ExecutionGeometry;
            pending = null;
        }
        if (loaded == null)
        {
            loaded = Resources.Load<ExecutionGeometry>("Combat/ExecutionGeometry");
        }
        return loaded;
    }
}
