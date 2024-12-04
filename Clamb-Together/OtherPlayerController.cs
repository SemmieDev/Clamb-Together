using MelonLoader;
using UnityEngine;

namespace ClambTogether;

[RegisterTypeInIl2Cpp]
public class OtherPlayerController : MonoBehaviour {
    public Transform? head;
    public Transform? leftHand;
    public Transform? rightHand;

    public OtherPlayerController(IntPtr ptr) : base(ptr) {}

    private void Awake() {
        head = transform.GetChild(0);
        leftHand = transform.GetChild(1);
        rightHand = transform.GetChild(2);
    }
}