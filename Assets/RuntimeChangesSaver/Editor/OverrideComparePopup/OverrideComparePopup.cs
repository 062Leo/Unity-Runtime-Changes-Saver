using UnityEditor;
using UnityEngine;

namespace RuntimeChangesSaver.Editor.OverrideComparePopup
{
    /// <summary>
    /// Popup window for comparing original and current component/transform states side-by-side.
    /// Delegates functionality to specialized helper classes.
    /// </summary>
    internal class OverrideComparePopupContent : PopupWindowContent
    {
        private readonly Component liveComponent;
        private readonly bool openedFromBrowser;
        private readonly System.Action onRefreshRequest;

        private OverrideComparePopupSnapshot snapshotHelper;
        private OverrideComparePopupInteraction interactionHelper;
        private UnityEditor.Editor leftEditor;
        private UnityEditor.Editor rightEditor;

        // Scroll state
        private float scrollNormalized;
        private float leftMaxScroll;
        private float rightMaxScroll;

        // Layout constants
        private const float MinWidth = 350f;
        private const float HeaderHeight = 24f;
        private const float FooterHeight = 40f;
        private const float MaxWindowHeight = 400f;
        private const float MinWindowHeight = 250f;

        private float targetWindowHeight = -1f;

        public OverrideComparePopupContent(Component component, bool openedFromBrowser = false, System.Action onRefreshRequest = null)
        {
            liveComponent = component;
            this.openedFromBrowser = openedFromBrowser;
            this.onRefreshRequest = onRefreshRequest;
            InitializePopup();
        }

        private void InitializePopup()
        {
            snapshotHelper = new OverrideComparePopupSnapshot(liveComponent);
            interactionHelper = new OverrideComparePopupInteraction(
                liveComponent,
                snapshotHelper.SnapshotGO,
                snapshotHelper.SnapshotComponent
            );

            CreateEditors();
        }

        private void CreateEditors()
        {
            if (snapshotHelper.SnapshotComponent)
            {
                leftEditor = UnityEditor.Editor.CreateEditor(snapshotHelper.SnapshotComponent);
                rightEditor = UnityEditor.Editor.CreateEditor(liveComponent);
            }
            else
            {
                Debug.LogWarning($"[OverrideComparePopup] Snapshot component is NULL for component '{liveComponent.GetType().Name}'");
            }
        }

        public override Vector2 GetWindowSize()
        {
            float h = targetWindowHeight < 0 ? MinWindowHeight : targetWindowHeight;
            return new Vector2(MinWidth * 2 + 6, h);
        }

        public override void OnGUI(Rect rect)
        {
            if (leftEditor == null || rightEditor == null) return;

            interactionHelper.HandleDragAndDrop(rect, editorWindow);

            // Dynamic size adjustment
            float extraSpaceNeeded = Mathf.Max(leftMaxScroll, rightMaxScroll);

            if (Event.current.type == EventType.Layout)
            {
                float desiredHeight = Mathf.Clamp(rect.height + extraSpaceNeeded, MinWindowHeight, MaxWindowHeight);

                if (Mathf.Abs(targetWindowHeight - desiredHeight) > 1f)
                {
                    targetWindowHeight = desiredHeight;
                    editorWindow.ShowAsDropDown(new Rect(editorWindow.position.position, Vector2.zero), GetWindowSize());
                }
            }

            // Scroll handling
            bool needsScrolling = (rect.height >= MaxWindowHeight - 1f) && extraSpaceNeeded > 0.5f;
            HandleMouseWheel(rect, needsScrolling);

            // Layout setup
            float scrollbarWidth = needsScrolling ? 15f : 0f;
            float columnWidth = (rect.width - scrollbarWidth - 6) * 0.5f;
            float contentHeight = rect.height - FooterHeight - HeaderHeight;

            OverrideComparePopupUI.DrawColumnHeader(new Rect(rect.x, rect.y, columnWidth, HeaderHeight), leftEditor.target, "Original");
            OverrideComparePopupUI.DrawColumnHeader(new Rect(rect.x + columnWidth + 6, rect.y, columnWidth, HeaderHeight), rightEditor.target, "Play Mode");

            Rect contentRect = new Rect(rect.x, rect.y + HeaderHeight, rect.width, contentHeight);
            GUILayout.BeginArea(contentRect);
            GUILayout.BeginHorizontal();

            OverrideComparePopupUI.DrawSynchronizedColumn(columnWidth, contentHeight, leftEditor, scrollNormalized, ref leftMaxScroll, false);
            OverrideComparePopupUI.DrawSeparator(new Rect(columnWidth, 0, 2, contentHeight));
            OverrideComparePopupUI.DrawSynchronizedColumn(columnWidth, contentHeight, rightEditor, scrollNormalized, ref rightMaxScroll, true);

            if (needsScrolling)
            {
                Rect scrollbarRect = new Rect(rect.width - 15, 0, 15, contentHeight);
                scrollNormalized = GUI.VerticalScrollbar(scrollbarRect, scrollNormalized, 0.1f, 0f, 1.0f);
            }
            else
            {
                scrollNormalized = 0f;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            DrawFooter(new Rect(rect.x, rect.y + rect.height - FooterHeight, rect.width, FooterHeight));
        }

        private void HandleMouseWheel(Rect rect, bool needsScrolling)
        {
            if (needsScrolling && rect.Contains(Event.current.mousePosition) && Event.current.type == EventType.ScrollWheel)
            {
                scrollNormalized = Mathf.Clamp01(scrollNormalized + Event.current.delta.y * 0.05f);
                Event.current.Use();
            }
        }

        private void DrawFooter(Rect rect)
        {
            GUILayout.BeginArea(rect);

            GUILayout.Space(2);
            GUILayout.BeginHorizontal();

            bool hasUnsavedChanges = Application.isPlaying && interactionHelper.HasUnsavedChanges();
            bool hasSavedEntry = interactionHelper.HasSavedEntry();
            
            GUILayout.BeginVertical();
            OverrideComparePopupUI.DrawFooter(rect, hasUnsavedChanges);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Revert to Original", GUILayout.Width(130f), GUILayout.Height(28f)))
            {
                interactionHelper.RevertToOriginal(openedFromBrowser);
                onRefreshRequest?.Invoke();
                if (!openedFromBrowser)
                {
                    editorWindow.Close();
                }
            }

            GUILayout.Space(4);

            EditorGUI.BeginDisabledGroup(!hasSavedEntry);
            if (GUILayout.Button("Revert to Saved", GUILayout.Width(130f), GUILayout.Height(28f)))
            {
                interactionHelper.RevertToSaved(openedFromBrowser);
                onRefreshRequest?.Invoke();
                if (!openedFromBrowser)
                {
                    editorWindow.Close();
                }
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(8);

            EditorGUI.BeginDisabledGroup(!hasUnsavedChanges);
            if (GUILayout.Button("Apply", GUILayout.Width(120f), GUILayout.Height(28f)))
            {
                interactionHelper.ApplyChanges(openedFromBrowser);
                onRefreshRequest?.Invoke();
                if (!openedFromBrowser)
                {
                    editorWindow.Close();
                }
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        public override void OnClose()
        {
            if (leftEditor) Object.DestroyImmediate(leftEditor);
            if (rightEditor) Object.DestroyImmediate(rightEditor);
            snapshotHelper?.Cleanup();
        }
    }
}

