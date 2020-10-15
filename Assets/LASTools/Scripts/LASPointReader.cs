using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace bosqmode.LASTools
{
    public class LASPointReader : ThreadScheduler
    {
        #region Vars
        public const int THREAD_NOTIFY_AMOUNT = 1000;
        private Action<Thread, Vector3[]> _batchAction = null;
        private int _pskip = 0;
        #endregion

        #region Public methods
        /// <summary>
        /// Initiates new LASPointReader
        /// </summary>
        /// <param name="pointskip">amount of points to skip between readings</param>
        /// <param name="batchAction">action to call every THREAD_NOTIFY_AMOUNT of reading</param>
        public LASPointReader(int pointskip, Action<Thread, Vector3[]> batchAction)
        {
            _batchAction = batchAction;
            _pskip = pointskip;
        }

        /// <summary>
        /// Opens a .las -file and reads the points
        /// </summary>
        /// <param name="path">path to the .las file</param>
        /// <returns>the reader thread</returns>
        public Thread ReadLASPointsAsync(string path)
        {
            if (!File.Exists(path) || !path.Contains(".las"))
            {
                Debug.LogError("Trying to open a file that does not exist or is not a .las -file!");
                return null;
            }

            Thread t = new Thread(() =>
            {
                LASReaderThread(path, _pskip, _batchAction);
                ThreadFinished(Thread.CurrentThread);
            });

            t.IsBackground = true;

            QueueThread(t);

            return t;
        }

        /// <summary>
        /// Takes in the bytes of a .las file and the header,
        /// and proceeds to read the points.
        /// </summary>
        /// <param name="bytes">bytes of a .las-file</param>
        /// <param name="header">header of the .las file</param>
        /// <returns>the reader thread</returns>
        public Thread ReadLASPointsAsync(byte[] bytes, LASHeader_1_2 header)
        {
            Thread t = new Thread(() =>
            {
                LASReaderThread(_pskip, _batchAction, bytes, header);
                ThreadFinished(Thread.CurrentThread);
            });

            t.IsBackground = true;

            QueueThread(t);

            return t;
        }
        #endregion

        #region Private methods
        private void LASReaderThread(string path, int pointskip, Action<Thread, Vector3[]> cb)
        {
            byte[] bytes = File.ReadAllBytes(path);
            LASHeader_1_2 header = LASHeaders.MarshalHeader(bytes, true);

            LASReaderThread(pointskip, cb, bytes, header);
        }

        private void LASReaderThread(int pointskip, Action<Thread, Vector3[]> cb, byte[] bytes, LASHeader_1_2 header)
        {
            bytes = bytes.Skip((int)header.OffsetToPointData).ToArray();
            List<Vector3> vertices = new List<Vector3>();

            using (MemoryStream stream = new MemoryStream(bytes))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {

                    for (int i = 0; i < header.NumberOfPointRecords; i++)
                    {

                        byte[] point = reader.ReadBytes(header.PointDataRecordLength);

                        if (i % (1 + pointskip) != 0)
                        {
                            continue;
                        }
                        int x = BitConverter.ToInt32(point, 0);
                        int y = BitConverter.ToInt32(point, 4);
                        int z = BitConverter.ToInt32(point, 8);

                        //ushort R = BitConverter.ToUInt16(point, 28);
                        //ushort G = BitConverter.ToUInt16(point, 30);
                        //ushort B = BitConverter.ToUInt16(point, 32);


                        Vector3 pos = new Vector3((float)((x * header.XScaleFactor) + (header.XOffset)),
                                                ((float)((y * header.YScaleFactor) + (header.YOffset))),
                                                ((float)((z * header.ZScaleFactor) + (header.ZOffset))));


                        vertices.Add(pos);

                        if (vertices.Count > THREAD_NOTIFY_AMOUNT)
                        {
                            cb.Invoke(Thread.CurrentThread, vertices.ToArray());
                            vertices.Clear();
                        }

                        if (_cancel)
                        {
                            break;
                        }
                    }
                }

                cb.Invoke(Thread.CurrentThread, vertices.ToArray());
            }
        }

        public static Vector3 GetFirstPoint(byte[] bytes, LASHeader_1_2 header)
        {
            bytes = bytes.Skip((int)header.OffsetToPointData).ToArray();

            using (MemoryStream stream = new MemoryStream(bytes))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {

                    byte[] point = reader.ReadBytes(header.PointDataRecordLength);

                    int x = BitConverter.ToInt32(point, 0);
                    int y = BitConverter.ToInt32(point, 4);
                    int z = BitConverter.ToInt32(point, 8);

                    //ushort R = BitConverter.ToUInt16(point, 28);
                    //ushort G = BitConverter.ToUInt16(point, 30);
                    //ushort B = BitConverter.ToUInt16(point, 32);


                    Vector3 pos = new Vector3((float)((x * header.XScaleFactor) + (header.XOffset)),
                                            ((float)((y * header.YScaleFactor) + (header.YOffset))),
                                            ((float)((z * header.ZScaleFactor) + (header.ZOffset))));


                    return pos;
                }
            }
        }
        #endregion
    }

}