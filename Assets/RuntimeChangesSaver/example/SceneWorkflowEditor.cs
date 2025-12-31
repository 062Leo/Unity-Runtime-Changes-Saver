// using UnityEditor;
// using UnityEditor.SceneManagement;
// using UnityEngine;
// using UnityEngine.SceneManagement;

// [InitializeOnLoad]
// public static class SceneWorkflowEditor
// {
//     private const string MainScenePath = "Assets/Scenes/MainScene.unity";
//     private const string TestScenePath = "Assets/Scenes/TestScene.unity";

//     private const string StateKey = "WF_State";
//     private const string TimerKey = "WF_Timer";

//     static SceneWorkflowEditor()
//     {
//         EditorApplication.playModeStateChanged += OnPlayModeChanged;
//         EditorApplication.update += OnUpdate;
//     }

//     private static void OnPlayModeChanged(PlayModeStateChange state)
//     {
//         // 1. Erkennen, dass Play-Mode beendet wurde und wir WIRKLICH wieder im Edit-Mode sind
//         if (state == PlayModeStateChange.EnteredEditMode)
//         {
//             // Sicherstellen, dass wir aus der TestScene kamen (optional, je nach Wunsch)
//             // Hier starten wir den Prozess für die MainScene
//             if (SceneManager.GetActiveScene().name == "MainScene")
//             {
//                 // delayCall wartet, bis der Editor diesen Frame/Zyklus beendet hat
//                 EditorApplication.delayCall += () => TriggerSavePopup(true);
//             }
//         }
//     }

//     private static void OnUpdate()
//     {
//         int state = SessionState.GetInt(StateKey, 0);
//         if (state == 0) return;

//         // Schritt 2: 0.5 Sekunden warten nach dem ersten Popup
//         if (state == 2)
//         {
//             float waitStart = SessionState.GetFloat(TimerKey, 0);
//             if (EditorApplication.timeSinceStartup - waitStart >= 0.5f)
//             {
//                 SessionState.SetInt(StateKey, 0); // Timer stoppen
//                 TriggerSwitchPopup();
//             }
//         }
//     }

//     private static void TriggerSavePopup(bool sequenceNext)
//     {
//         // Ein zweiter delayCall erhöht die Sicherheit bei extrem langsamen Projekten
//         EditorApplication.delayCall += () =>
//         {
//             if (EditorApplication.isUpdating || EditorApplication.isCompiling)
//             {
//                 EditorApplication.delayCall += () => TriggerSavePopup(sequenceNext);
//                 return;
//             }

//             int option = EditorUtility.DisplayDialogComplex(
//                 "Szene speichern?",
//                 "Möchten Sie die Änderungen speichern? (Szene: " + SceneManager.GetActiveScene().name + ")",
//                 "Save", "Discard", "");

//             if (option == 0) // Save
//             {
//                 EditorSceneManager.SaveOpenScenes();
//             }

//             if (sequenceNext)
//             {
//                 SessionState.SetFloat(TimerKey, (float)EditorApplication.timeSinceStartup);
//                 SessionState.SetInt(StateKey, 2); // Starte Timer für Switch-Popup
//             }
//         };
//     }

//     private static void TriggerSwitchPopup()
//     {
//         bool proceed = EditorUtility.DisplayDialog(
//             "Szenenwechsel",
//             "Möchten Sie zur TestScene wechseln?",
//             "Ja", "Abbrechen");

//         if (proceed)
//         {
//             // Szene laden
//             SceneAsset targetScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath);
//             if (targetScene != null)
//             {
//                 EditorSceneManager.OpenScene(TestScenePath);
//                 // Sobald die Szene offen ist, rufen wir das Save-Popup erneut auf
//                 EditorApplication.delayCall += () => TriggerSavePopup(false);
//             }
//             else
//             {
//                 Debug.LogError("Szene nicht gefunden unter: " + TestScenePath);
//             }
//         }
//     }
// }