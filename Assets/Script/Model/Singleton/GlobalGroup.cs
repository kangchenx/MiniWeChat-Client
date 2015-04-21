﻿using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

using protocol;

namespace MiniWeChat
{
    public class GlobalGroup : Singleton<GlobalGroup>
    {
        private const float WAIT_QUERY_INTERVAL = 0.1f;

        private Dictionary<string, GroupItem> _groupDict = new Dictionary<string,GroupItem>();
        private Dictionary<string, UserItem> _groupMemberDict = new Dictionary<string, UserItem>();

        private HashSet<string> _waitQueryMemberSet = new HashSet<string>();

        public int Count
        {
            get { return _groupDict.Count; }
        }

        #region LifeCycle
        
        public override void Init()
        {

            base.Init();

            MessageDispatcher.GetInstance().RegisterMessageHandler((uint)ENetworkMessage.GET_PERSONALINFO_RSP, OnGetPersonalInfoRsp);
            MessageDispatcher.GetInstance().RegisterMessageHandler((uint)ENetworkMessage.CHANGE_GROUP_SYNC, OnChangeGroupSync);

            MessageDispatcher.GetInstance().RegisterMessageHandler((uint)EModelMessage.TRY_LOGIN, OnTryLogin);
            MessageDispatcher.GetInstance().RegisterMessageHandler((uint)ENetworkMessage.LOGOUT_RSP, OnLogOutRsp);
            MessageDispatcher.GetInstance().RegisterMessageHandler((uint)ENetworkMessage.OFFLINE_SYNC, OnLogOutRsp);

            StartCoroutine(QueryMemberData());

            LoadGroupData();
        }

        public override void Release()
        {
            base.Release();

            MessageDispatcher.GetInstance().UnRegisterMessageHandler((uint)ENetworkMessage.GET_PERSONALINFO_RSP, OnGetPersonalInfoRsp);
            MessageDispatcher.GetInstance().RegisterMessageHandler((uint)ENetworkMessage.CHANGE_GROUP_SYNC, OnChangeGroupSync);            

            MessageDispatcher.GetInstance().UnRegisterMessageHandler((uint)EModelMessage.TRY_LOGIN, OnTryLogin);
            MessageDispatcher.GetInstance().UnRegisterMessageHandler((uint)ENetworkMessage.LOGOUT_RSP, OnLogOutRsp);
            MessageDispatcher.GetInstance().UnRegisterMessageHandler((uint)ENetworkMessage.OFFLINE_SYNC, OnLogOutRsp);

            SaveAndClearGroupData();
        }

        #endregion

        #region QueryData

        public UserItem GetGroupMember(string userID)
        {
            if (!_groupMemberDict.ContainsKey(userID))
            {
                return _groupMemberDict[userID];
            }
            else 
	        {
                UserItem userItem = GlobalContacts.GetInstance().GetUserItemById(userID);

                if (userItem == null)
                {
                    _waitQueryMemberSet.Add(userID);
                }

                return userItem;
            }
        }

        public bool ContainsMember(string groupID, string userID)
        {
            GroupItem group = GetGroup(groupID);
            return group.memberUserId.Contains(groupID);
        }

        public GroupItem GetGroup(string groupID)
        {
            if (!_groupDict.ContainsKey(groupID))
            {
                return _groupDict[groupID];
            }
            else
            {
                return new GroupItem
                {
                    groupId = groupID
                };
            }
        }

        private IEnumerator QueryMemberData()
        {
            while(true)
            {
                if (_waitQueryMemberSet.Count != 0)
                {
                    GetUserInfoReq req = new GetUserInfoReq();
                    foreach (var item in _waitQueryMemberSet)
                    {
                        req.targetUserId.Add(item);
                    }
                    NetworkManager.GetInstance().SendPacket<GetUserInfoReq>(ENetworkMessage.GET_USERINFO_REQ, req);
                    _waitQueryMemberSet.Clear();
                }
                yield return new WaitForSeconds(WAIT_QUERY_INTERVAL);
            }
        }

        private static int SortGroupItemByName(GroupItem u1, GroupItem u2)
        {
            return (int)(u1.groupName.CompareTo(u2.groupName));
        }


        public List<GroupItem>.Enumerator GetEnumerator()
        {
            Log4U.LogDebug(_groupDict);
            List<GroupItem> sortedGroupList = new List<GroupItem>();
            foreach (var group in _groupDict.Values)
            {
                sortedGroupList.Add(group);
            }
            sortedGroupList.Sort(SortGroupItemByName);
            return sortedGroupList.GetEnumerator();
        }

        #endregion

        #region MessageHandler

        public void OnGetPersonalInfoRsp(uint iMessageType, object kParam)
        {
            NetworkMessageParam param = kParam as NetworkMessageParam;
            GetPersonalInfoRsp rsp = param.rsp as GetPersonalInfoRsp;
            GetPersonalInfoReq req = param.req as GetPersonalInfoReq;
            if (rsp.resultCode == GetPersonalInfoRsp.ResultCode.SUCCESS
                && req.friendInfo)
            {
                _groupDict.Clear();
                foreach (GroupItem group in rsp.groups)
                {
                    _groupDict[group.groupId] = group;
                }
            }
        }

        public void OnChangeGroupSync(uint iMessageType, object kParam)
        {
            ChangeGroupSync sync = kParam as ChangeGroupSync;
            GroupItem group = GetGroup(sync.groupId);
            switch (sync.changeType)
            {
                case ChangeGroupSync.ChangeType.ADD:
                    foreach (var item in sync.userId)
                    {
                        group.memberUserId.Add(item);
                    }
                    break;
                case ChangeGroupSync.ChangeType.DELETE:
                    foreach (var item in sync.userId)
                    {
                        group.memberUserId.Remove(item);
                    }
                    break;
                case ChangeGroupSync.ChangeType.UPDATE_INFO:
                    group.groupName = sync.groupName;
                    break;
                case ChangeGroupSync.ChangeType.UPDATE_MEMBER:
                    group.memberUserId.Clear();
                    foreach (var item in sync.userId)
                    {
                        group.memberUserId.Add(item);
                    }
                    break;
                default:
                    break;
            }
        }

        public void OnTryLogin(uint iMessageType, object kParam)
        {
            LoadGroupData();
        }

        public void OnLogOutRsp(uint iMessageType, object kParam)
        {
            SaveAndClearGroupData();
        }

        #endregion

        #region LocalData

        private string GetGroupDirPath()
        {
            return GlobalUser.GetInstance().GetUserDir() + "/Group";
        }

        private void SaveGroupData()
        {
            foreach (var groupID in _groupDict.Keys)
            {
                string filePath = GetGroupDirPath() + "/" + groupID;
                IOTool.SerializeToFile<GroupItem>(filePath, _groupDict[groupID]);
            }
        }

        private void SaveAndClearGroupData()
        {
            SaveGroupData();
            ClearGroupData();
        }

        private void LoadGroupData()
        {
            if (_groupDict.Count == 0 && IOTool.IsDirExist(GetGroupDirPath()))
            {
                foreach (var file in IOTool.GetFiles(GetGroupDirPath()))
                {
                    GroupItem groupItem = IOTool.DeserializeFromFile<GroupItem>(file.FullName);
                    if (groupItem != null)
                    {
                        _groupDict[groupItem.groupId] = groupItem;
                    }
                }
            }
        }

        public void ClearGroupData()
        {
            _groupDict.Clear();
        }

        #endregion
    }
}
