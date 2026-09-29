namespace Kruty1918.SaveSystem
{
    /// <summary>
    /// Реалізація CRC-32 (IEEE 802.3 polynomial).
    /// Використовується для верифікації цілісності блоків і глобального файлу.
    /// </summary>
    public static class Crc32
    {
        private static readonly uint[] Table = BuildTable();

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 0; j < 8; j++)
                    crc = (crc & 1u) != 0u ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
                table[i] = crc;
            }
            return table;
        }

        public static uint Compute(byte[] data)
            => Compute(data, 0, data.Length);

        public static uint Compute(byte[] data, int offset, int length)
        {
            uint crc = 0xFFFFFFFFu;
            int  end = offset + length;
            for (int i = offset; i < end; i++)
                crc = (crc >> 8) ^ Table[(crc ^ data[i]) & 0xFF];
            return crc ^ 0xFFFFFFFFu;
        }
    }
}
