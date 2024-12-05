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

    public static readonly unsafe byte* buffer = (byte*) NativeMemory.Alloc(SIZE);

    public static unsafe void Free() {
        NativeMemory.Free(buffer);
    }

    public static unsafe void WriteTransform(ref uint offset, Transform transform) {
        var position = transform.position;

        *(short*) (buffer + offset) = unchecked((short) (position.x * POSITION_SCALE));
        offset += sizeof(short);

        *(ushort*) (buffer + offset) = unchecked((ushort) ((position.y + POSITION_Y_MIN) * POSITION_SCALE));
        offset += sizeof(ushort);

        *(short*) (buffer + offset) = unchecked((short) (position.z * POSITION_SCALE));
        offset += sizeof(short);

        var eulerAngles = transform.rotation.eulerAngles;

        *(buffer + offset) = unchecked((byte) (eulerAngles.x * DEGREE_TO_BYTE));
        offset += sizeof(byte);

        *(buffer + offset) = unchecked((byte) (eulerAngles.y * DEGREE_TO_BYTE));
        offset += sizeof(byte);

        *(buffer + offset) = unchecked((byte) (eulerAngles.z * DEGREE_TO_BYTE));
        offset += sizeof(byte);
    }

    public static unsafe void ReadTransform(ref uint offset, out Vector3 position, out Quaternion rotation) {
        var x = *(short*) (buffer + offset) / (float) POSITION_SCALE;
        offset += sizeof(short);

        var y = (*(ushort*) (buffer + offset) - POSITION_Y_MIN * POSITION_SCALE) / (float) POSITION_SCALE;
        offset += sizeof(ushort);

        var z = *(short*) (buffer + offset) / (float) POSITION_SCALE;
        offset += sizeof(short);

        position = new Vector3(x, y, z);

        x = *(buffer + offset) * BYTE_TO_DEGREE;
        offset += sizeof(byte);

        y = *(buffer + offset) * BYTE_TO_DEGREE;
        offset += sizeof(byte);

        z = *(buffer + offset) * BYTE_TO_DEGREE;
        offset += sizeof(byte);

        rotation = Quaternion.Euler(x, y, z);
    }
}