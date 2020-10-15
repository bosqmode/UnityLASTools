using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEngine.VFX;
using UnityEngine.VFX.Utility;

namespace bosqmode.LASTools
{
    #region Property drawer
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine.UIElements;

    /// <summary>
    /// propertydrawer hack to display a button in propertybindings list of a VFXPropertyBinder.cs
    /// </summary>
    [CustomPropertyDrawer(typeof(VFXButton))]
    public class VFXButtonDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {

            EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            EditorGUI.BeginChangeCheck();

            if (GUI.Button(new Rect(position.x, position.y, 100, position.height), "Update")){
                property.FindPropertyRelative("pressed").boolValue = true;
            }

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }

    [System.Serializable]
    public class VFXButton
    {
        public bool pressed = false;
    }
    #endregion

    /// <summary>
    /// VFXBinder to bind a .las-file into a vfx graph
    /// </summary>
    [AddComponentMenu("VFX/Property Binders/LASBinder")]
    [VFXBinder("LASBinder")]
    internal class LASBinder : VFXBinderBase
    {
#region Serializable
        [VFXPropertyBinding("UnityEngine.Texture2DArray"), UnityEngine.Serialization.FormerlySerializedAs("PositionMapArrayParameter")]
        private ExposedProperty PositionMapPropertyArray = "_PositionMapArray";
        [VFXPropertyBinding("System.Int32"), UnityEngine.Serialization.FormerlySerializedAs("PositionDepthParameter")]
        private ExposedProperty PositionDepthProperty = "_Depth";

        [Range(0, 10000)]
        [SerializeField]
        [Tooltip("skip this many points from the addition to the vfx-graph")]
        private int m_skip = 25;

        [SerializeField]
        [Tooltip("LAS -files in StreamingAssets to be added to the vfx-graph")]
        private List<string> m_LASFilesInStreamingAssets;

        [SerializeField]
        [Tooltip("Use first acquired point as anchor point")]
        private bool m_useFirstPointAsAnchor = true;

        [SerializeField]
        private VFXButton _update = new VFXButton();
#endregion

#region Private vars
        private ConcurrentBag<Vector3> _points = new ConcurrentBag<Vector3>();
        private Texture2DArray _positionMapArray;
        private int _depth;
        private int _index;
        private List<Color[]> _colorarray = new List<Color[]>();
        private Vector3 _anchorOffset = Vector3.zero;
        private LASPointReader _pointReader;
        private VisualEffect _effect;
        private int _maxTextureDepthOverride = -1;

        private const int MAX_TEXTURE_DEPTH = 256;
        private const int POINT_BATCH = 500;
        private const int MAX_CONCURRENT_TEXTUREUPDATES = 3;

        private static List<LASBinder> _currentlyUpdating = new List<LASBinder>();
#endregion

#region Methods
        public void Initialize(List<string> streamingAssetFiles, int pskip, bool useanchor, Vector3 anchorOverride, int maxTextureSizeOverride = -1)
        {
            m_LASFilesInStreamingAssets = streamingAssetFiles;
            m_skip = pskip;
            m_useFirstPointAsAnchor = useanchor;
            _anchorOffset = anchorOverride;
            _maxTextureDepthOverride = maxTextureSizeOverride;
        }

        public void UpdatePoints(bool resetanchor = true)
        {
            if(resetanchor || !m_useFirstPointAsAnchor)
                _anchorOffset = Vector3.zero;

            _index = 0;
            _colorarray = new List<Color[]>() { new Color[_positionMapArray.width] };
            _depth = 0;
            InitiateTexture();
            _points = new ConcurrentBag<Vector3>();
            ReadPoints();
            _update.pressed = false;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (_pointReader != null)
            {
                _pointReader.Dispose();
            }

            if (_currentlyUpdating.Contains(this))
            {
                _currentlyUpdating.Remove(this);
            }
        }

        protected override void OnEnable()
        {
            _effect = GetComponent<VisualEffect>();
            base.OnEnable();
            _update.pressed = false;

            if(_positionMapArray == null)
                InitiateTexture();
        }

        private void InitiateTexture()
        {
            _positionMapArray = new Texture2DArray(SystemInfo.maxTextureSize, 1, _maxTextureDepthOverride > 0 ? _maxTextureDepthOverride : MAX_TEXTURE_DEPTH, TextureFormat.RGBAFloat, false, true);
        }

        private void ReadPoints()
        {
            if (_pointReader != null)
            {
                _pointReader.Dispose();
            }

            _pointReader = new LASPointReader(m_skip, PointsCallback);

            for (int i = 0; i < m_LASFilesInStreamingAssets.Count; i++)
            {
                _pointReader.ReadLASPointsAsync(Path.Combine(Application.streamingAssetsPath, m_LASFilesInStreamingAssets[i]));
            }
        }

        private void PointsCallback(System.Threading.Thread t, Vector3[] newpoints)
        {
            if (m_useFirstPointAsAnchor && _anchorOffset == Vector3.zero)
            {
                _anchorOffset = newpoints[0];
            }

            for (int i = 0; i < newpoints.Length; i++)
            {
                _points.Add(newpoints[i] - _anchorOffset);
            }
        }

        public override bool IsValid(VisualEffect component)
        {
            return component.HasTexture(PositionMapPropertyArray) &&
                component.HasInt(PositionDepthProperty) && m_LASFilesInStreamingAssets.Count > 0;
        }

        private void Update()
        {
            if(_update.pressed)
            {
                UpdatePoints();
            }

            if (_points.Count > 0)
            {
                if (_currentlyUpdating.Count < MAX_CONCURRENT_TEXTUREUPDATES && !_currentlyUpdating.Contains(this))
                {
                    _currentlyUpdating.Add(this);
                }

                if (_currentlyUpdating.Contains(this))
                {
                    AddNewPoints();
                }
            } else if (_currentlyUpdating.Contains(this))
            {
                _currentlyUpdating.Remove(this);
            }
        }

        public override void UpdateBinding(VisualEffect component)
        {
            component.SetTexture(PositionMapPropertyArray, _positionMapArray);
            component.SetInt(PositionDepthProperty, _depth);
        }

        private void AddNewPoints()
        {
            for (int i = 0; i < POINT_BATCH; i++)
            {
                if (_points.Count == 0)
                {
                    break;
                }

                if (_points.TryTake(out Vector3 p))
                {
                    if (_index <= _positionMapArray.width - 1)
                    {
                        _colorarray[_depth][_index] = new Color(p.x, p.y, p.z);
                        _index++;
                    }
                    else
                    {
                        _positionMapArray.SetPixels(_colorarray[_depth], _depth);

                        if(_depth > _positionMapArray.depth)
                        {
                            Debug.LogError("maximum depth reached: try increasing point skip");
                            break;
                        }

                        _depth++;
                        _index = 0;
                        _colorarray.Add(new Color[_positionMapArray.width]);
                    }
                }
            }

            _positionMapArray.SetPixels(_colorarray[_depth], _depth);
            _positionMapArray.Apply(false, false);

            //Force effect update since UpdateBinding doesn't seem to be called if depth is 0
            UpdateBinding(_effect);
        }

        public override string ToString()
        {
            return string.Format("LAS-binder ({0} depth)", _depth);
        }
#endregion
    }
}
#endif

