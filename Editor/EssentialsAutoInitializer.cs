#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;


namespace SUG.Essentials.Editor
{
    [InitializeOnLoad]
    public static class EssentialsAutoInitializer
    {

        /*
         * EditorPrefs is global to the whole Editor
         * installation, NOT per project.
         *
         * A bare key would only ever trigger the setup
         * window once on the entire machine, so every
         * other project that installs this package
         * would silently skip the auto-open. Scope the
         * key with the project path instead.
         */
        private const string KeyPrefix =
            "Essentials.Initialized";

        private static readonly string Key =
            KeyPrefix + "." + Application.dataPath;


        static EssentialsAutoInitializer()
        {

            EditorApplication.delayCall += () =>
            {

                if (EditorPrefs.GetBool(Key))
                    return;


                EditorPrefs.SetBool(
                    Key,
                    true
                );


                EssentialsInitializerWindow.Open();

            };

        }

    }
}

#endif