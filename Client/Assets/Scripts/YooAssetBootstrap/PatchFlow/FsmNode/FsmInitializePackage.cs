using System.Collections;
using UnityEngine;
using UniFramework.Machine;
using YooAsset;

internal class FsmInitializePackage : IStateNode
{
    private StateMachine _machine;

    void IStateNode.OnCreate(StateMachine machine)
    {
        _machine = machine;
    }
    void IStateNode.OnEnter()
    {
        PatchEventDefine.PatchStepsChange.SendEventMessage("初始化资源包！");
        YooAssetGameRuntime runtime = (YooAssetGameRuntime)_machine.GetBlackboardValue("Runtime");
        runtime.RunHostedCoroutine(InitPackage());
    }
    void IStateNode.OnUpdate()
    {
    }
    void IStateNode.OnExit()
    {
    }

    private IEnumerator InitPackage()
    {
        var playMode = (EPlayMode)_machine.GetBlackboardValue("PlayMode");
        var packageName = (string)_machine.GetBlackboardValue("PackageName");
        var runtime = (YooAssetGameRuntime)_machine.GetBlackboardValue("Runtime");

        // 创建资源包裹类
        var package = YooAssets.TryGetPackage(packageName);
        if (package == null)
            package = YooAssets.CreatePackage(packageName);

        InitializationOperation initializationOperation = null;
        try
        {
            initializationOperation = package.InitializeAsync(runtime.CreateInitializeParameters(playMode));
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning(exception);
            PatchEventDefine.InitializeFailed.SendEventMessage();
            yield break;
        }

        yield return initializationOperation;

        // 如果初始化失败弹出提示界面
        if (initializationOperation.Status != EOperationStatus.Succeed)
        {
            Debug.LogWarning($"{initializationOperation.Error}");
            PatchEventDefine.InitializeFailed.SendEventMessage();
        }
        else
        {
            _machine.ChangeState<FsmRequestPackageVersion>();
        }
    }
}
