#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;


namespace SUG.Essentials.Editor
{

    public class EssentialsInitializerWindow : EditorWindow
    {
        private AddRequest _addRequest;

        private Vector2 _scroll;

        private DependencyInfo[] _dependencies;

        private DependencyInfo _currentInstalling;

        private bool _installingEssentials;

        private float _loadingTimer;

        //private int _installIndex;

        private Queue<DependencyInfo> _installQueue = new Queue<DependencyInfo>();

        private const string EServicesGit = "https://github.com/Sugar0612/Essentials-Services.git?path=Assets/EssentialsServices#1.0.0";

        [MenuItem("Tools/Essentials/Initialization")]
        public static void Open()
        {
            var window =
                GetWindow<EssentialsInitializerWindow>("Essentials Initialization");

            window.minSize = new Vector2(450, 400);

            window.Refresh();

            window.Show();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void Update()
        {

            if (_currentInstalling != null || _installingEssentials)
            {
                _loadingTimer += Time.deltaTime;

                Repaint();
            }

            UpdateRequest();
        }

        private void Refresh()
        {

            _dependencies = new[]
            {
                new DependencyInfo(
                    "Addressables",
                    "com.unity.addressables",
                    DependencyType.UnityPackage,
                    CheckPackageVaild
                ),

                new DependencyInfo(
                    "DOTween",
                    "DOTween",
                    DependencyType.External,
                    CheckPackageVaild,
                    "https://assetstore.unity.com/packages/tools/animation/dotween-hotween-v2-27676"
                ),


                new DependencyInfo(
                    "Essentials DI",
                    "com.sug.essentials",
                    DependencyType.GitHub,
                    CheckPackageVaild,
                    "https://github.com/Sugar0612/Essentials.git?path=Assets/Essentials#1.1.0"
                )
            };
        }

        private void Install(DependencyInfo dep)
        {
            if (dep.State == DependencyState.Installing)
                return;

            switch (dep.Type)
            {
                case DependencyType.UnityPackage:
                    dep.State = DependencyState.Installing;
                    _currentInstalling = dep;
                    _addRequest = Client.Add(dep.Id);
                    break;

                case DependencyType.External:
                    Application.OpenURL(dep.Url);
                    break;

                case DependencyType.GitHub:
                    dep.State = DependencyState.Installing;
                    _currentInstalling = dep;
                    _addRequest = Client.Add(dep.Url);
                    break;
            }
        }

        private void InstallEssentials()
        {
            if (_installingEssentials)
                return;

            _installingEssentials = true;

            _addRequest = Client.Add(EServicesGit);
        }

        //private void FixAll()
        //{
        //    _installQueue.Clear();

        //    foreach (var dep in _dependencies)
        //    {
        //        if (!dep.IsInstalled(dep.Id, dep.Type))
        //        {
        //            _installQueue.Enqueue(dep);
        //        }

        //    }
        //    InstallNext();
        //}

        private void InstallNext()
        {
            if (_installQueue.Count > 0)
            {
                var dep = _installQueue.Dequeue();
                Install(dep);
                return;
            }

            // 所有依赖完成
            if (AllDependenciesInstalled())
            {
                InstallEssentials();
            }
        }

        private void UpdateRequest()
        {

            if (_addRequest == null)
                return;

            if (!_addRequest.IsCompleted)
                return;

            var request = _addRequest;

            var installing = _currentInstalling;

            _addRequest = null;

            _currentInstalling = null;

            if (request.Status == StatusCode.Success)
            {
                Debug.Log("Install success.");

                if (installing != null)
                    installing.State = DependencyState.Installed;
            }
            else
            {
                string error = request.Error != null ? request.Error.message : "Unknown package manager error";

                //Debug.LogError(error);

                if (installing != null)
                {
                    installing.State = DependencyState.NotInstalled;
                }
            }

            if (installing != null)
            {
                InstallNext();
            }
            else if (_installingEssentials)
            {
                _installingEssentials = false;
                _addRequest = null;
            }

            Refresh();

            Repaint();
        }

        private bool AllDependenciesInstalled()
        {
            foreach (var dep in _dependencies)
            {
                if (!dep.IsInstalled(dep.Id, dep.Type))
                    return false;
            }

            return true;
        }

        #region OnGUI

        private void OnGUI()
        {
            GUILayout.Space(10);

            var titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.margin = new RectOffset(0, 0, 0, 0);
            titleStyle.padding = new RectOffset(0, 0, 2, 0);

            var statusStyle = new GUIStyle(EditorStyles.miniBoldLabel);
            statusStyle.alignment = TextAnchor.MiddleLeft;
            statusStyle.margin = new RectOffset(8, 0, 0, 0);
            statusStyle.padding = new RectOffset(0, 0, 0, 0);

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(24));

            GUILayout.Label(
                "Essentials Initialization",
                titleStyle
            );

            var ready = AllDependenciesInstalled() ? "Ready" : "Not Ready";
            var statusColor = AllDependenciesInstalled() ? Color.green : new Color(1f, 0.8f, 0.2f);

            GUI.contentColor = statusColor;
            GUILayout.Label(ready, statusStyle, GUILayout.Height(18));
            GUI.contentColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "Essentials requires the following dependencies.",
                MessageType.Info
            );

            //GUILayout.Space(10);

            //GUI.backgroundColor = Color.green;

            //if (GUILayout.Button("Fix All Dependencies", GUILayout.Height(35)))
            //{
            //    FixAll();
            //}

            GUI.backgroundColor = Color.white;

            GUILayout.Space(15);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var dep in _dependencies)
            {
                DrawDependency(dep);
            }

            EditorGUILayout.EndScrollView();

            GUILayout.Space(20);

            DrawEssServicesInstall();
        }

        /// <summary>
        /// 依赖面板绘制
        /// </summary>
        /// <param name="dep"></param>
        private void DrawDependency(DependencyInfo dep)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.Height(36));

            GUILayout.Space(8);
            GUILayout.Label(
                dep.Name,
                GUILayout.Width(150),
                GUILayout.Height(18)
            );

            GUILayout.FlexibleSpace();

            if (dep.State == DependencyState.Installing)
            {
                DrawSpinner();
            }
            else if (dep.IsInstalled(dep.Id, dep.Type))
            {
                dep.State = DependencyState.Installed;

                GUI.color = new Color(0.3f, 0.9f, 0.4f);
                GUILayout.Label("✔ Installed", GUILayout.Width(120), GUILayout.Height(18));
                GUI.color = Color.white;
            }
            else
            {
                string button = dep.Type == DependencyType.External ? "Open Page" : "Install";

                if (GUILayout.Button(button, GUILayout.Width(120), GUILayout.Height(22)))
                {
                    Install(dep);
                }
            }

            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// EssServices安装面板绘制
        /// </summary>
        private void DrawEssServicesInstall()
        {
            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.Height(40));

            GUILayout.Space(8);
            GUILayout.Label("Essentials Services", GUILayout.Width(150), GUILayout.Height(18));
            GUILayout.FlexibleSpace();

            bool enable = AllDependenciesInstalled();
            GUI.enabled = enable && !_installingEssentials;

            if (_installingEssentials)
            {
                DrawSpinner();
            }
            else
            {
                if (GUILayout.Button("Install", GUILayout.Width(120), GUILayout.Height(22)))
                {
                    InstallEssentials();
                }
            }

            GUI.enabled = true;
            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();

            if (!enable)
            {
                EditorGUILayout.HelpBox("Please install all dependencies first.", MessageType.Warning);
            }
        }

        private void DrawSpinner()
        {
            Rect rect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(120));

            float angle = (_loadingTimer * 240f) % 360f; 

            Vector3 start = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0);

            Handles.BeginGUI();

            Handles.color = Color.white;

            Handles.DrawWireArc(rect.center, Vector3.forward, start, 270f, 7f);

            Handles.EndGUI();
        }

        #endregion

        #region CheckPackage

        private static bool CheckPackageVaild(string id, DependencyType type)
        {
            if (type == DependencyType.UnityPackage)
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForPackageName(id);
                return package != null;
            }

            if (type == DependencyType.External)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name == id)
                    {
                        return true;
                    }
                }
            }

            if (type == DependencyType.GitHub)
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForPackageName(id);
                return package != null;
            }

            return false;
        }

        #endregion

        private enum DependencyType
        {
            UnityPackage,
            External,
            GitHub,
        }

        private enum DependencyState
        {

            NotInstalled,


            Installing,


            Installed

        }

        private class DependencyInfo
        {

            public string Name;

            public string Id;

            public DependencyType Type;

            public string Url;

            public DependencyState State = DependencyState.NotInstalled;

            private Func<string, DependencyType, bool> _checker;

            public DependencyInfo(string name, string id, DependencyType type, Func<string, DependencyType, bool> checker, string url = null)
            {
                // TODO...
                Name = name;

                Id = id;

                Type = type;

                Url = url;

                _checker = checker;

                if (_checker(id, type))
                {
                    State = DependencyState.Installed;
                }
            }

            public bool IsInstalled(string id, DependencyType type)
            {
                try
                {
                    return _checker(id, type);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);

                    return false;
                }
            }
        }
    }
}

#endif