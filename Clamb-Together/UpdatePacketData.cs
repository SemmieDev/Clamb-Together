using System.Runtime.InteropServices;
using UnityEngine;

namespace ClambTogether;

public static class UpdatePacketData {
    public const uint SIZE = (POSITION_COMPONENT_SIZE * 3 + ROTATION_COMPONENT_SIZE * 3) * 3;

    private const uint POSITION_COMPONENT_SIZE = sizeof(short);
    private const uint ROTATION_COMPONENT_SIZE = sizeof(byte);
    private const float DEGREE_TO_BYTE = 255f / 360f;
    private const float BYTE_TO_DEGREE = 360f / 255f;

    private static readonly IntPtr updatePacketData = Marshal.AllocHGlobal((int) SIZE);

    public static unsafe byte* GetBuffer() {
        return (byte*) updatePacketData.ToPointer();
    }

    public static void Free() {
        Marshal.FreeHGlobal(updatePacketData);
    }

    public static void WriteTransform(ref int offset, Transform transform) {
        var position = transform.position;
        Marshal.WriteInt16(updatePacketData, offset, unchecked((short) (position.x * 100)));
        Marshal.WriteInt16(updatePacketData, offset += sizeof(short), unchecked((short) (position.y * 100)));
        Marshal.WriteInt16(updatePacketData, offset += sizeof(short), unchecked((short) (position.z * 100)));

        var eulerAngles = transform.rotation.eulerAngles;
        Marshal.WriteByte(updatePacketData, offset += sizeof(short), unchecked((byte) (eulerAngles.x * DEGREE_TO_BYTE)));
        Marshal.WriteByte(updatePacketData, offset += sizeof(byte), unchecked((byte) (eulerAngles.y * DEGREE_TO_BYTE)));
        Marshal.WriteByte(updatePacketData, offset += sizeof(byte), unchecked((byte) (eulerAngles.z * DEGREE_TO_BYTE)));
        offset += sizeof(byte);
    }

    public static void ReadTransform(ref int offset, Transform transform) {
        transform.position = new Vector3(
            Marshal.ReadInt16(updatePacketData, offset) / 100f,
            Marshal.ReadInt16(updatePacketData, offset += sizeof(short)) / 100f,
            Marshal.ReadInt16(updatePacketData, offset += sizeof(short)) / 100f
        );

        transform.rotation.SetEulerAngles(
            Marshal.ReadByte(updatePacketData, offset += sizeof(short)) * BYTE_TO_DEGREE,
            Marshal.ReadByte(updatePacketData, offset += sizeof(byte)) * BYTE_TO_DEGREE,
            Marshal.ReadByte(updatePacketData, offset += sizeof(byte)) * BYTE_TO_DEGREE
        );
        offset += sizeof(byte);
    }
}