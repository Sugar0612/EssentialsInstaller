#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace SUG.Essentials.Editor
{
    /// <summary>
    /// Essentials 初始化窗口。
    ///
    /// Responsibilities:
    /// 1. Check and install required dependencies.
    /// 2. Install optional Essentials Services.
    /// </summary>
    public class EssentialsInitializerWindow : EditorWindow
    {
        #region Fields

        private AddRequest _addRequest;

        private Vector2 _scroll;

        private DependencyInfo[] _dependencies;
        private ServiceInfo[] _services;

        private DependencyInfo _currentInstalling;
        private ServiceInfo _currentInstallingService;

        private float _loadingTimer;
        private float _lastStateCheck;

        private string _errorMessage;

        private readonly Queue<DependencyInfo> _installQueue = new();

        #endregion


        #region Constants

        private const string EServicesGit =
            "https://github.com/Sugar0612/Essentials-Services.git?path=Assets/EssentialsServices#1.0.0";

        private const string EssentialsGit =
            "https://github.com/Sugar0612/Essentials.git?path=Assets/Essentials#1.1.0";

        private const string Version = "0.1.0";

        private const float WindowMinWidth = 540f;
        private const float WindowMinHeight = 640f;

        private const float HeaderHeight = 88f;

        private const float DependencyCardHeight = 64f;
        private const float ServiceCardHeight = 84f;

        private const float StateCheckInterval = 2f;

        #endregion


        #region GUI Styles

        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;

        private GUIStyle _sectionTitleStyle;

        private GUIStyle _cardTitleStyle;
        private GUIStyle _cardDescriptionStyle;

        private GUIStyle _smallLabelStyle;

        private GUIStyle _actionLabelStyle;

        private GUIStyle _pillTextStyle;
        private GUIStyle _progressTextStyle;
        private GUIStyle _errorTextStyle;

        private GUIStyle _footerStyle;

        #endregion


        #region Menu

        [MenuItem("Tools/Essentials/Initialization")]
        public static void Open()
        {
            var window = GetWindow<EssentialsInitializerWindow>(
                "Essentials Setup"
            );

            window.minSize = new Vector2(
                WindowMinWidth,
                WindowMinHeight
            );

            window.Refresh();

            window.Show();
        }

        #endregion


        #region Unity Events

        private void OnEnable()
        {
            InitializeStyles();
            Refresh();
        }

        private void OnDestroy()
        {
            /*
             * Window closed while a Package Manager
             * request is in flight.
             *
             * The UPM install continues in the
             * background and finishes on its own;
             * just drop our local references so a
             * reopened window starts clean.
             */
            _addRequest = null;
            _currentInstalling = null;
            _currentInstallingService = null;
        }

        private void Update()
        {
            if (IsInstalling())
            {
                _loadingTimer += Time.deltaTime;

                Repaint();
            }

            UpdateRequest();

            RefreshStatesIfStale();
        }

        #endregion


        #region Initialization

        private void Refresh()
        {
            InitializeDependencies();
            InitializeServices();

            _lastStateCheck = Time.realtimeSinceStartup;

            Repaint();
        }

        private void InitializeDependencies()
        {
            _dependencies = new[]
            {
                new DependencyInfo(
                    "Addressables",
                    "com.unity.addressables",
                    DependencyType.UnityPackage,
                    CheckPackageValid
                ),

                new DependencyInfo(
                    "DOTween",
                    "DOTween",
                    DependencyType.External,
                    CheckPackageValid,
                    "https://assetstore.unity.com/packages/tools/animation/dotween-hotween-v2-27676"
                ),

                new DependencyInfo(
                    "Essentials DI",
                    "com.sug.essentials",
                    DependencyType.GitHub,
                    (id, type) =>
                        CheckGitPackage(
                            id,
                            EssentialsGit
                        ),
                    EssentialsGit
                )
            };
        }

        private void InitializeServices()
        {
            _services = new[]
            {
                new ServiceInfo(
                    "Essentials Services",
                    "Core services provided by Essentials.",
                    EServicesGit,
                    CheckEssentialsServices
                )

                // Future services can be added here.
                //
                // new ServiceInfo(
                //     "UI Services",
                //     "UI management and runtime UI services.",
                //     "...",
                //     CheckUIServices
                // ),
                //
                // new ServiceInfo(
                //     "Audio Services",
                //     "Audio management services.",
                //     "...",
                //     CheckAudioServices
                // )
            };
        }

        #endregion


        #region Installation

        /// <summary>
        /// Install a dependency.
        ///
        /// Returns true when a Package Manager request
        /// was started (the caller must wait for it),
        /// false when the dependency was handled
        /// synchronously (e.g. an external page was
        /// opened) or rejected.
        /// </summary>
        private bool Install(DependencyInfo dependency)
        {
            if (dependency == null)
                return false;

            /*
             * Only one request at a time.
             *
             * Both Install and InstallService share the
             * single _addRequest slot, so each must
             * guard against the other.
             */
            if (IsInstalling())
                return false;

            if (dependency.State == DependencyState.Installing)
                return false;

            switch (dependency.Type)
            {
                case DependencyType.UnityPackage:

                    return StartPackageInstall(
                        dependency,
                        dependency.Id
                    );


                case DependencyType.External:

                    if (!string.IsNullOrEmpty(dependency.Url))
                    {
                        Application.OpenURL(dependency.Url);

                        Debug.Log(
                            $"[Essentials] External dependency requires manual install: {dependency.Name}\n" +
                            $"Opening: {dependency.Url}"
                        );
                    }

                    return false;


                case DependencyType.GitHub:

                    return StartPackageInstall(
                        dependency,
                        dependency.Url
                    );
            }

            return false;
        }

        /// <summary>
        /// Start Unity Package Manager installation.
        ///
        /// Returns true when a Package Manager request
        /// was actually started, false when starting
        /// failed (so the queue can move on instead of
        /// waiting for a request that never begins).
        /// </summary>
        private bool StartPackageInstall(
            DependencyInfo dependency,
            string packageUrl
        )
        {
            try
            {
                dependency.State =
                    DependencyState.Installing;

                _currentInstalling =
                    dependency;

                _loadingTimer = 0f;

                _errorMessage = null;

                Debug.Log(
                    $"[Essentials] Installing dependency: {dependency.Name}\n" +
                    $"Package: {packageUrl}"
                );

                _addRequest =
                    Client.Add(packageUrl);

                return true;
            }
            catch (Exception e)
            {
                dependency.State =
                    DependencyState.NotInstalled;

                _currentInstalling = null;
                _addRequest = null;

                SetError(
                    $"Failed to start dependency install ({dependency.Name}): {e.Message}"
                );

                Debug.LogException(e);

                return false;
            }
        }


        /// <summary>
        /// Install all missing dependencies.
        /// </summary>
        private void InstallMissingDependencies()
        {
            if (IsInstalling())
                return;

            _installQueue.Clear();

            foreach (var dependency in _dependencies)
            {
                if (dependency.State !=
                    DependencyState.Installed)
                {
                    _installQueue.Enqueue(dependency);
                }
            }

            InstallNext();
        }


        /// <summary>
        /// Install next dependency in queue.
        /// </summary>
        private void InstallNext()
        {
            while (_installQueue.Count > 0)
            {
                var dependency =
                    _installQueue.Dequeue();

                /*
                 * If a Package Manager request was
                 * started, wait for it to complete
                 * before continuing the queue.
                 *
                 * Synchronously handled dependencies
                 * (external pages) advance the queue
                 * immediately instead of stalling it.
                 */
                if (Install(dependency))
                    return;
            }

            Refresh();
        }


        /// <summary>
        /// Install a service.
        /// </summary>
        private void InstallService(ServiceInfo service)
        {
            if (service == null)
                return;

            /*
             * Same mutual-exclusion guard as Install:
             * a service install must never overlap a
             * dependency install on the shared request.
             */
            if (IsInstalling())
                return;

            if (service.State == ServiceState.Installing)
                return;

            if (!AllDependenciesInstalled())
                return;

            try
            {
                service.State =
                    ServiceState.Installing;

                _currentInstallingService =
                    service;

                _loadingTimer = 0f;

                _errorMessage = null;

                Debug.Log(
                    $"[Essentials] Installing service: {service.Name}\n" +
                    $"Package: {service.Url}"
                );

                _addRequest =
                    Client.Add(service.Url);
            }
            catch (Exception e)
            {
                service.State =
                    ServiceState.NotInstalled;

                _currentInstallingService = null;
                _addRequest = null;

                SetError(
                    $"Failed to start service install ({service.Name}): {e.Message}"
                );

                Debug.LogException(e);
            }

            Repaint();
        }

        #endregion


        #region Package Manager Request

        private void UpdateRequest()
        {
            if (_addRequest == null)
                return;

            if (!_addRequest.IsCompleted)
                return;

            var request = _addRequest;

            var installingDependency =
                _currentInstalling;

            var installingService =
                _currentInstallingService;

            _addRequest = null;

            _currentInstalling = null;
            _currentInstallingService = null;

            /*
             * IMPORTANT:
             *
             * Do not immediately assume that Error is available.
             * Some Package Manager failures can return a null Error.
             */

            if (request.Status == StatusCode.Success)
            {
                Debug.Log(
                    "[Essentials] Package installation succeeded."
                );

                if (installingDependency != null)
                {
                    installingDependency.State =
                        DependencyState.Installed;
                }

                if (installingService != null)
                {
                    installingService.State =
                        ServiceState.Installed;
                }
            }
            else
            {
                LogPackageManagerError(
                    request,
                    installingDependency,
                    installingService
                );

                if (installingDependency != null)
                {
                    installingDependency.State =
                        DependencyState.NotInstalled;
                }

                if (installingService != null)
                {
                    installingService.State =
                        ServiceState.NotInstalled;
                }
            }

            /*
             * If this was a dependency installation,
             * continue installing the queue.
             */
            if (installingDependency != null)
            {
                InstallNext();
            }

            /*
             * Rebuild the dependency/service lists so
             * cards pick up fresh check results, then
             * make sure the currently installing item
             * still shows its spinner on the new card.
             */
            Refresh();

            SyncInstallingState();

            Repaint();
        }


        /// <summary>
        /// Log detailed Unity Package Manager error information.
        /// </summary>
        private void LogPackageManagerError(
            AddRequest request,
            DependencyInfo dependency,
            ServiceInfo service
        )
        {
            string targetName;

            if (dependency != null)
            {
                targetName =
                    $"Dependency: {dependency.Name}";
            }
            else if (service != null)
            {
                targetName =
                    $"Service: {service.Name}";
            }
            else
            {
                targetName =
                    "Unknown target";
            }


            string status =
                request.Status.ToString();


            string errorCode =
                request.Error != null
                    ? request.Error.errorCode.ToString()
                    : "NULL";


            string errorMessage =
                request.Error != null
                    ? request.Error.message
                    : "Package Manager returned no error message.";


            string message =
                $"{targetName} failed: {errorMessage}";

            SetError(message);

            Debug.LogError(
                "[Essentials] Package installation failed.\n" +
                $"Target: {targetName}\n" +
                $"Status: {status}\n" +
                $"Error Code: {errorCode}\n" +
                $"Error Message: {errorMessage}"
            );
        }

        #endregion


        #region State

        private bool IsInstalling()
        {
            return _currentInstalling != null ||
                   _currentInstallingService != null;
        }

        private bool AllDependenciesInstalled()
        {
            if (_dependencies == null ||
                _dependencies.Length == 0)
            {
                return true;
            }


            foreach (var dependency in _dependencies)
            {
                if (dependency.State !=
                    DependencyState.Installed)
                {
                    return false;
                }
            }


            return true;
        }


        private int GetInstalledDependencyCount()
        {
            if (_dependencies == null)
                return 0;


            int count = 0;


            foreach (var dependency in _dependencies)
            {
                if (dependency.State ==
                    DependencyState.Installed)
                {
                    count++;
                }
            }


            return count;
        }


        private int GetInstalledServiceCount()
        {
            if (_services == null)
                return 0;


            int count = 0;


            foreach (var service in _services)
            {
                if (service.State ==
                    ServiceState.Installed)
                {
                    count++;
                }
            }


            return count;
        }


        /// <summary>
        /// Periodically re-evaluate installed states.
        ///
        /// Checking is comparatively expensive (assembly
        /// scanning for external packages), so it runs
        /// at most every StateCheckInterval seconds
        /// instead of every OnGUI frame.
        /// </summary>
        private void RefreshStatesIfStale()
        {
            if (IsInstalling())
                return;

            if (Time.realtimeSinceStartup -
                _lastStateCheck < StateCheckInterval)
            {
                return;
            }

            _lastStateCheck =
                Time.realtimeSinceStartup;

            RecheckAllStates();

            Repaint();
        }


        private void RecheckAllStates()
        {
            if (_dependencies != null)
            {
                foreach (var dependency in _dependencies)
                {
                    dependency.Recheck();
                }
            }

            if (_services != null)
            {
                foreach (var service in _services)
                {
                    service.Recheck();
                }
            }
        }


        /// <summary>
        /// After Refresh() rebuilds the lists, restore
        /// the Installing state on the card that matches
        /// the item currently being installed.
        /// </summary>
        private void SyncInstallingState()
        {
            if (_currentInstalling != null &&
                _dependencies != null)
            {
                foreach (var dependency in _dependencies)
                {
                    if (dependency.Id ==
                        _currentInstalling.Id)
                    {
                        dependency.State =
                            DependencyState.Installing;

                        break;
                    }
                }
            }

            if (_currentInstallingService != null &&
                _services != null)
            {
                foreach (var service in _services)
                {
                    if (service.Url ==
                        _currentInstallingService.Url)
                    {
                        service.State =
                            ServiceState.Installing;

                        break;
                    }
                }
            }
        }


        private void SetError(string message)
        {
            _errorMessage =
                string.IsNullOrEmpty(message)
                    ? "Unknown error."
                    : message;
        }

        #endregion


        #region GUI

        private void OnGUI()
        {
            InitializeStyles();

            DrawHeader();

            DrawErrorBanner();

            GUILayout.Space(10);

            _scroll = EditorGUILayout.BeginScrollView(
                _scroll,
                false,
                false
            );

            if (IsInstalling())
            {
                DrawInstallProgress();
            }

            DrawDependencies();

            GUILayout.Space(26);

            DrawServices();

            GUILayout.Space(20);

            EditorGUILayout.EndScrollView();

            DrawFooter();
        }

        #endregion


        #region Header

        private void DrawHeader()
        {
            Rect headerRect =
                GUILayoutUtility.GetRect(
                    1,
                    HeaderHeight,
                    GUILayout.ExpandWidth(true)
                );

            EditorGUI.DrawRect(
                headerRect,
                GetHeaderColor()
            );


            GUI.Label(
                new Rect(
                    headerRect.x + 20,
                    headerRect.y + 15,
                    headerRect.width - 180,
                    30
                ),
                "Essentials",
                _titleStyle
            );

            GUI.Label(
                new Rect(
                    headerRect.x + 20,
                    headerRect.y + 45,
                    headerRect.width - 180,
                    18
                ),
                "Project Setup",
                _subtitleStyle
            );


            string statusText;
            Color statusColor;

            GetOverallStatus(
                out statusText,
                out statusColor
            );

            DrawStatusPill(
                headerRect,
                statusText,
                statusColor
            );


            string counts =
                $"{GetInstalledDependencyCount()} / " +
                $"{_dependencies?.Length ?? 0} Dependencies  ·  " +
                $"{GetInstalledServiceCount()} / " +
                $"{_services?.Length ?? 0} Services";

            GUI.Label(
                new Rect(
                    headerRect.x + 20,
                    headerRect.yMax - 24,
                    headerRect.width - 40,
                    16
                ),
                counts,
                _smallLabelStyle
            );

            GUILayout.Space(12);
        }


        private void DrawStatusPill(
            Rect headerRect,
            string text,
            Color color
        )
        {
            Vector2 size =
                _pillTextStyle.CalcSize(
                    new GUIContent(text)
                );

            float width =
                size.x + 32f;

            Rect pill =
                new Rect(
                    headerRect.xMax - width - 20f,
                    headerRect.y +
                    (headerRect.height - 28f) * 0.5f,
                    width,
                    28f
                );

            DrawRoundedRect(
                pill,
                color,
                14f
            );

            GUI.Label(
                pill,
                text,
                _pillTextStyle
            );
        }


        private void GetOverallStatus(
            out string text,
            out Color color
        )
        {
            if (IsInstalling())
            {
                text = "Installing" +
                       new string(
                           '.',
                           1 + (int)(_loadingTimer * 2f) % 3
                       );

                color = GetInstallingColor();

                return;
            }


            bool dependenciesReady =
                AllDependenciesInstalled();

            int installedServices =
                GetInstalledServiceCount();

            int totalServices =
                _services?.Length ?? 0;


            if (!dependenciesReady)
            {
                text = "Setup Required";
                color = GetWarningColor();
            }
            else if (installedServices < totalServices)
            {
                text = "Ready";
                color = GetReadyColor();
            }
            else
            {
                text = "Complete";
                color = GetInstalledColor();
            }
        }

        #endregion


        #region Error Banner

        private void DrawErrorBanner()
        {
            if (string.IsNullOrEmpty(_errorMessage))
                return;

            GUILayout.Space(8);

            Rect box =
                GUILayoutUtility.GetRect(
                    1,
                    50f,
                    GUILayout.ExpandWidth(true)
                );

            DrawRoundedRect(
                box,
                GetErrorColor(),
                8f
            );

            GUI.Label(
                new Rect(
                    box.x + 14,
                    box.y + 4,
                    box.width - 76,
                    box.height - 8
                ),
                _errorMessage,
                _errorTextStyle
            );

            if (GUI.Button(
                    new Rect(
                        box.xMax - 46,
                        box.y + (box.height - 24f) * 0.5f,
                        32,
                        24
                    ),
                    new GUIContent(
                        "X",
                        "Dismiss error"
                    )
                ))
            {
                _errorMessage = null;
            }
        }

        #endregion


        #region Install Progress

        private void DrawInstallProgress()
        {
            string target =
                _currentInstalling != null
                    ? _currentInstalling.Name
                    : _currentInstallingService != null
                        ? _currentInstallingService.Name
                        : "…";

            GUILayout.Space(4);

            Rect barRect =
                GUILayoutUtility.GetRect(
                    1,
                    20f,
                    GUILayout.ExpandWidth(true)
                );

            DrawRoundedRect(
                barRect,
                GetProgressBackgroundColor(),
                10f
            );

            /*
             * Indeterminate progress: a highlighted
             * segment sweeps across the bar while the
             * Package Manager request is in flight.
             */
            float t =
                (_loadingTimer * 0.6f) % 1f;

            float segmentWidth =
                barRect.width * 0.35f;

            float x =
                barRect.x +
                (barRect.width + segmentWidth) * t -
                segmentWidth;

            DrawRoundedRect(
                new Rect(
                    x,
                    barRect.y + 3f,
                    segmentWidth,
                    barRect.height - 6f
                ),
                GetProgressHighlightColor(),
                7f
            );

            _progressTextStyle.normal.textColor =
                EditorGUIUtility.isProSkin
                    ? Color.white
                    : new Color(0.18f, 0.20f, 0.24f, 1f);

            GUI.Label(
                barRect,
                $"Installing {target}…",
                _progressTextStyle
            );

            GUILayout.Space(10);
        }

        #endregion


        #region Dependencies

        private void DrawDependencies()
        {
            DrawSectionHeader(
                "Dependencies",
                "Required packages for Essentials."
            );

            GUILayout.Space(8);

            if (_dependencies == null)
                return;

            foreach (var dependency in _dependencies)
            {
                DrawDependency(dependency);

                GUILayout.Space(6);
            }

            GUILayout.Space(6);

            bool canInstall =
                !AllDependenciesInstalled() &&
                !IsInstalling();

            GUI.enabled = canInstall;

            if (GUILayout.Button(
                    new GUIContent(
                        "Install Missing Dependencies",
                        "Installs every dependency that is not present yet."
                    ),
                    GUILayout.Height(32)
                ))
            {
                InstallMissingDependencies();
            }

            GUI.enabled = true;
        }


        private void DrawDependency(
            DependencyInfo dependency
        )
        {
            EditorGUILayout.BeginHorizontal(
                EditorStyles.helpBox,
                GUILayout.Height(
                    DependencyCardHeight
                )
            );

            bool showCheck =
                dependency.State ==
                DependencyState.Installed;

            bool showSpinner =
                dependency.State ==
                DependencyState.Installing;

            DrawStatusBadge(
                showCheck,
                showSpinner,
                DependencyCardHeight
            );

            GUILayout.Space(10);


            EditorGUILayout.BeginVertical();

            GUILayout.Space(9);

            GUILayout.Label(
                dependency.Name,
                _cardTitleStyle
            );

            GUILayout.Space(2);

            GUILayout.Label(
                GetDependencyTypeName(
                    dependency.Type
                ),
                _cardDescriptionStyle
            );

            GUILayout.Space(9);

            EditorGUILayout.EndVertical();


            GUILayout.FlexibleSpace();


            DrawDependencyAction(
                dependency
            );


            GUILayout.Space(12);

            EditorGUILayout.EndHorizontal();
        }


        private void DrawDependencyAction(
            DependencyInfo dependency
        )
        {
            if (dependency.State ==
                DependencyState.Installing)
            {
                DrawCenteredLabel(
                    DependencyCardHeight,
                    "Installing…",
                    90f
                );

                return;
            }


            if (dependency.State ==
                DependencyState.Installed)
            {
                /*
                 * Installed state intentionally has
                 * no text or button.
                 *
                 * The green check is already shown
                 * on the left.
                 */
                return;
            }


            string buttonText =
                dependency.Type ==
                DependencyType.External
                    ? "Open Page"
                    : "Install";

            string tooltip =
                dependency.Type ==
                DependencyType.External
                    ? "Opens the Asset Store page in your browser."
                    : "Installs this package via Unity Package Manager.";

            bool busy =
                IsInstalling();

            GUI.enabled = !busy;

            DrawCenteredButton(
                DependencyCardHeight,
                buttonText,
                tooltip,
                90f,
                24f,
                () => Install(dependency)
            );

            GUI.enabled = true;
        }

        #endregion


        #region Services

        private void DrawServices()
        {
            DrawSectionHeader(
                "Services",
                "Optional components that extend Essentials."
            );

            GUILayout.Space(8);

            if (_services == null ||
                _services.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "No services are available.",
                    MessageType.Info
                );

                return;
            }


            foreach (var service in _services)
            {
                DrawService(service);

                GUILayout.Space(6);
            }
        }


        private void DrawService(
            ServiceInfo service
        )
        {
            EditorGUILayout.BeginHorizontal(
                EditorStyles.helpBox,
                GUILayout.Height(
                    ServiceCardHeight
                )
            );

            bool showCheck =
                service.State ==
                ServiceState.Installed;

            bool showSpinner =
                service.State ==
                ServiceState.Installing;

            DrawStatusBadge(
                showCheck,
                showSpinner,
                ServiceCardHeight
            );

            GUILayout.Space(10);


            EditorGUILayout.BeginVertical();

            GUILayout.Space(12);

            GUILayout.Label(
                service.Name,
                _cardTitleStyle
            );

            GUILayout.Space(4);

            GUILayout.Label(
                service.Description,
                _cardDescriptionStyle
            );

            GUILayout.Space(12);

            EditorGUILayout.EndVertical();


            GUILayout.FlexibleSpace();


            DrawServiceAction(
                service
            );


            GUILayout.Space(12);

            EditorGUILayout.EndHorizontal();
        }


        private void DrawServiceAction(
            ServiceInfo service
        )
        {
            if (!AllDependenciesInstalled())
            {
                DrawCenteredLabel(
                    ServiceCardHeight,
                    "Dependencies Required",
                    125f
                );

                return;
            }


            if (service.State ==
                ServiceState.Installing)
            {
                DrawCenteredLabel(
                    ServiceCardHeight,
                    "Installing…",
                    90f
                );

                return;
            }


            if (service.State ==
                ServiceState.Installed)
            {
                /*
                 * No "Installed" text.
                 *
                 * The green check on the left
                 * represents the installed state.
                 */
                return;
            }


            bool busy =
                IsInstalling();

            GUI.enabled = !busy;

            DrawCenteredButton(
                ServiceCardHeight,
                "Install",
                "Installs this service via Unity Package Manager.",
                90f,
                24f,
                () => InstallService(service)
            );

            GUI.enabled = true;
        }

        #endregion


        #region Status Drawing

        private void DrawStatusBadge(
            bool showCheck,
            bool showSpinner,
            float cardHeight
        )
        {
            Rect column =
                GUILayoutUtility.GetRect(
                    28,
                    cardHeight,
                    GUILayout.Width(28)
                );

            Color iconColor =
                GetStateColor(
                    showCheck,
                    showSpinner
                );

            /*
             * Soft circular background tinted by state,
             * with the icon drawn on top in the same hue.
             */
            DrawCircle(
                column.center,
                GetBadgeBackgroundColor(
                    showCheck,
                    showSpinner
                ),
                12f
            );

            if (showSpinner)
            {
                DrawSpinner(
                    column,
                    iconColor
                );
            }
            else if (showCheck)
            {
                DrawCheck(
                    column,
                    iconColor
                );
            }
            else
            {
                DrawDot(
                    column,
                    iconColor
                );
            }
        }


        private static void DrawCircle(
            Vector2 center,
            Color color,
            float radius
        )
        {
            Handles.BeginGUI();

            Color previous = Handles.color;

            Handles.color = color;

            Handles.DrawSolidDisc(
                center,
                Vector3.forward,
                radius
            );

            Handles.color = previous;

            Handles.EndGUI();
        }


        private void DrawCheck(
            Rect rect,
            Color color
        )
        {
            /*
             * Drawn with Handles instead of a glyph so
             * it never depends on font support for "✓".
             */
            Vector2 center =
                rect.center;

            Vector3 top =
                new Vector3(
                    center.x - 7f,
                    center.y - 2f,
                    0f
                );

            Vector3 middle =
                new Vector3(
                    center.x - 2f,
                    center.y + 4f,
                    0f
                );

            Vector3 bottom =
                new Vector3(
                    center.x + 8f,
                    center.y - 6f,
                    0f
                );

            Handles.BeginGUI();

            Color previous = Handles.color;

            Handles.color = color;

            Handles.DrawLine(top, middle);

            Handles.DrawLine(middle, bottom);

            Handles.color = previous;

            Handles.EndGUI();
        }


        private void DrawDot(
            Rect rect,
            Color color
        )
        {
            Handles.BeginGUI();

            Color previous = Handles.color;

            Handles.color = color;

            Handles.DrawSolidDisc(
                rect.center,
                Vector3.forward,
                4f
            );

            Handles.color = previous;

            Handles.EndGUI();
        }


        private void DrawSpinner(
            Rect rect,
            Color color
        )
        {
            float angle =
                (_loadingTimer * 240f) % 360f;

            Vector3 start =
                new Vector3(
                    Mathf.Cos(
                        angle * Mathf.Deg2Rad
                    ),
                    Mathf.Sin(
                        angle * Mathf.Deg2Rad
                    ),
                    0
                );

            Handles.BeginGUI();

            Color previous = Handles.color;

            Handles.color = color;

            Handles.DrawWireArc(
                rect.center,
                Vector3.forward,
                start,
                270f,
                9f
            );

            Handles.color = previous;

            Handles.EndGUI();
        }


        #region Action Layout

        /// <summary>
        /// Vertically center a button inside a
        /// fixed-height card row.
        ///
        /// Reserves a full-height slot with GetRect, then
        /// positions the control at the slot's vertical
        /// center. This is deterministic and does not
        /// depend on GUILayout stretch behavior.
        /// </summary>
        private void DrawCenteredButton(
            float cardHeight,
            string text,
            string tooltip,
            float width,
            float height,
            Action onClick
        )
        {
            Rect slot =
                GUILayoutUtility.GetRect(
                    width,
                    cardHeight,
                    GUILayout.Width(width)
                );

            Rect buttonRect =
                new Rect(
                    slot.x,
                    slot.y +
                    (slot.height - height) * 0.5f,
                    width,
                    height
                );

            if (GUI.Button(
                    buttonRect,
                    new GUIContent(
                        text,
                        tooltip
                    )
                ))
            {
                onClick?.Invoke();
            }
        }


        private void DrawCenteredLabel(
            float cardHeight,
            string text,
            float width
        )
        {
            Rect slot =
                GUILayoutUtility.GetRect(
                    width,
                    cardHeight,
                    GUILayout.Width(width)
                );

            GUI.Label(
                new Rect(
                    slot.x,
                    slot.y,
                    width,
                    slot.height
                ),
                text,
                _actionLabelStyle
            );
        }

        #endregion


        private static void DrawRoundedRect(
            Rect rect,
            Color color,
            float radius
        )
        {
            if (rect.width < 1f ||
                rect.height < 1f)
            {
                return;
            }

            float r =
                Mathf.Min(
                    radius,
                    Mathf.Min(
                        rect.width,
                        rect.height
                    ) * 0.5f
                );

            var points =
                new List<Vector3>(28);

            AddArc(
                points,
                rect.x + r,
                rect.y + r,
                r,
                180f,
                270f
            );

            AddArc(
                points,
                rect.xMax - r,
                rect.y + r,
                r,
                270f,
                360f
            );

            AddArc(
                points,
                rect.xMax - r,
                rect.yMax - r,
                r,
                0f,
                90f
            );

            AddArc(
                points,
                rect.x + r,
                rect.yMax - r,
                r,
                90f,
                180f
            );

            Handles.BeginGUI();

            Color previous = Handles.color;

            Handles.color = color;

            Handles.DrawAAConvexPolygon(
                points.ToArray()
            );

            Handles.color = previous;

            Handles.EndGUI();
        }


        private static void AddArc(
            List<Vector3> points,
            float centerX,
            float centerY,
            float radius,
            float fromAngle,
            float toAngle
        )
        {
            const int segments = 5;

            for (int i = 0; i <= segments; i++)
            {
                float angle =
                    Mathf.Lerp(
                        fromAngle,
                        toAngle,
                        i / (float)segments
                    ) * Mathf.Deg2Rad;

                points.Add(
                    new Vector3(
                        centerX +
                        Mathf.Cos(angle) * radius,
                        centerY +
                        Mathf.Sin(angle) * radius,
                        0f
                    )
                );
            }
        }

        #endregion


        #region Footer

        private void DrawFooter()
        {
            GUILayout.Space(5);

            DrawSeparator();

            GUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();

            GUILayout.Space(18);


            int dependencies =
                GetInstalledDependencyCount();

            int totalDependencies =
                _dependencies?.Length ?? 0;


            int services =
                GetInstalledServiceCount();

            int totalServices =
                _services?.Length ?? 0;


            GUILayout.Label(
                $"{dependencies} / {totalDependencies} Dependencies   ·   " +
                $"{services} / {totalServices} Services",
                _footerStyle
            );


            GUILayout.FlexibleSpace();


            if (GUILayout.Button(
                    new GUIContent(
                        "Recheck",
                        "Re-evaluates whether each dependency and service is installed."
                    ),
                    GUILayout.Width(70),
                    GUILayout.Height(22)
                ))
            {
                RecheckAllStates();

                _lastStateCheck =
                    Time.realtimeSinceStartup;

                Repaint();
            }


            GUILayout.Space(8);


            GUILayout.Label(
                $"v{Version}",
                _footerStyle
            );


            GUILayout.Space(18);

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8);
        }

        #endregion


        #region Section

        private void DrawSectionHeader(
            string title,
            string description
        )
        {
            EditorGUILayout.BeginHorizontal();

            Rect accent =
                GUILayoutUtility.GetRect(
                    4,
                    22,
                    GUILayout.Width(4)
                );

            DrawRoundedRect(
                accent,
                GetAccentColor(),
                2f
            );

            GUILayout.Space(8);

            EditorGUILayout.BeginVertical();

            GUILayout.Label(
                title.ToUpperInvariant(),
                _sectionTitleStyle
            );

            GUILayout.Space(2);

            GUILayout.Label(
                description,
                _smallLabelStyle
            );

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }


        private void DrawSeparator()
        {
            Rect rect =
                GUILayoutUtility.GetRect(
                    1,
                    1,
                    GUILayout.ExpandWidth(true)
                );

            EditorGUI.DrawRect(
                rect,
                GetSeparatorColor()
            );
        }

        #endregion


        #region Colors

        private static Color GetHeaderColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.10f, 0.11f, 0.13f, 1f)
                : new Color(0.90f, 0.91f, 0.93f, 1f);
        }

        private static Color GetSeparatorColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.16f, 0.16f, 0.16f, 1f)
                : new Color(0.72f, 0.72f, 0.72f, 1f);
        }

        private static Color GetAccentColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.30f, 0.55f, 0.88f, 1f)
                : new Color(0.15f, 0.38f, 0.72f, 1f);
        }

        private static Color GetInstalledColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.13f, 0.55f, 0.30f, 1f)
                : new Color(0.10f, 0.48f, 0.24f, 1f);
        }

        private static Color GetNotInstalledColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.42f, 0.44f, 0.49f, 1f)
                : new Color(0.55f, 0.57f, 0.62f, 1f);
        }

        private static Color GetInstallingColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.16f, 0.42f, 0.78f, 1f)
                : new Color(0.13f, 0.36f, 0.70f, 1f);
        }

        private static Color GetReadyColor()
        {
            return GetInstallingColor();
        }

        private static Color GetWarningColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.72f, 0.42f, 0.10f, 1f)
                : new Color(0.66f, 0.38f, 0.08f, 1f);
        }

        private static Color GetErrorColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.52f, 0.16f, 0.16f, 1f)
                : new Color(0.76f, 0.30f, 0.28f, 1f);
        }

        private static Color GetProgressBackgroundColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.15f, 0.16f, 0.18f, 1f)
                : new Color(0.83f, 0.84f, 0.86f, 1f);
        }

        private static Color GetProgressHighlightColor()
        {
            return GetInstallingColor();
        }

        private static Color GetStateColor(
            bool showCheck,
            bool showSpinner
        )
        {
            if (showSpinner)
                return GetInstallingColor();

            if (showCheck)
                return GetInstalledColor();

            return GetNotInstalledColor();
        }

        private static Color GetBadgeBackgroundColor(
            bool showCheck,
            bool showSpinner
        )
        {
            if (showSpinner)
            {
                return EditorGUIUtility.isProSkin
                    ? new Color(0.12f, 0.24f, 0.44f, 1f)
                    : new Color(0.76f, 0.88f, 0.98f, 1f);
            }

            if (showCheck)
            {
                return EditorGUIUtility.isProSkin
                    ? new Color(0.12f, 0.30f, 0.18f, 1f)
                    : new Color(0.78f, 0.92f, 0.82f, 1f);
            }

            return EditorGUIUtility.isProSkin
                ? new Color(0.20f, 0.21f, 0.24f, 1f)
                : new Color(0.87f, 0.88f, 0.90f, 1f);
        }

        #endregion


        #region Service Checks

        private static bool CheckEssentialsServices()
        {
            /*
             * IMPORTANT:
             *
             * Make sure this package name matches
             * package.json in Essentials-Services.
             *
             * If the names ever drift apart,
             * FindForPackageName returns null even
             * though the package is installed, so fall
             * back to matching the git source URL in
             * the project manifest.
             */
            if (CheckPackageByName(
                    "com.sug.essentials.services"
                ))
            {
                return true;
            }

            return CheckPackageBySourceUrl(
                EServicesGit
            );
        }


        private static bool CheckPackageByName(
            string packageName
        )
        {
            try
            {
                var package =
                    UnityEditor.PackageManager.PackageInfo
                        .FindForPackageName(
                            packageName
                        );

                return package != null;
            }
            catch (Exception e)
            {
                Debug.LogException(e);

                return false;
            }
        }

        #endregion


        #region Dependency Checks

        /// <summary>
        /// Check a git dependency by name first, then by
        /// its source URL in the project manifest.
        /// </summary>
        private static bool CheckGitPackage(
            string id,
            string gitUrl
        )
        {
            if (CheckPackageByName(id))
                return true;

            return CheckPackageBySourceUrl(
                gitUrl
            );
        }


        /// <summary>
        /// Fallback detection for git packages.
        ///
        /// A git package's package.json name can differ
        /// from the expected one, which makes
        /// FindForPackageName return null even though
        /// the package is installed. Look for the source
        /// URL in Packages/manifest.json instead.
        /// </summary>
        private static bool CheckPackageBySourceUrl(
            string gitUrl
        )
        {
            try
            {
                string manifestPath =
                    Path.Combine(
                        Application.dataPath,
                        "..",
                        "Packages",
                        "manifest.json"
                    );

                if (!File.Exists(manifestPath))
                    return false;

                string manifest =
                    File.ReadAllText(manifestPath);

                /*
                 * Strip the version fragment (#1.0.0)
                 * so a package added without a pinned
                 * ref still matches.
                 */
                string needle = gitUrl;

                int hashIndex =
                    needle.IndexOf('#');

                if (hashIndex >= 0)
                {
                    needle =
                        needle.Substring(0, hashIndex);
                }

                return manifest.Contains(needle);
            }
            catch (Exception e)
            {
                Debug.LogException(e);

                return false;
            }
        }


        private static bool CheckPackageValid(
            string id,
            DependencyType type
        )
        {
            try
            {
                if (type ==
                    DependencyType.UnityPackage)
                {
                    var package =
                        UnityEditor.PackageManager.PackageInfo
                            .FindForPackageName(id);

                    return package != null;
                }


                if (type ==
                    DependencyType.External)
                {
                    foreach (
                        var assembly
                        in AppDomain.CurrentDomain
                            .GetAssemblies()
                    )
                    {
                        if (assembly.GetName().Name == id)
                        {
                            return true;
                        }
                    }

                    return false;
                }


                if (type ==
                    DependencyType.GitHub)
                {
                    var package =
                        UnityEditor.PackageManager.PackageInfo
                            .FindForPackageName(id);

                    return package != null;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }


            return false;
        }

        #endregion


        #region GUI Styles

        private void InitializeStyles()
        {
            if (_titleStyle != null)
                return;


            _titleStyle =
                new GUIStyle(
                    EditorStyles.boldLabel
                )
                {
                    fontSize = 22,
                    fontStyle = FontStyle.Bold
                };


            _subtitleStyle =
                new GUIStyle(
                    EditorStyles.label
                )
                {
                    fontSize = 12
                };


            _sectionTitleStyle =
                new GUIStyle(
                    EditorStyles.boldLabel
                )
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold
                };


            _cardTitleStyle =
                new GUIStyle(
                    EditorStyles.label
                )
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold
                };


            _cardDescriptionStyle =
                new GUIStyle(
                    EditorStyles.miniLabel
                )
                {
                    fontSize = 10,
                    wordWrap = true
                };


            _smallLabelStyle =
                new GUIStyle(
                    EditorStyles.miniLabel
                )
                {
                    fontSize = 10
                };


            _actionLabelStyle =
                new GUIStyle(
                    EditorStyles.miniLabel
                )
                {
                    fontSize = 10,
                    alignment =
                        TextAnchor.MiddleCenter
                };


            _pillTextStyle =
                new GUIStyle(
                    EditorStyles.label
                )
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal =
                    {
                        textColor = Color.white
                    }
                };


            _progressTextStyle =
                new GUIStyle(
                    EditorStyles.label
                )
                {
                    fontSize = 10,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal =
                    {
                        textColor = Color.white
                    }
                };


            _errorTextStyle =
                new GUIStyle(
                    EditorStyles.label
                )
                {
                    fontSize = 11,
                    wordWrap = true,
                    normal =
                    {
                        textColor = Color.white
                    }
                };


            _footerStyle =
                new GUIStyle(
                    EditorStyles.miniLabel
                )
                {
                    fontSize = 10
                };
        }

        #endregion


        #region Types

        private enum DependencyType
        {
            UnityPackage,
            External,
            GitHub
        }


        private enum DependencyState
        {
            NotInstalled,
            Installing,
            Installed
        }


        private enum ServiceState
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

            public DependencyState State =
                DependencyState.NotInstalled;

            private readonly
                Func<string, DependencyType, bool>
                _checker;


            public DependencyInfo(
                string name,
                string id,
                DependencyType type,
                Func<string, DependencyType, bool> checker,
                string url = null
            )
            {
                Name = name;
                Id = id;
                Type = type;
                Url = url;

                _checker = checker;

                Recheck();
            }


            public void Recheck()
            {
                try
                {
                    State =
                        _checker != null &&
                        _checker(Id, Type)
                            ? DependencyState.Installed
                            : DependencyState.NotInstalled;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);

                    State =
                        DependencyState.NotInstalled;
                }
            }
        }


        private class ServiceInfo
        {
            public string Name;
            public string Description;
            public string Url;

            public ServiceState State =
                ServiceState.NotInstalled;

            private readonly Func<bool> _checker;


            public ServiceInfo(
                string name,
                string description,
                string url,
                Func<bool> checker
            )
            {
                Name = name;
                Description = description;
                Url = url;

                _checker = checker;

                Recheck();
            }


            public void Recheck()
            {
                try
                {
                    State =
                        _checker != null &&
                        _checker()
                            ? ServiceState.Installed
                            : ServiceState.NotInstalled;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);

                    State =
                        ServiceState.NotInstalled;
                }
            }
        }

        #endregion


        #region Utility

        private static string GetDependencyTypeName(
            DependencyType type
        )
        {
            switch (type)
            {
                case DependencyType.UnityPackage:
                    return "Unity Package";

                case DependencyType.External:
                    return "External Package";

                case DependencyType.GitHub:
                    return "Git Repository";

                default:
                    return "Package";
            }
        }

        #endregion
    }
}

#endif
