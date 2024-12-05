using MelonLoader;
using UnityEngine;

namespace ClambTogether;

[RegisterTypeInIl2Cpp]
public class OtherPlayerController : MonoBehaviour {
    private Transform head = null!;
    private Transform leftHand = null!;
    private Transform rightHand = null!;
    private Vector3 targetHeadPosition;
    private Quaternion targetHeadRotation;
    private Vector3 targetLeftHandPosition;
    private Quaternion targetLeftHandRotation;
    private Vector3 targetRightHandPosition;
    private Quaternion targetRightHandRotation;
    private Vector3 lastHeadPosition;
    private Quaternion lastHeadRotation;
    private Vector3 lastLeftHandPosition;
    private Quaternion lastLeftHandRotation;
    private Vector3 lastRightHandPosition;
    private Quaternion lastRightHandRotation;
    private float interpolationProgress;

    public OtherPlayerController(IntPtr ptr) : base(ptr) {}

    public void UpdateTransforms(
        Vector3 headPosition,
        Quaternion headRotation,
        Vector3 leftHandPosition,
        Quaternion leftHandRotation,
        Vector3 rightHandPosition,
        Quaternion rightHandRotation
    ) {
        targetHeadPosition = headPosition;
        targetHeadRotation = headRotation;
        targetLeftHandPosition = leftHandPosition;
        targetLeftHandRotation = leftHandRotation;
        targetRightHandPosition = rightHandPosition;
        targetRightHandRotation = rightHandRotation;

        lastHeadPosition = head.position;
        lastHeadRotation = head.rotation;
        lastLeftHandPosition = leftHand.position;
        lastLeftHandRotation = leftHand.rotation;
        lastRightHandPosition = rightHand.position;
        lastRightHandRotation = rightHand.rotation;

        interpolationProgress = UpdatePacketData.UPDATE_DELAY;
    }

    public void ResetInterpolation() {
        interpolationProgress = 0;

        head.position = targetHeadPosition;
        head.rotation = targetHeadRotation;
        leftHand.position = targetLeftHandPosition;
        leftHand.rotation = targetLeftHandRotation;
        rightHand.position = targetRightHandPosition;
        rightHand.rotation = targetRightHandRotation;
    }

    private void Awake() {
        head = transform.GetChild(0);
        leftHand = transform.GetChild(1);
        rightHand = transform.GetChild(2);

        transform.position = new Vector3(0, -100, 0); // Fixes issue with IK not wanting to go below ground level
    }

    private void Update() {
        if (interpolationProgress <= 0) return;

        interpolationProgress -= Time.deltaTime;

        if (interpolationProgress < 0) interpolationProgress = 0;

        var interpolationDelta = interpolationProgress / UpdatePacketData.UPDATE_DELAY;

        head.position = Vector3.Lerp(targetHeadPosition, lastHeadPosition, interpolationDelta);
        head.rotation = Quaternion.Slerp(targetHeadRotation, lastHeadRotation, interpolationDelta);
        leftHand.position = Vector3.Lerp(targetLeftHandPosition, lastLeftHandPosition, interpolationDelta);
        leftHand.rotation = Quaternion.Slerp(targetLeftHandRotation, lastLeftHandRotation, interpolationDelta);
        rightHand.position = Vector3.Lerp(targetRightHandPosition, lastRightHandPosition, interpolationDelta);
        rightHand.rotation = Quaternion.Slerp(targetRightHandRotation, lastRightHandRotation, interpolationDelta);
    }
}