using System.Collections;
using UnityEngine;
using UniFramework.Machine;
using YooAsset;

public class FsmDownloadPackageFiles : IStateNode
{
    private StateMachine _machine;

    void IStateNode.OnCreate(StateMachine machine)
    {
        _machine = machine;
    }
    void IStateNode.OnEnter()
    {
        PatchEventDefine.PatchStepsChange.SendEventMessage("开始下载资源文件！");
        YooAssetGameRuntime runtime = (YooAssetGameRuntime)_machine.GetBlackboardValue("Runtime");
        runtime.RunHostedCoroutine(BeginDownload());
    }
    void IStateNode.OnUpdate()
    {
    }
    void IStateNode.OnExit()
    {
    }

    private IEnumerator BeginDownload()
    {
        var downloader = (ResourceDownloaderOperation)_machine.GetBlackboardValue("Downloader");
        if (downloader == null)
        {
            Debug.LogError("[YooAsset] Downloader was not created before download.");
            yield break;
        }

        Debug.Log($"[YooAsset] Begin download. TotalCount={downloader.TotalDownloadCount} TotalBytes={downloader.TotalDownloadBytes}");
        downloader.DownloadErrorCallback = PatchEventDefine.WebFileDownloadFailed.SendEventMessage;
        downloader.DownloadUpdateCallback = PatchEventDefine.DownloadUpdate.SendEventMessage;
        downloader.BeginDownload();
        yield return downloader;

        // 检测下载结果
        if (downloader.Status != EOperationStatus.Succeed)
        {
            Debug.LogError($"[YooAsset] Download failed: {downloader.Error}");
            yield break;
        }

        Debug.Log("[YooAsset] Download completed.");

        _machine.ChangeState<FsmDownloadPackageOver>();
    }
}
