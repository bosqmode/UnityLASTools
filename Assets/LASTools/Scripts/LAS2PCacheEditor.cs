using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace bosqmode.LASTools
{
#if UNITY_EDITOR

    using UnityEditor;

    /// <summary>
    /// LASTools/LAS 2 PCache editorwindow
    /// </summary>
    public class LAS2PCacheEditor : EditorWindow
    {
        #region Public vars
        public List<string> filenames = new List<string>();
        public string outputpath;
        public bool mergefiles = false;
        public int pointskip = 0;
        public bool anchorToFirstPoint = true;
        #endregion

        #region private vars
        private SerializedObject so;
        private SerializedProperty filenamesprop;
        private SerializedProperty outputpathprop;
        private SerializedProperty mergeFilesprop;
        private SerializedProperty pointskipprop;
        private SerializedProperty anchortofirstpointprop;
        private LAS2PCacheConverter converter = null;
        #endregion

        #region Methods
        [MenuItem("LASTools/LAS 2 PCache")]
        private static void Init()
        {
            LAS2PCacheEditor window = (LAS2PCacheEditor)EditorWindow.GetWindow(typeof(LAS2PCacheEditor));
            window.titleContent.text = "LAS to .PCache Editor";
            window.Show();
        }

        private void OnEnable()
        {
            so = new SerializedObject(this);
            filenamesprop = so.FindProperty("filenames");
            outputpathprop = so.FindProperty("outputpath");
            mergeFilesprop = so.FindProperty("mergefiles");
            pointskipprop = so.FindProperty("pointskip");
            anchortofirstpointprop = so.FindProperty("anchorToFirstPoint");
        }

        public void OnGUI()
        {
            EditorGUILayout.LabelField("Convert .las -files into .pcache files: ");
            EditorGUILayout.LabelField("\n");

            EditorGUILayout.PropertyField(filenamesprop, true);
            if (GUILayout.Button("Add file"))
            {
                string path = EditorUtility.OpenFilePanel("select .las file", "", "las");
                filenames.Add(path);
            }

            EditorGUILayout.LabelField("\n");

            EditorGUILayout.PropertyField(outputpathprop, true);
            if (GUILayout.Button("Set outputh path"))
            {
                string path = EditorUtility.OpenFolderPanel("select output path", "", "Assets");
                outputpath = path;
            }

            EditorGUILayout.LabelField("\n");

            EditorGUILayout.LabelField("Merges the files into a single .pcache -file:");
            EditorGUILayout.PropertyField(mergeFilesprop, true);

            EditorGUILayout.LabelField("\n");

            EditorGUILayout.LabelField("Amount of points to skip in between readings:");
            EditorGUILayout.PropertyField(pointskipprop, true);

            EditorGUILayout.LabelField("\n");

            EditorGUILayout.LabelField("Whether to center the first point to zero and use it as origin:");
            EditorGUILayout.PropertyField(anchortofirstpointprop, true);

            pointskipprop.intValue = Mathf.Clamp(pointskipprop.intValue, 0, 10000);

            EditorGUILayout.LabelField("\n");

            if (GUILayout.Button("Convert"))
            {
                if (converter != null)
                    converter.Dispose();

                converter = new LAS2PCacheConverter(filenames.ToArray(), outputpath, mergefiles, pointskip, anchorToFirstPoint);
                converter.Convert();
            }

            if (converter != null)
            {
                if (GUILayout.Button("Cancel"))
                {
                    converter?.Dispose();
                    converter = null;
                    return;
                }

                int idx = 0;
                foreach (KeyValuePair<Thread, float> keyval in converter.ProgressSnapshot)
                {
                    Rect rect = EditorGUILayout.BeginVertical();
                    EditorGUI.ProgressBar(new Rect(3, rect.y + 21 * idx, position.width - 6, 20), keyval.Value, "Thread-" + idx.ToString() + " " + ((keyval.Value * 100).ToString("0.0") + "%"));
                    idx++;
                }
            }
            so.ApplyModifiedProperties();
            so.Update();
        }

        private void Update()
        {
            Repaint();
        }

        private void OnDestroy()
        {
            converter?.Cancel();
        }
        #endregion
    }

#endif
}