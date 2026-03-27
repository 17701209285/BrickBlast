using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniFramework.Machine;
using YooAsset;

public class FsmUpdatePackageManifest : IStateNode
{
    private StateMachine _machine;

    void IStateNode.OnCreate(StateMachine machine)
    {
        _machine = machine;
    }
    void IStateNode.OnEnter()
    {
        PatchEventDefine.PatchStepsChange.SendEventMessage("更新资源清单！");
        YooAssetGameRuntime runtime = (YooAssetGameRuntime)_machine.GetBlackboardValue("Runtime");
        runtime.RunHostedCoroutine(UpdateManifest());
    }
    void IStateNode.OnUpdate()
    {
    }
    void IStateNode.OnExit()
    {
    }

    private IEnumerator UpdateManifest()
    {
        var packageName = (string)_machine.GetBlackboardValue("PackageName");
        var packageVersion = (string)_machine.GetBlackboardValue("PackageVersion");
        int timeout = (int)_machine.GetBlackboardValue("RequestTimeout");
        var package = YooAssets.GetPackage(packageName);
        var operation = package.UpdatePackageManifestAsync(packageVersion, timeout);
        yield return operation;

        if (operation.Status != EOperationStatus.Succeed)
        {
            Debug.LogWarning("[YooAsset] Update package manifest failed: " + operation.Error);
            GameLog.Warning(operation.Error);
            PatchEventDefine.PackageManifestUpdateFailed.SendEventMessage();
            yield break;
        }
        else
        {
            _machine.ChangeState<FsmCreateDownloader>();
        }
    }
}
