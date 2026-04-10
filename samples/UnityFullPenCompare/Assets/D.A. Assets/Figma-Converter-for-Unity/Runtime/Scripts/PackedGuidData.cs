using System;

namespace DA_Assets.FCU
{
    public readonly struct IntFloatGuid
    {
        private readonly Guid _guid;
        private IntFloatGuid(Guid g) => _guid = g;
        public Guid Value => _guid;

        // GUID byte layout (16 bytes total):
        // [0..3]  — int hash    (active, used for sprite identity)
        // [4..7]  — RESERVED    (previously: float scale, written before v3.x)
        // [8..15] — unused zeros
        //
        // Old .meta files may still contain scale in bytes [4..7].
        // Do NOT write new data into bytes [4..7] to avoid collision
        // with old encoded scale values during transition period.

        public static IntFloatGuid Encode(int hash)
        {
            var bytes = new byte[16];
            var h = BitConverter.GetBytes(hash);
            Buffer.BlockCopy(h, 0, bytes, 0, 4);
            return new IntFloatGuid(new Guid(bytes));
        }

        public static int Decode(Guid g)
        {
            byte[] b = g.ToByteArray();
            return BitConverter.ToInt32(b, 0);
        }
    }
}
