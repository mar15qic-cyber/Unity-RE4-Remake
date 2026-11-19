using System.Collections.Generic;
using NUnit.Framework;
[UnityEngine.Scripting.APIUpdating.MovedFrom(true,"","Assembly-CSharp-Editor","架构写入边界测试")]

public sealed class ArchitectureWriteBoundaryTests
{
    [Test]
    public void PublicSnapshotNotExposeWritableState()
    {
        var failure = new List<string>();
        ArchitectureBoundaryValidator.ValidateNewContract(failure);
        Assert.IsEmpty(failure);
    }

    [Test]
    public void CompilationCheckCanLocateExistingExternalConfigurationWrite()
    {
        var failure = new List<string>();
        ArchitectureBoundaryValidator.ValidateStateExclusiveWrite(ArchitectureBoundaryValidator.Scan(), typeof(VillagerController), new[] { "maxHealth" }, failure);
        Assert.IsTrue(failure.Exists(item => item.Contains("PerformanceSamplingEntry") && item.Contains("Tick") && item.Contains("指令偏移=")));
    }
}
