using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.VFX.Utility;


namespace bosqmode.LASTools
{
#if UNITY_EDITOR

    using UnityEditor;

    /// <summary>
    /// LASTools/LAS 2 PCache editorwindow
    /// </summary>
    public class LASCreatorEditor : EditorWindow
    {
        #region Public vars
        public List<string> filenames = new List<string>();
        public string outputpath;
        public bool mergefiles = false;
        public int pointskip = 15;
        public bool anchorToFirstPoint = true;
        public VisualEffectAsset graph;
        #endregion

        #region private vars
        private SerializedObject so;
        private SerializedProperty filenamesprop;
        private SerializedProperty mergeFilesprop;
        private SerializedProperty pointskipprop;
        private SerializedProperty anchortofirstpointprop;
        private SerializedProperty vfxgraphprop;
        private Vector2 scrollpos;
        private int generated = 0;
        private Transform parent = null;
        private Vector3 anchor;
        #endregion

        #region Methods
        [MenuItem("LASTools/LAS Scene Creator")]
        private static void Init()
        {
            LASCreatorEditor window = (LASCreatorEditor)EditorWindow.GetWindow(typeof(LASCreatorEditor));
            window.titleContent.text = "(EXPERIMENTAL) LAS Scene Creator";
            window.Show();
        }

        private void OnEnable()
        {
            so = new SerializedObject(this);
            filenamesprop = so.FindProperty("filenames");
            mergeFilesprop = so.FindProperty("mergefiles");
            pointskipprop = so.FindProperty("pointskip");
            anchortofirstpointprop = so.FindProperty("anchorToFirstPoint");
            vfxgraphprop = so.FindProperty("graph");

            filenames = new List<string>();
        }

        public void OnGUI()
        {
            scrollpos = EditorGUILayout.BeginScrollView(scrollpos);
            EditorGUILayout.LabelField("EXPERIMENTAL Editor to easily instantiate VFX Graphs from .las into the scene");
            EditorGUILayout.LabelField("\n");

            EditorGUILayout.LabelField(".las files in StreamingAssets:");
            EditorGUILayout.PropertyField(filenamesprop, true);
            if (GUILayout.Button("Add file"))
            {
                string path = EditorUtility.OpenFilePanel("select .las file", "Assets/StreamingAssets", "las");
                string[] filepath = path.Split('/');
                filenames.Add(filepath[filepath.Length - 1]);
            }

            EditorGUILayout.LabelField("\n");
            EditorGUILayout.LabelField("VisualEffect to be used:");
            EditorGUILayout.LabelField("Requires two exposed properties in the graph");
            EditorGUILayout.LabelField("1. _PositionMapArray (Texture2DArray)");
            EditorGUILayout.LabelField("2. _Depth (int)");
            EditorGUILayout.PropertyField(vfxgraphprop, true);

            EditorGUILayout.LabelField("\n");

            EditorGUILayout.LabelField("Whether to merge all to one LASBinder or create separate");
            EditorGUILayout.LabelField("(try to disable if precision of the graph becomes a problem)");
            EditorGUILayout.PropertyField(mergeFilesprop, true);

            EditorGUILayout.LabelField("\n");

            EditorGUILayout.LabelField("Amount of points to skip in between readings:");
            EditorGUILayout.PropertyField(pointskipprop, true);

            EditorGUILayout.LabelField("\n");

            EditorGUILayout.LabelField("Whether to center the first point to zero and use it as origin:");
            EditorGUILayout.PropertyField(anchortofirstpointprop, true);

            pointskipprop.intValue = Mathf.Clamp(pointskipprop.intValue, 0, 10000);

            if (GUILayout.Button("Add to selected gameobject"))
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath, filenames[0]));
                LASHeader_1_2 firstfileheader = LASHeaders.MarshalHeader(bytes, true);
                anchor = LASPointReader.GetFirstPoint(bytes, firstfileheader);

                if (Selection.transforms != null && Selection.transforms.Length > 0)
                {
                    parent = Selection.transforms[0];
                }

                if (!mergefiles)
                {
                    generated = 0;
                    EditorApplication.update += FrameByFrameNewGraph;
                }
                else
                {
                    NewGraph(filenames, parent, anchor);
                }
            }

            EditorGUILayout.LabelField("\n");

            EditorGUILayout.EndScrollView();

            so.ApplyModifiedProperties();
            so.Update();
        }

        private void FrameByFrameNewGraph()
        {
            if(generated < filenames.Count)
            {
                NewGraph(new List<string>() { filenames[generated] }, parent, anchor);
                generated++;
            }
        }

        private void NewGraph(List<string> files, Transform parent, Vector3 anchor)
        {
            GameObject go = new GameObject(files[0], typeof(VisualEffect), typeof(VFXPropertyBinder)).gameObject;
            go.transform.SetParent(parent);

            VisualEffect vfx = go.GetComponent<VisualEffect>();
            vfx.visualEffectAsset = graph;
            VFXPropertyBinder binder = go.GetComponent<VFXPropertyBinder>();

            LASBinder lasbinder = binder.AddPropertyBinder<LASBinder>();
            lasbinder.Initialize(files, pointskip, anchorToFirstPoint, anchor, 10);
            lasbinder.UpdatePoints(false);
        }

        private void Update()
        {
            Repaint();
        }
        #endregion
    }

#endif
}