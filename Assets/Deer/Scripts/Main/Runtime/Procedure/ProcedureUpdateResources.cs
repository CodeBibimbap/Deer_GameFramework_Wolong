// ================================================
//描 述:
//作 者:杜鑫
//创建时间:2022-06-09 01-52-23
//修改作者:杜鑫
//修改时间:2022-06-09 01-52-23
//版 本:0.1 
// ===============================================
using GameFramework;
using GameFramework.Event;
using Main.Runtime.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace Main.Runtime.Procedure
{
    public enum UpdateStateType
    {
        Start = 0,
        Change,
        Success,
        Failure
    }

    public class ProcedureUpdateResources : ProcedureBase
    {
        public override bool UseNativeDialog => true;

        private float m_LastUpdateTime;
        private bool m_NoticeUpdate = false;
        private bool m_CheckConfigComplete = false;
        private bool m_CheckResourcesComplete = false;
        private bool m_NeedUpdateConfig = false;
        private bool m_NeedUpdateResources = false;
        private int m_UpdateConfigCount = 0;
        private int m_UpdateResourceCount = 0;
        private long m_UpdateTotalZipLength = 0;
        private int m_UpdateSuccessCount = 0;
        private bool m_UpdateConfigsComplete = false;
        private bool m_UpdateResourcesComplete = false;
        private List<UpdateInfoData> m_UpdateInfoDatas = new List<UpdateInfoData>();
        private int m_NativeLoadingFormId = 0;
        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            m_NoticeUpdate = false;
            m_CheckConfigComplete = false;
            m_CheckResourcesComplete = false;
            m_NeedUpdateConfig = false;
            m_NeedUpdateResources = false;
            m_UpdateConfigsComplete = false;
            m_UpdateResourcesComplete = false;
            m_UpdateTotalZipLength = 0;
            m_UpdateSuccessCount = 0;
            m_LastUpdateTime = 0;
            m_UpdateConfigCount = 0;
            m_UpdateResourceCount = 0;
            m_UpdateInfoDatas.Clear();

            GameEntryMain.Event.Subscribe(ResourceUpdateStartEventArgs.EventId, OnResourceUpdateStart);
            GameEntryMain.Event.Subscribe(ResourceUpdateChangedEventArgs.EventId, OnResourceUpdateChanged);
            GameEntryMain.Event.Subscribe(ResourceUpdateSuccessEventArgs.EventId, OnResourceUpdateSuccess);
            GameEntryMain.Event.Subscribe(ResourceUpdateFailureEventArgs.EventId, OnResourceUpdateFailure);
            GameEntryMain.Event.Subscribe(DownloadStartEventArgs.EventId, OnDownloadStart);
            GameEntryMain.Event.Subscribe(DownloadSuccessEventArgs.EventId, OnDownloadSuccess);
            GameEntryMain.Event.Subscribe(DownloadFailureEventArgs.EventId, OnDownloadFailure);

            if (GameEntryMain.Base.EditorResourceMode)
            {
                if (!DeerSettingsUtils.DeerGlobalSettings.ReadLocalConfigInEditor)
                {
                    GameEntryMain.Instance.CheckConfigVersion(OnCheckConfigComplete);
                }
                else {
                    OnCheckConfigComplete(0,0,0,0);
                }
                m_NeedUpdateResources = false;
                m_UpdateResourcesComplete = true;
                OnNoticeUpdate();
                return;
            }
            GameEntryMain.Instance.CheckConfigVersion(OnCheckConfigComplete);
            GameEntryMain.Resource.CheckResources(OnCheckResourcesComplete);
        }
        protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            base.OnLeave(procedureOwner, isShutdown);
            GameEntryMain.Event.Unsubscribe(ResourceUpdateStartEventArgs.EventId, OnResourceUpdateStart);
            GameEntryMain.Event.Unsubscribe(ResourceUpdateChangedEventArgs.EventId, OnResourceUpdateChanged);
            GameEntryMain.Event.Unsubscribe(ResourceUpdateSuccessEventArgs.EventId, OnResourceUpdateSuccess);
            GameEntryMain.Event.Unsubscribe(ResourceUpdateFailureEventArgs.EventId, OnResourceUpdateFailure);
            GameEntryMain.Event.Unsubscribe(DownloadStartEventArgs.EventId, OnDownloadStart);
            GameEntryMain.Event.Unsubscribe(DownloadSuccessEventArgs.EventId, OnDownloadSuccess);
            GameEntryMain.Event.Unsubscribe(DownloadFailureEventArgs.EventId, OnDownloadFailure);
        }
        protected override void OnUpdate(ProcedureOwner procedureOwner, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);

            if (m_CheckResourcesComplete && m_CheckConfigComplete)
            {
                if (!m_NoticeUpdate)
                {
                    OnNoticeUpdate();
                }
            }
            if (!m_UpdateResourcesComplete || !m_UpdateConfigsComplete)
            {
                return;
            }

            ChangeState<ProcedureLoadAssembly>(procedureOwner);
        }
        private void OnCheckConfigComplete(int movedCount, int removedCount, int updateCount, long updateTotalZipLength)
        {
            m_CheckConfigComplete = true;
            m_NeedUpdateConfig = updateCount > 0;
            m_UpdateConfigCount = updateCount;
            m_UpdateTotalZipLength += updateTotalZipLength;
        }
        private void OnCheckResourcesComplete(int movedCount, int removedCount, int updateCount, long updateTotalLength, long updateTotalZipLength)
        {
            m_CheckResourcesComplete = true;
            m_NeedUpdateResources = updateCount > 0;
            m_UpdateResourceCount = updateCount;
            m_UpdateTotalZipLength += updateTotalZipLength;
            GameEntryMain.UI.DeerUIInitRootForm().OnOpenLoadingForm(true);
        }

        private void OnNoticeUpdate()
        {
            m_NoticeUpdate = true;
            if (m_UpdateTotalZipLength > 0)
            {
                string conetnt = Utility.Text.Format("有{0}更新", FileUtils.GetLengthString(m_UpdateTotalZipLength));
                UnityGameFramework.Runtime.Log.Info(conetnt);
                DialogParams dialogParams = new DialogParams();
                dialogParams.Mode = 2;
                dialogParams.Title = "提示";
                dialogParams.ConfirmText = "确定";
                dialogParams.CancelText = "取消";
                dialogParams.Message = Utility.Text.Format("更新文件大小{0}，建议你在WIFI环境下进行下载，是否现在更新？", FileUtils.GetLengthString(m_UpdateTotalZipLength));
                dialogParams.OnClickConfirm = (object o) => { StartUpdate(); };
                dialogParams.OnClickCancel = (object o) => { Application.Quit(); };
                GameEntryMain.UI.DeerUIInitRootForm().OnOpenUIDialogForm(dialogParams);
            }
            else
            {
                StartUpdate();
            }
        }

        private void StartUpdate()
        {
            if (m_NeedUpdateConfig)
            {
                StartUpdateConfigs(null);
            }
            else
            {
                m_UpdateConfigsComplete = true;
            }

            if (m_NeedUpdateResources)
            {
                StartUpdateResources(null);
            }
            else
            {
                m_UpdateResourcesComplete = true;
            }
        }

        private void StartUpdateConfigs(object userData)
        {
            Log.Info("Start update config ");
            GameEntryMain.Instance.UpdateConfigs(OnUpdateConfigsComplete);
        }

        private void StartUpdateResources(object userData)
        {
            Log.Info("Start update resource group ");
            GameEntryMain.Resource.UpdateResources(OnUpdateResourcesComplete);
        }
        private void RefreshProgress()
        {
            string updateProgress = string.Empty;
            long currentTotalUpdateLength = 0L;
            for (int i = 0; i < m_UpdateInfoDatas.Count; i++)
            {
                currentTotalUpdateLength += m_UpdateInfoDatas[i].Length;
            }
            if (Time.time - m_LastUpdateTime > 1f)
            {
                m_LastUpdateTime = Time.time;
                int needTime = 0;
                if (GameEntryMain.Download.CurrentSpeed > 0)
                {
                    needTime = (int)((m_UpdateTotalZipLength - currentTotalUpdateLength) / GameEntryMain.Download.CurrentSpeed);
                }

                TimeSpan ts = new TimeSpan(0, 0, needTime);
                string timeStr = ts.ToString(@"mm\:ss");
                updateProgress = string.Format("剩余时间 {0}({1}/s)", timeStr, FileUtils.GetLengthString((int)GameEntryMain.Download.CurrentSpeed));
                Log.Info(updateProgress);
            }
            float progressTotal = (float)currentTotalUpdateLength / m_UpdateTotalZipLength;
/*            Log.Info($"更新成功数量:{m_UpdateSuccessCount} 总更新数量:{m_UpdateConfigCount + m_UpdateResourceCount} 资源数量:{m_UpdateResourceCount} Config数量:{m_UpdateConfigCount}");
            Log.Info($"当前下载:{FileUtils.GetByteLengthString(currentTotalUpdateLength)} 总下载:{FileUtils.GetByteLengthString(m_UpdateTotalZipLength)} 下载进度:{progressTotal}");
            Log.Info($"下载速度:{FileUtils.GetByteLengthString((int)GameEntryMain.Download.CurrentSpeed)}");*/
            var tips = $"{FileUtils.GetByteLengthString(currentTotalUpdateLength)}/{FileUtils.GetByteLengthString(m_UpdateTotalZipLength)}  当前下载速度每秒{FileUtils.GetByteLengthString((int)GameEntryMain.Download.CurrentSpeed)}";
            GameEntryMain.UI.DeerUIInitRootForm().OnRefreshLoadingProgress(currentTotalUpdateLength, m_UpdateTotalZipLength, tips);
        }
        private void OnUpdateConfigsComplete(bool result)
        {
            if (result)
            {
                m_UpdateConfigsComplete = true;
                Log.Info("Update config complete with no errors.");
            }
            else
            {
                Log.Error("Update config complete with errors.");
            }
        }
        private void OnUpdateResourcesComplete(GameFramework.Resource.IResourceGroup resourceGroup, bool result)
        {
            if (result)
            {
                m_UpdateResourcesComplete = true;
                Log.Info("Update resources complete with no errors.");
            }
            else
            {
                Log.Error("Update resources complete with errors.");
            }
        }
        private void OnUpdateCompleteOne(string name, int length, UpdateStateType type)
        {
            for (int i = 0; i < m_UpdateInfoDatas.Count; i++)
            {
                if (m_UpdateInfoDatas[i].Name == name)
                {
                    if (type == UpdateStateType.Failure)
                    {
                        Log.Warning("Update '{0}' is failure.", name);
                        m_UpdateInfoDatas.Remove(m_UpdateInfoDatas[i]);
                    }
                    else
                    {
                        if (type == UpdateStateType.Start)
                        {
                            Log.Warning("Update '{0}' is invalid.", name);
                        }

                        if (type == UpdateStateType.Success)
                        {
                            Log.Warning("Update '{0}' is success.", name);
                        }
                        m_UpdateInfoDatas[i].Length = length;
                    }
                    RefreshProgress();
                    return;
                }
            }
        }
        private void OnResourceUpdateStart(object sender, GameEventArgs e)
        {
            ResourceUpdateStartEventArgs ne = (ResourceUpdateStartEventArgs)e;
            OnUpdateCompleteOne(ne.Name, 0, UpdateStateType.Start);
            m_UpdateInfoDatas.Add(new UpdateInfoData(ne.Name));
        }

        private void OnResourceUpdateChanged(object sender, GameEventArgs e)
        {
            ResourceUpdateChangedEventArgs ne = (ResourceUpdateChangedEventArgs)e;
            OnUpdateCompleteOne(ne.Name, ne.CurrentLength, UpdateStateType.Change);
        }

        private void OnResourceUpdateSuccess(object sender, GameEventArgs e)
        {
            ResourceUpdateSuccessEventArgs ne = (ResourceUpdateSuccessEventArgs)e;
            OnUpdateCompleteOne(ne.Name, ne.Length, UpdateStateType.Success);
        }

        private void OnResourceUpdateFailure(object sender, GameEventArgs e)
        {
            ResourceUpdateFailureEventArgs ne = (ResourceUpdateFailureEventArgs)e;
            if (ne.RetryCount >= ne.TotalRetryCount)
            {
                Log.Error("Update resource '{0}' failure from '{1}' with error message '{2}', retry count '{3}'.", ne.Name, ne.DownloadUri, ne.ErrorMessage, ne.RetryCount.ToString());
                OpenDisplay("当前网络不可用，请检查是否连接可用wifi或移动网络");
                return;
            }
            else
            {
                Log.Info("Update resource '{0}' failure from '{1}' with error message '{2}', retry count '{3}'.", ne.Name, ne.DownloadUri, ne.ErrorMessage, ne.RetryCount.ToString());
            }
            OnUpdateCompleteOne(ne.Name, 0, UpdateStateType.Failure);
        }
        
        private void OnDownloadStart(object sender, GameEventArgs e)
        {
            DownloadStartEventArgs ne = (DownloadStartEventArgs)e;
            if (!(ne.UserData is ConfigInfo configInfo))
            {
                return;
            }
            OnUpdateCompleteOne(configInfo.Name, 0, UpdateStateType.Start);
            m_UpdateInfoDatas.Add(new UpdateInfoData(configInfo.Name));
        }
        private void OnDownloadSuccess(object sender, GameEventArgs e)
        {
            DownloadSuccessEventArgs ne = (DownloadSuccessEventArgs)e;
            if (!(ne.UserData is ConfigInfo configInfo))
            {
                return;
            }
            int size = int.Parse(configInfo.Size);
            OnUpdateCompleteOne(configInfo.Name, (size > 0 ? size : 1) * 1024, UpdateStateType.Success);
        }
        private void OnDownloadFailure(object sender, GameEventArgs e)
        {
            DownloadFailureEventArgs ne = (DownloadFailureEventArgs)e;
            if (!(ne.UserData is ConfigInfo configInfo))
            {
                return;
            }
            if (ne.ErrorMessage == "Received no data in response")
            {
                OpenDisplay("当前网络不可用，请检查是否连接可用wifi或移动网络");
                return;
            }

            if (ne.ErrorMessage.Contains("HTTP/1.1 404 Not Found"))
            {
                OpenDisplay("当前资源路径不存在，请联系技术人员检查后重新进入");
                return;
            }
            OnUpdateCompleteOne(configInfo.Name, 0, UpdateStateType.Failure);
        }
        //弹出提示
        private void OpenDisplay(string content)
        {
            DialogParams dialogParams = new DialogParams();
            dialogParams.Mode = 1;
            dialogParams.Title = "";
            dialogParams.Message = content;
            dialogParams.ConfirmText = "确认";
            dialogParams.OnClickConfirm = (object o) => { Application.Quit(); };
            GameEntryMain.UI.DeerUIInitRootForm().OnOpenUIDialogForm(dialogParams);
        }
        private class UpdateInfoData
        {
            private readonly string m_Name;

            public UpdateInfoData(string name)
            {
                m_Name = name;
            }

            public string Name => m_Name;

            public int Length
            {
                get;
                set;
            }
        }
    }
}