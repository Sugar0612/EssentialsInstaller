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

        private const string EssentialsGit = "https://github.com/Sugar0612/Essentials.git?path=Assets/Essentials#1.0.1";

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
                    CheckAddressables
                ),

                new DependencyInfo(
                    "DOTween",
                    "DOTween",
                    DependencyType.External,
                    CheckDOTween,
                    "https://assetstore.unity.com/packages/tools/animation/dotween-hotween-v2-27676"
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
            }
        }

        private void InstallEssentials()
        {
            if (_installingEssentials)
                return;

            _installingEssentials = true;

            _addRequest = Client.Add(EssentialsGit);
        }

        private void FixAll()
        {
            _installQueue.Clear();

            foreach (var dep in _dependencies)
            {
                if (!dep.IsInstalled())
                {
                    _installQueue.Enqueue(dep);
                }

            }
            InstallNext();
        }

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
                Debug.Log("Essentials installed.");
            }

            Refresh();

            Repaint();
        }

        private void OnGUI()
        {
            GUILayout.Space(10);

            GUILayout.Label(
                "Essentials Initialization",
                EditorStyles.boldLabel
            );

            EditorGUILayout.HelpBox(
                "Essentials requires the following dependencies.",
                MessageType.Info
            );

            GUILayout.Space(10);

            GUI.backgroundColor = Color.green;

            if (GUILayout.Button("Fix All Dependencies", GUILayout.Height(35)))
            {
                FixAll();
            }

            GUI.backgroundColor = Color.white;

            GUILayout.Space(15);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var dep in _dependencies)
            {
                DrawDependency(dep);
            }

            EditorGUILayout.EndScrollView();

            GUILayout.Space(20);

            DrawEssentialsInstall();
        }

        private void DrawDependency(DependencyInfo dep)
        {

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            GUILayout.Label(
                dep.Name,
                GUILayout.Width(150)
            );

            GUILayout.FlexibleSpace();

            if (dep.State == DependencyState.Installing)
            {
                DrawSpinner();
            }
            else if (dep.IsInstalled())
            {

                dep.State = DependencyState.Installed;

                GUI.color = Color.green;

                GUILayout.Label(
                    "✔ Installed",
                    GUILayout.Width(120)
                );

                GUI.color = Color.white;
            }
            else
            {

                string button = dep.Type == DependencyType.UnityPackage ? "Install" : "Open Page";

                if (GUILayout.Button(button,GUILayout.Width(120)))
                {
                    Install(dep);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawEssentialsInstall()
        {

            GUILayout.Space(10);

            bool enable = AllDependenciesInstalled();

            GUI.enabled = enable && !_installingEssentials;

            if (_installingEssentials)
            {
                DrawSpinner();
            }
            else
            {
                if (GUILayout.Button("Install Essentials", GUILayout.Height(35)))
                {
                    InstallEssentials();
                }
            }

            GUI.enabled = true;

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

        private bool AllDependenciesInstalled()
        {
            foreach (var dep in _dependencies)
            {
                if (!dep.IsInstalled())
                    return false;
            }

            return true;
        }

        private static bool CheckAddressables()
        {

            var package = UnityEditor.PackageManager.PackageInfo.FindForPackageName("com.unity.addressables");

            return package != null;
        }

        private static bool CheckDOTween()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == "DOTween")
                {
                    return true;
                }
            }

            return false;
        }

        private enum DependencyType
        {

            UnityPackage,


            External

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

            private Func<bool> _checker;

            public DependencyInfo(string name, string id, DependencyType type, Func<bool> checker, string url = null)
            {
                Name = name;

                Id = id;

                Type = type;

                Url = url;

                _checker = checker;

                if (_checker())
                {
                    State = DependencyState.Installed;
                }
            }

            public bool IsInstalled()
            {
                try
                {
                    return _checker();
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
