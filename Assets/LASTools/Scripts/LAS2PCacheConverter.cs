using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace bosqmode.LASTools
{
    /// <summary>
    /// .las to .pcache converter
    /// </summary>
    public class LAS2PCacheConverter : IDisposable
    {
        #region Vars
        private const string POINT_FORMAT = "{0:0.#####} {1:0.#####} {2:0.#####}";

        private Dictionary<Thread, float> _progresses = new Dictionary<Thread, float>();
        private volatile bool _cancel = false;
        private string _outputpath;
        private int _pskip = 0;
        private string[] _files;
        private bool _merge = false;
        private int _totalrecords = 0;
        private ConcurrentQueue<string> _mergedQue = new ConcurrentQueue<string>();
        private Dictionary<Thread, ConcurrentQueue<string>> _separateQues = new Dictionary<Thread, ConcurrentQueue<string>>();
        private bool _useFirstPointAsAnchor = false;
        private LASPointReader _pointReader;
        private Vector3 _anchorOffset = Vector3.zero;
        #endregion

        #region Public methods
        /// <summary>
        /// Gets the current progresses of converter threads
        /// </summary>
        public KeyValuePair<Thread, float>[] ProgressSnapshot
        {
            get
            {
                return _progresses.ToArray();
            }
        }

        /// <summary>
        /// Constructor for a new converter
        /// Note: one must call Convert to actually start the conversion
        /// </summary>
        /// <param name="filenames">List of files to be processed</param>
        /// <param name="outputpath">Path to output the .pcache/.pcaches to</param>
        /// <param name="mergefiles">whether to merge the read files into one .pcache or not</param>
        /// <param name="pskip">amount of points to skip in between readings (0 means every point will be read, 1 every seconds, etc..)</param>
        /// <param name="anchorToFirstPoint">whether to zero the first read point and anchor all the other ones to it</param>
        public LAS2PCacheConverter(string[] filenames, string outputpath, bool mergefiles, int pskip, bool anchorToFirstPoint)
        {
            _pskip = pskip;
            _outputpath = outputpath;
            _files = filenames;
            _merge = mergefiles;
            _cancel = false;
            _progresses = new Dictionary<Thread, float>();
            _pointReader = new LASPointReader(_pskip, PointsCallback);
            _useFirstPointAsAnchor = anchorToFirstPoint;
        }

        /// <summary>
        /// starts the conversion
        /// </summary>
        public void Convert()
        {
            for (int i = 0; i < _files.Length; i++)
            {
                byte[] bytes = File.ReadAllBytes(_files[i]);
                LASHeader_1_2 header = LASHeaders.MarshalHeader(bytes, true);

                if (!_merge)
                {
                    Thread t = _pointReader.ReadLASPointsAsync(bytes, header);
                    _separateQues.Add(t, new ConcurrentQueue<string>());
                    StartSeparateWriter(Path.Combine(_outputpath, _files[i].Split('/').Last() + ".pcache"), header, _pskip, t);
                }
                else
                {
                    Thread t = _pointReader.ReadLASPointsAsync(bytes, header);
                    _totalrecords += Mathf.FloorToInt((header.NumberOfPointRecords / (float)(1 + _pskip)));
                }
            }

            if (_merge)
            {
                StartMergeWriter(_totalrecords);
            }
        }

        /// <summary>
        /// Cancel the reader/writer threads
        /// </summary>
        public void Cancel()
        {
            _cancel = true;
            _pointReader?.Dispose();
        }

        public void Dispose()
        {
            Cancel();
        }

        #endregion

        #region Private methods
        private void PointsCallback(Thread t, Vector3[] newpoints)
        {
            for (int i = 0; i < newpoints.Length; i++)
            {
                if (_useFirstPointAsAnchor && _anchorOffset == Vector3.zero)
                {
                    _anchorOffset.x = newpoints[0].x;
                    _anchorOffset.y = newpoints[0].y;
                    _anchorOffset.z = newpoints[0].z;
                }

                newpoints[i] = newpoints[i] - _anchorOffset;

                string str = string.Format(POINT_FORMAT, (decimal)newpoints[i].x, (decimal)newpoints[i].y, (decimal)newpoints[i].z);

                if (!_merge)
                {
                    if (_separateQues.ContainsKey(t))
                    {
                        _separateQues[t].Enqueue(str);
                    }
                }
                else
                {
                    _mergedQue.Enqueue(str);
                }
            }
        }

        private void StartSeparateWriter(string outputpath, LASHeader_1_2 header, int pskip, Thread observe)
        {
            Thread t = new Thread(() => SingleWriterThread(outputpath, header, pskip, observe));
            t.Start();
            _progresses.Add(t, 0);
        }

        private void SingleWriterThread(string outputpath, LASHeader_1_2 header, int pskip, Thread observe)
        {
            using (StreamWriter writer = new StreamWriter(outputpath))
            {
                int targetamount = Mathf.FloorToInt((float)header.NumberOfPointRecords / (1 + pskip) + 1);

                int written = 0;
                writer.WriteLine("pcache");
                writer.WriteLine("format ascii 1.0");
                writer.WriteLine("elements " + targetamount);
                writer.WriteLine("property float position.x");
                writer.WriteLine("property float position.y");
                writer.WriteLine("property float position.z");
                writer.WriteLine("end_header");

                while (true)
                {
                    if (_separateQues[observe].TryDequeue(out string row))
                    {
                        writer.WriteLine(row);

                        if (written % 1000 == 0)
                        {
                            _progresses[Thread.CurrentThread] = (written / (float)targetamount);
                        }
                        written++;
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }

                    if (_cancel || (!observe.IsAlive && !observe.ThreadState.HasFlag(ThreadState.Unstarted) && _separateQues[observe].Count == 0))
                    {
                        break;
                    }
                }
                _progresses[Thread.CurrentThread] = 1;
            }
        }

        private void StartMergeWriter(int totalrecords)
        {
            Thread t = new Thread(() => MergeWriterThread(totalrecords));
            t.Start();
            _progresses.Add(t, 0);
        }

        private void MergeWriterThread(int totalrecords)
        {
            using (StreamWriter writer = new StreamWriter(_outputpath + "/" + "MERGED" + GetCurrentUnixTimestampSeconds().ToString() + ".pcache"))
            {
                writer.WriteLine("pcache");
                writer.WriteLine("format ascii 1.0");
                writer.WriteLine("elements " + totalrecords);
                writer.WriteLine("property float position.x");
                writer.WriteLine("property float position.y");
                writer.WriteLine("property float position.z");
                writer.WriteLine("end_header");

                int writtenlines = 0;
                while (true)
                {
                    if (writtenlines % 1000 == 0)
                    {
                        _progresses[Thread.CurrentThread] = (writtenlines / (float)totalrecords);
                    }

                    if (writtenlines < totalrecords)
                    {
                        if (_mergedQue.TryDequeue(out string str))
                        {
                            writer.WriteLine(str);
                            writtenlines++;
                        }
                    }
                    else
                    {
                        break;
                    }

                    if (_cancel)
                    {
                        break;
                    }
                }

                _progresses[Thread.CurrentThread] = 1;
            }
        }

        private static readonly DateTime UnixEpoch =
        new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static long GetCurrentUnixTimestampSeconds()
        {
            return (long)(DateTime.UtcNow - UnixEpoch).TotalSeconds;
        }
        #endregion
    }
}