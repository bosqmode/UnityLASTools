using System;
using System.Runtime.InteropServices;

namespace bosqmode.LASTools
{
    /// <summary>
    /// Headers of the .las -files
    /// TODO: more headers and automatic header detection
    /// </summary>
    public static class LASHeaders
    {
        /// <summary>
        /// Marshals .las -file byte array into .las header file
        /// </summary>
        /// <param name="bytes">.las file's bytearray</param>
        /// <param name="includeoffset">whether to include the Header's offset or make it zero (sometimes .las-files' offsets are too large for Unity to handle)</param>
        /// <returns>header in a struct</returns>
        public static LASHeader_1_2 MarshalHeader(byte[] bytes, bool includeoffset = true)
        {
            GCHandle handle;
            int BufferSize = Marshal.SizeOf(typeof(LASHeader_1_2)) + 1;
            byte[] buff = new byte[BufferSize];

            Array.Copy(bytes, 0, buff, 0, BufferSize);

            handle = GCHandle.Alloc(buff, GCHandleType.Pinned);

            var Header = (LASHeader_1_2)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(LASHeader_1_2));
            handle.Free();

            if (includeoffset)
            {
                Header.XOffsetDecimal = (decimal)Header.XOffset;
                Header.YOffsetDecimal = (decimal)Header.YOffset;
                Header.ZOffsetDecimal = (decimal)Header.ZOffset;
            }
            else
            {
                Header.XOffset = 0;
                Header.YOffset = 0;
                Header.ZOffset = 0;
            }

            return Header;
        }
    }

    //https://www.asprs.org/a/society/committees/standards/asprs_las_format_v12.pdf
    [StructLayout(LayoutKind.Explicit, Size = 227, Pack = 1)]
    public struct LASHeader_1_2
    {
        [FieldOffset(4)]
        public uint FileSourceID;

        [FieldOffset(6)]
        public UInt16 GlobalEncoding;

        [FieldOffset(8)]
        public UInt32 GuidData1;

        [FieldOffset(12)]
        public UInt16 GuidData2;

        [FieldOffset(14)]
        public UInt16 GuidData3;

        [FieldOffset(24)]
        public byte VersionMajor;

        [FieldOffset(25)]
        public byte VersionMinor;

        [FieldOffset(90)]
        public UInt16 FileCreationDayOfYear;

        [FieldOffset(92)]
        public UInt16 FileCreationYear;

        [FieldOffset(94)]
        public UInt16 HeaderSize;

        [FieldOffset(96)]
        public UInt32 OffsetToPointData;

        [FieldOffset(100)]
        public UInt32 NumberOfVariableRcords;

        [FieldOffset(104)]
        public byte PointDataFormat;

        [FieldOffset(105)]
        public UInt16 PointDataRecordLength;

        [FieldOffset(107)]
        public UInt32 NumberOfPointRecords;

        [FieldOffset(131)]
        public double XScaleFactor;

        [FieldOffset(139)]
        public double YScaleFactor;

        [FieldOffset(147)]
        public double ZScaleFactor;

        [FieldOffset(155)]
        public double XOffset;

        [FieldOffset(163)]
        public double YOffset;

        [FieldOffset(171)]
        public double ZOffset;

        [FieldOffset(179)]
        public double MaxX;

        [FieldOffset(187)]
        public double MinX;

        [FieldOffset(195)]
        public double MaxY;

        [FieldOffset(203)]
        public double MinY;

        [FieldOffset(211)]
        public double MaxZ;

        [FieldOffset(219)]
        public double MinZ;

        [FieldOffset(227)]
        public decimal XOffsetDecimal;

        [FieldOffset(243)]
        public decimal YOffsetDecimal;

        [FieldOffset(259)]
        public decimal ZOffsetDecimal;
    }
}