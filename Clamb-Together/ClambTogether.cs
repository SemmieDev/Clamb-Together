﻿using Il2CppRootMotion.FinalIK;
using Il2CppXRClimbGame;
using MelonLoader;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ClambTogether;

public class ClambTogether : MelonMod {
    private readonly Dictionary<ulong, OtherPlayerController> otherPlayerControllers = new ();

    private GameObject otherPlayerPrefab = null!;
    private bool inGame;
    private Lobby lobby;
    private float lastUpdateTime;
    private Transform localHead = null!;
    private Transform localLeftHand = null!;
    private Transform localRightHand = null!;

    public override void OnInitializeMelon() {
        try {
            SteamClient.Init(2709120);
        } catch (Exception e) {
            LoggerInstance.Error("Failed to initialize steam", e);
            Unregister("Failed to initialize steam");
            return;
        }

        SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;

        SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
        SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;
        SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
        SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeave;

        SteamNetworking.OnP2PSessionRequest += OnP2PSessionRequest;
        SteamNetworking.OnP2PConnectionFailed += OnP2PConnectionFailed;

        LoggerInstance.Msg("Steam successfully initialized");
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName) {
#if DEBUG
        if (sceneName == "Home") {
            SceneManager.LoadScene(1);
        }
#endif
        if (sceneName == "Hike_V2" || sceneName == "Hike_V2_Demo") {
            inGame = true;

            CreateOtherPlayerPrefab();

            var characterController = GameObject.FindWithTag("Character Controller");

            if (characterController == null) {
                LoggerInstance.Error("Failed to find character controller");
                return;
            }

            localHead = characterController.transform.Find("Floor Offset/HMD/Main Camera/VRIK_Head_Target_Neo_V3_35_Exp_Decap");
            localLeftHand = characterController.transform.Find("Floor Offset/Palm_L_Target_Follower/VRIK_Left Arm_Target_Neo_V3_35_Exp_Decap");
            localRightHand = characterController.transform.Find("Floor Offset/Palm_R_Target_Follower/VRIK_Right Arm_Target_Neo_V3_35_Exp_Decap");

            if (localHead == null) {
                LoggerInstance.Error("Failed to find local head");
                return;
            }

            if (localLeftHand == null) {
                LoggerInstance.Error("Failed to find local left hand");
                return;
            }

            if (localRightHand == null) {
                LoggerInstance.Error("Failed to find local right hand");
                return;
            }

            // TODO: Add button to create lobby
            if (lobby.Id == 0 && SteamClient.Name == "SemmieDev") {
                SteamMatchmaking.CreateLobbyAsync(250);
            }
        } else {
            inGame = false;

            LeaveLobby();
        }
    }

    public override void OnUpdate() {
        if (
            lobby.Id == 0 ||
            localHead == null ||
            localLeftHand == null ||
            localRightHand == null ||
            Time.time - lastUpdateTime < UpdatePacketData.UPDATE_DELAY
        ) return;

        lastUpdateTime = Time.time;

        var offset = 0;
        UpdatePacketData.WriteTransform(ref offset, localHead);
        UpdatePacketData.WriteTransform(ref offset, localLeftHand);
        UpdatePacketData.WriteTransform(ref offset, localRightHand);

        unsafe {
            foreach (var member in lobby.Members) {
                SteamNetworking.SendP2PPacket(
                    member.Id,
                    UpdatePacketData.GetBuffer(),
                    UpdatePacketData.SIZE,
                    0,
                    P2PSend.Unreliable
                );
            }

            var size = UpdatePacketData.SIZE;
            var sender = new SteamId();

            while (SteamNetworking.IsP2PPacketAvailable()) {
                var success = SteamNetworking.ReadP2PPacket(
                    UpdatePacketData.GetBuffer(),
                    UpdatePacketData.SIZE,
                    ref size,
                    ref sender
                );

                if (!success || size != UpdatePacketData.SIZE) continue;

                var exists = otherPlayerControllers.TryGetValue(sender, out var otherPlayerController);

                if (!exists) {
                    if (lobby.Members.All(member => member.Id != sender)) continue;

                    var gameObject = Object.Instantiate(otherPlayerPrefab);

                    gameObject.SetActive(true);
                    gameObject.name = $"Other Player ({new Friend(sender).Name})";
                    otherPlayerController = gameObject.GetComponent<OtherPlayerController>();

                    otherPlayerControllers.Add(sender, otherPlayerController);
                }

                offset = 0;
                UpdatePacketData.ReadTransform(ref offset, out var headPosition, out var headRotation);
                UpdatePacketData.ReadTransform(ref offset, out var leftHandPosition, out var leftHandRotation);
                UpdatePacketData.ReadTransform(ref offset, out var rightHandPosition, out var rightHandRotation);

                otherPlayerController?.UpdateTransforms(
                    headPosition,
                    headRotation,
                    leftHandPosition,
                    leftHandRotation,
                    rightHandPosition,
                    rightHandRotation
                );

                if (!exists) otherPlayerController?.ResetInterpolation();
            }
        }
    }

    public override void OnDeinitializeMelon() {
        LeaveLobby();

        if (SteamClient.IsValid) {
            SteamClient.Shutdown();
        }

        UpdatePacketData.Free();
    }

    private void CreateOtherPlayerPrefab() {
        var playerBody = GameObject.FindWithTag("Player Body");

        if (playerBody == null) {
            LoggerInstance.Error("Failed to find player body");
            return;
        }

        otherPlayerPrefab = new GameObject("Other Player");
        otherPlayerPrefab.SetActive(false);

        var head = new GameObject("Head").transform;
        var leftHand = new GameObject("Left Hand").transform;
        var rightHand = new GameObject("Right Hand").transform;

        head.parent = otherPlayerPrefab.transform;
        leftHand.parent = otherPlayerPrefab.transform;
        rightHand.parent = otherPlayerPrefab.transform;

        otherPlayerPrefab.AddComponent<OtherPlayerController>();

        var otherPlayerBody = Object.Instantiate(
            playerBody,
            otherPlayerPrefab.transform,
            false
        );

        otherPlayerBody.tag = "Untagged";

        var vrik = otherPlayerBody.GetComponent<VRIK>();
        vrik.solver.spine.headTarget = head;
        vrik.solver.leftArm.target = leftHand;
        vrik.solver.rightArm.target = rightHand;

        Object.DestroyImmediate(otherPlayerBody.GetComponent<LegsWalkAnimator>());
    }

    private void LeaveLobby() {
        if (lobby.Id != 0) {
            foreach (var member in lobby.Members) {
                SteamNetworking.CloseP2PSessionWithUser(member.Id);
            }

            lobby.Leave();
        }
    }

    private void OnGameLobbyJoinRequested(Lobby lobby, SteamId friend) {
        LeaveLobby();

        lobby.Join();
    }

    private void OnLobbyEntered(Lobby lobby) {
        if (!inGame) {
            lobby.Leave();
            LoggerInstance.Warning("Entered a lobby whilst not in-game");
            return;
        }

        LoggerInstance.Msg($"Joined {lobby.Owner.Name}'s lobby");

        this.lobby = lobby;
    }

    private void OnLobbyCreated(Result result, Lobby lobby) {
        if (result != Result.OK) {
            LoggerInstance.Error($"Failed to create lobby: {result}");
            return;
        }

        if (!inGame) {
            lobby.Leave();
            LoggerInstance.Warning("Created a lobby whilst not in-game");
            return;
        }

        LoggerInstance.Msg("Created a new lobby");

        if (!lobby.SetPublic()) LoggerInstance.Warning("Failed to set created lobby to public");

        this.lobby = lobby;
    }

    private void OnLobbyMemberJoined(Lobby lobby, Friend member) {
        if (!inGame || lobby.Id != this.lobby.Id) {
            return;
        }

        LoggerInstance.Msg($"{member.Name} joined the lobby");
    }

    private void OnLobbyMemberLeave(Lobby lobby, Friend member) {
        if (!inGame || lobby.Id != this.lobby.Id) {
            return;
        }

        SteamNetworking.CloseP2PSessionWithUser(member.Id);

        if (otherPlayerControllers.Remove(member.Id, out var otherPlayerController)) {
            Object.Destroy(otherPlayerController.gameObject);
        }

        LoggerInstance.Msg($"{member.Name} left the lobby");
    }

    private void OnP2PSessionRequest(SteamId steamId) {
        if (!inGame || lobby.Id == 0 || lobby.Members.All(member => member.Id != steamId)) {
            return;
        }

        SteamNetworking.AcceptP2PSessionWithUser(steamId);

        LoggerInstance.Msg($"Accepted P2P session with {steamId}");
    }

    private void OnP2PConnectionFailed(SteamId steamId, P2PSessionError error) {
        LoggerInstance.Error($"Failed to connect to {steamId}: {error}");
    }
}