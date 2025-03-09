using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MonoMod.RuntimeDetour;
using Photon.Pun;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LateJoin
{
    [BepInPlugin("nickklmao.latejoin", MOD_NAME, "1.0.0")]
    internal sealed class Entry : BaseUnityPlugin
    {
        private const string MOD_NAME = "Late Join";

        internal static readonly ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource(MOD_NAME);

        private static void RunManager_ChangeLevelHook(Action<RunManager, bool, bool, RunManager.ChangeLevelType> orig, RunManager self, bool _completedLevel, bool _levelFailed, RunManager.ChangeLevelType _changeLevelType)
        {
            orig.Invoke(self, _completedLevel, _levelFailed, _changeLevelType);
            
            if (_levelFailed || !PhotonNetwork.IsMasterClient)
                return;

            var canJoin = SemiFunc.RunIsLobby() || SemiFunc.RunIsLobbyMenu(); 
            
            if (canJoin)
                SteamManager.instance.UnlockLobby();
            else
                SteamManager.instance.LockLobby();
            
            PhotonNetwork.CurrentRoom.IsOpen = canJoin;

            _playersJoining.Clear();
        }

        /* Fixes players teleporting when another player late joins
        private static void PlayerAvatar_SpawnHook(Action<PlayerAvatar, Vector3, Quaternion> orig, PlayerAvatar self, Vector3 position, Quaternion rotation)
        {
            if ((bool) AccessTools.Field(typeof(PlayerAvatar), "spawned").GetValue(self))
                return;
            
            orig.Invoke(self, position, rotation);
        }
        */

        // Temporary fix to load in players. Reloads the lobby whenever a new player loads in
        private static HashSet<string> _playersJoining = new HashSet<string>();

        private static void SteamManager_OnLobbyMemberJoinedHook(Action<SteamManager, Lobby, Friend> orig, SteamManager self, Lobby _lobby, Friend _friend)
        {
            orig(self, _lobby, _friend);
            if (SemiFunc.IsMasterClient() && SemiFunc.RunIsLobby())
                _playersJoining.Add(_friend.Id.Value.ToString());
        }

        private static void SteamManager_OnLobbyMemberLeftHook(Action<SteamManager, Lobby, Friend> orig, SteamManager self, Lobby _lobby, Friend _friend)
        {
            orig(self, _lobby, _friend);
            if (SemiFunc.IsMasterClient())
                _playersJoining.Remove(_friend.Id.Value.ToString());
        }

        private static void PlayerAvatar_SpawnHook(Action<PlayerAvatar, Vector3, Quaternion> orig, PlayerAvatar self, Vector3 position, Quaternion rotation)
        {
            orig.Invoke(self, position, rotation);

            string id = (string)AccessTools.Field(typeof(PlayerAvatar), "steamID").GetValue(self);
            if (_playersJoining.Contains(id))
            {
                _playersJoining.Remove(id);
                RunManager.instance.RestartScene();
            }
        }

        private void Awake()
        {
            new Hook(AccessTools.Method(typeof(RunManager), "ChangeLevel"), RunManager_ChangeLevelHook);
            new Hook(AccessTools.Method(typeof(SteamManager), "OnLobbyMemberJoined"), SteamManager_OnLobbyMemberJoinedHook);
            new Hook(AccessTools.Method(typeof(SteamManager), "OnLobbyMemberLeft"), SteamManager_OnLobbyMemberLeftHook);
            new Hook(AccessTools.Method(typeof(PlayerAvatar), "Spawn"), PlayerAvatar_SpawnHook);
        }
    }
}