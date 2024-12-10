using System.Globalization;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppRootMotion.FinalIK;
using Il2CppXRClimbGame;
using MelonLoader;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ClambTogether;

public class ClambTogether : MelonMod {
    public const string MOD_VERSION = "1.0.0";
    public const uint PROTOCOL_VERSION = 1;

    private readonly Dictionary<ulong, OtherPlayerController> otherPlayerControllers = new ();

    private GameObject otherPlayerPrefab = null!;
    private bool inGame;
    private Lobby lobby;
    private float lastUpdateTime;
    private Transform localHead = null!;
    private Transform localLeftHand = null!;
    private Transform localRightHand = null!;
    private Transform localHammer = null!;
    private TogetherUI? togetherUI;

    public Lobby GetLobby() {
        return lobby;
    }

    public void CreateLobby() {
        LeaveLobby();

        SteamMatchmaking.CreateLobbyAsync(250);
    }

    public void LeaveLobby() {
        if (lobby.Id == 0) return;

        foreach (var member in lobby.Members) {
            SteamNetworking.CloseP2PSessionWithUser(member.Id);
        }

        lobby.Leave();

        lobby = new Lobby(0);

        foreach (var otherPlayerController in otherPlayerControllers.Values) {
            if (otherPlayerController == null || otherPlayerController.gameObject == null) continue;

            Object.Destroy(otherPlayerController.gameObject);
        }

        otherPlayerControllers.Clear();

        togetherUI?.OnLobbyLeft();
    }

    public override void OnInitializeMelon() {
        var appId = 2709120U;

        if (File.Exists("steam_appid.txt")) {
            try {
                if (!uint.TryParse(File.OpenText("steam_appid.txt").ReadLine(), NumberStyles.None, CultureInfo.InvariantCulture, out appId)) {
                    LoggerInstance.Error("Invalid app ID in steam_appid.txt");
                }
            } catch (Exception e) {
                LoggerInstance.Error("Error whilst trying to read app ID from steam_appid.txt", e);
            }
        }

        try {
            SteamClient.Init(appId, false);
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

            var characterController = GameObject.FindWithTag("Character Controller");

            if (characterController == null) {
                LoggerInstance.Error("Failed to find character controller");
                return;
            }

            localHead = characterController.transform.Find("Floor Offset/HMD/Main Camera/VRIK_Head_Target_Neo_V3_35_Exp_Decap");
            localLeftHand = characterController.transform.Find("Floor Offset/Palm_L_Target_Follower/VRIK_Left Arm_Target_Neo_V3_35_Exp_Decap");
            localRightHand = characterController.transform.Find("Floor Offset/Palm_R_Target_Follower/VRIK_Right Arm_Target_Neo_V3_35_Exp_Decap");
            localHammer = GameObject.Find("/Item_Hammer/Model/Mesh/Sledgehammer_02").transform;

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

            if (localHammer == null) {
                LoggerInstance.Error("Failed to find local hammer");
                return;
            }

            CreateOtherPlayerPrefab();
            togetherUI = new TogetherUI(this);
        } else {
            inGame = false;
            togetherUI = null;

            LeaveLobby();
        }
    }

    public override void OnUpdate() {
        SteamClient.RunCallbacks();

        if (
            lobby.Id == 0 ||
            localHead == null ||
            localLeftHand == null ||
            localRightHand == null ||
            localHammer == null ||
            Time.time - lastUpdateTime < UpdatePacketData.UPDATE_DELAY
        ) return;

        lastUpdateTime = Time.time;

        var offset = 0U;
        UpdatePacketData.WriteTransform(ref offset, localHead);
        UpdatePacketData.WriteTransform(ref offset, localLeftHand);
        UpdatePacketData.WriteTransform(ref offset, localRightHand);
        UpdatePacketData.WriteTransform(ref offset, localHammer);

        foreach (var member in lobby.Members) {
            if (member.Id == SteamClient.SteamId) continue;

            unsafe {
                SteamNetworking.SendP2PPacket(
                    member.Id,
                    UpdatePacketData.buffer,
                    UpdatePacketData.SIZE,
                    0,
                    P2PSend.Unreliable
                );
            }
        }

        var size = UpdatePacketData.SIZE;
        var sender = new SteamId();

        while (SteamNetworking.IsP2PPacketAvailable()) {
            bool success;

            unsafe {
                success = SteamNetworking.ReadP2PPacket(
                    UpdatePacketData.buffer,
                    UpdatePacketData.SIZE,
                    ref size,
                    ref sender
                );
            }

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
            UpdatePacketData.ReadTransform(ref offset, out var hammerPosition, out var hammerRotation);

            otherPlayerController?.UpdateTransforms(
                headPosition,
                headRotation,
                leftHandPosition,
                leftHandRotation,
                rightHandPosition,
                rightHandRotation,
                hammerPosition,
                hammerRotation
            );

            if (!exists) otherPlayerController?.ResetInterpolation();
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
        var hammer = new GameObject("Hammer").transform;

        head.parent = otherPlayerPrefab.transform;
        leftHand.parent = otherPlayerPrefab.transform;
        rightHand.parent = otherPlayerPrefab.transform;
        hammer.parent = otherPlayerPrefab.transform;

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

        Object.Destroy(otherPlayerBody.GetComponent<LegsWalkAnimator>());

        var foundPrefab = false;

        foreach (var prefab in Resources.FindObjectsOfTypeAll<GameObject>()) {
            if (prefab.name != "Neo_V3_35_Exp_OBE Body_Hat") continue;

            foundPrefab = true;

            var prefabBodyMeshRenderer = prefab.transform.Find("CC_Base_Body").GetComponent<SkinnedMeshRenderer>();
            var prefabShirtMeshRenderer = prefab.transform.Find("Shirt").GetComponent<SkinnedMeshRenderer>();

            var bodyMeshRenderer = otherPlayerBody.transform.Find("CC_Base_Body").GetComponent<SkinnedMeshRenderer>();
            var bodyMaterials = new Il2CppReferenceArray<Material?>(6);

            bodyMaterials[0] = prefabBodyMeshRenderer.sharedMaterials[0];
            bodyMaterials[1] = bodyMeshRenderer.sharedMaterials[0];
            bodyMaterials[2] = bodyMeshRenderer.sharedMaterials[1];
            bodyMaterials[3] = bodyMeshRenderer.sharedMaterials[2];
            bodyMaterials[4] = bodyMeshRenderer.sharedMaterials[3];
            bodyMaterials[5] = prefabBodyMeshRenderer.sharedMaterials[5];

            bodyMeshRenderer.sharedMesh = prefabBodyMeshRenderer.sharedMesh;
            bodyMeshRenderer.sharedMaterials = bodyMaterials;

            var shirtMeshRenderer = otherPlayerBody.transform.Find("Shirt").GetComponent<SkinnedMeshRenderer>();
            shirtMeshRenderer.sharedMesh = prefabShirtMeshRenderer.sharedMesh;

            var eyes = Object.Instantiate(
                prefab.transform.Find("CC_Base_Eye").gameObject,
                otherPlayerBody.transform
            );
            var eyesMeshRenderer = eyes.GetComponent<SkinnedMeshRenderer>();
            var leftEyeBone = bodyMeshRenderer.rootBone.Find("CC_Base_Spine01/CC_Base_Spine02/CC_Base_NeckTwist01/CC_Base_NeckTwist02/CC_Base_Head/CC_Base_FacialBone/CC_Base_L_Eye");
            var rightEyeBone = bodyMeshRenderer.rootBone.Find("CC_Base_Spine01/CC_Base_Spine02/CC_Base_NeckTwist01/CC_Base_NeckTwist02/CC_Base_Head/CC_Base_FacialBone/CC_Base_R_Eye");
            var eyesBones = new Il2CppReferenceArray<Transform>(2);

            eyesMeshRenderer.rootBone = rightEyeBone;
            eyesBones[0] = leftEyeBone;
            eyesBones[1] = rightEyeBone;
            eyesMeshRenderer.bones = eyesBones;

            break;
        }

        if (!foundPrefab) {
            LoggerInstance.Error("Couldn't find complete human prefab, other players will be missing heads");
        }

        var clonedHammerModel = Object.Instantiate(
            localHammer.gameObject,
            hammer,
            false
        );

        clonedHammerModel.transform.localPosition = Vector3.zero;
        clonedHammerModel.transform.localRotation = Quaternion.identity;
    }

    private void OnGameLobbyJoinRequested(Lobby lobby, SteamId friend) {
        if (!inGame) {
            LoggerInstance.Warning("Requested joining a lobby whilst not in-game");
            return;
        }

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

        togetherUI?.OnLobbyEntered();
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

        lobby.SetData("name", $"{SteamClient.Name}'s lobby");
        lobby.SetData("mod-version", MOD_VERSION);
        lobby.SetData("protocol-version", PROTOCOL_VERSION.ToString(CultureInfo.InvariantCulture));

        LoggerInstance.Msg("Created a new lobby");

        this.lobby = lobby;
    }

    private void OnLobbyMemberJoined(Lobby lobby, Friend member) {
        if (!inGame || lobby.Id != this.lobby.Id) {
            return;
        }

        LoggerInstance.Msg($"{member.Name} joined the lobby");

        togetherUI?.OnLobbyMemberJoined(member);
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

        togetherUI?.OnLobbyMemberLeave(member);
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