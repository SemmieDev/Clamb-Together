using System.Runtime.InteropServices;
using UnityEngine;

namespace ClambTogether;

public static class UpdatePacketData {
    public const float UPDATE_DELAY = 0.1f;
    public const uint SIZE = (POSITION_COMPONENT_SIZE * 3 + ROTATION_COMPONENT_SIZE * 3) * 4;

    private const uint POSITION_COMPONENT_SIZE = sizeof(short);
    private const uint ROTATION_COMPONENT_SIZE = sizeof(byte);
    private const ushort POSITION_SCALE = 100;
    private const ushort POSITION_Y_MIN = 10;
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
        Marshal.WriteInt16(updatePacketData, offset, unchecked((short) (position.x * POSITION_SCALE)));
        Marshal.WriteInt16(updatePacketData, offset += sizeof(short), unchecked((short) (ushort) ((position.y + POSITION_Y_MIN) * POSITION_SCALE)));
        Marshal.WriteInt16(updatePacketData, offset += sizeof(short), unchecked((short) (position.z * POSITION_SCALE)));

        var eulerAngles = transform.rotation.eulerAngles;
        Marshal.WriteByte(updatePacketData, offset += sizeof(short), unchecked((byte) (eulerAngles.x * DEGREE_TO_BYTE)));
        Marshal.WriteByte(updatePacketData, offset += sizeof(byte), unchecked((byte) (eulerAngles.y * DEGREE_TO_BYTE)));
        Marshal.WriteByte(updatePacketData, offset += sizeof(byte), unchecked((byte) (eulerAngles.z * DEGREE_TO_BYTE)));
        offset += sizeof(byte);
    }

    public static void ReadTransform(ref int offset, out Vector3 position, out Quaternion rotation) {
        position = new Vector3(
            Marshal.ReadInt16(updatePacketData, offset) / (float) POSITION_SCALE,
            ((ushort) Marshal.ReadInt16(updatePacketData, offset += sizeof(short)) - POSITION_Y_MIN * POSITION_SCALE) / (float) POSITION_SCALE,
            Marshal.ReadInt16(updatePacketData, offset += sizeof(short)) / (float) POSITION_SCALE
        );

        rotation = Quaternion.Euler(
            Marshal.ReadByte(updatePacketData, offset += sizeof(short)) * BYTE_TO_DEGREE,
            Marshal.ReadByte(updatePacketData, offset += sizeof(byte)) * BYTE_TO_DEGREE,
            Marshal.ReadByte(updatePacketData, offset += sizeof(byte)) * BYTE_TO_DEGREE
        );
        offset += sizeof(byte);
    }
}