using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;

public class ComponentSearchTool : EditorWindow
{
    // Search Parameters
    private string componentName = "";
    private string[] componentNames = new string[1] { "" };
    private bool caseSensitive = false;
    private bool includeInactive = true;

    // Search Results
    private List<GameObject> foundObjects = new List<GameObject>();
    private Vector2 scrollPos;

    // Progress Bar Control
    private bool isSearching = false;
    private float searchProgress = 0f;
    private bool cancelSearch = false;

    // GUI Styles
    private GUIStyle headerStyle;
    private GUIStyle buttonStyle;

    [MenuItem("Tools/Component Search Tool")]
    public static void ShowWindow()
    {
        GetWindow<ComponentSearchTool>("Component Search");
    }

    private void OnEnable()
    {
        // Initialize GUI Styles here without accessing GUI.skin
        headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14
        };

        buttonStyle = new GUIStyle(EditorStyles.miniButton)
        {
            margin = new RectOffset(5, 5, 5, 5)
        };
    }

    private void OnGUI()
    {
        GUILayout.Label("Component Search Tool", headerStyle);
        EditorGUILayout.Space();

        // Search Parameters Section
        DrawSearchParameters();

        EditorGUILayout.Space();

        // Search Button or Cancel Button
        if (!isSearching)
        {
            if (GUILayout.Button("Search", buttonStyle))
            {
                if (ValidateInput())
                {
                    foundObjects.Clear();
                    SearchForComponents();
                }
            }
        }
        else
        {
            EditorGUI.BeginDisabledGroup(true);
            GUILayout.Button("Searching...", buttonStyle);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space();
            if (GUILayout.Button("Cancel", buttonStyle))
            {
                cancelSearch = true;
            }
        }

        EditorGUILayout.Space();
        GUILayout.Label($"Found {foundObjects.Count} GameObject(s):", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Display Search Results
        DrawSearchResults();

        EditorGUILayout.Space();

        // Export Options
        if (foundObjects.Count > 0)
        {
            if (GUILayout.Button("Copy Results to Clipboard", buttonStyle))
            {
                CopyResultsToClipboard();
            }

            if (GUILayout.Button("Export Results to Text File", buttonStyle))
            {
                ExportResultsToTextFile();
            }
        }

        // Progress Bar Display
        if (isSearching)
        {
            DrawProgressBar();
        }
    }

    private void DrawSearchParameters()
    {
        EditorGUILayout.LabelField("Search Parameters", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Component Names Input
        GUILayout.BeginVertical("box");
        GUILayout.Label("Component Names (Comma Separated):");
        componentName = EditorGUILayout.TextField("Components", componentName);
        GUILayout.EndVertical();

        EditorGUILayout.Space();

        // Search Options
        GUILayout.BeginHorizontal();
        caseSensitive = EditorGUILayout.Toggle("Case Sensitive", caseSensitive);
        includeInactive = EditorGUILayout.Toggle("Include Inactive", includeInactive);
        GUILayout.EndHorizontal();
    }

    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(componentName))
        {
            EditorUtility.DisplayDialog("Input Error", "Please enter at least one component name.", "OK");
            return false;
        }
        return true;
    }

    private void SearchForComponents()
    {
        isSearching = true;
        cancelSearch = false;
        foundObjects.Clear();

        // Split component names by comma and trim spaces
        componentNames = componentName.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(name => name.Trim())
                                     .ToArray();

        // Start the search on the next editor update
        EditorApplication.update += PerformSearch;
    }

    private void PerformSearch()
    {
        // Find all root GameObjects in the active scene
        var rootObjects = GetRootGameObjects();

        int total = rootObjects.Length;
        int processed = 0;

        foreach (var root in rootObjects)
        {
            if (cancelSearch)
            {
                EditorUtility.ClearProgressBar();
                isSearching = false;
                EditorApplication.update -= PerformSearch;
                Repaint();
                return;
            }

            SearchRecursive(root);

            processed++;
            searchProgress = (float)processed / total;
            EditorUtility.DisplayProgressBar("Searching for Components", $"Processing {processed}/{total} GameObjects...", searchProgress);
        }

        EditorUtility.ClearProgressBar();
        isSearching = false;
        EditorApplication.update -= PerformSearch;
        Repaint();

        Debug.Log($"Search Completed. Found {foundObjects.Count} GameObject(s) with specified component(s).");
    }

    private GameObject[] GetRootGameObjects()
    {
        // Depending on Unity version, use appropriate method
#if UNITY_2020_1_OR_NEWER
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
#else
        return UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().GetRootGameObjects();
#endif
    }

    private void SearchRecursive(GameObject obj)
    {
        if (foundObjects.Contains(obj))
            return;

        // Get components based on includeInactive flag
        Component[] components = obj.GetComponents<Component>();

        foreach (string compName in componentNames)
        {
            var match = components.FirstOrDefault(comp =>
                comp != null &&
                (caseSensitive
                    ? comp.GetType().Name.Equals(compName, StringComparison.Ordinal)
                    : comp.GetType().Name.Equals(compName, StringComparison.OrdinalIgnoreCase)));

            if (match != null)
            {
                foundObjects.Add(obj);
                break;
            }
        }

        // Optionally, search in children recursively if needed
        if (!includeInactive)
        {
            foreach (Transform child in obj.transform)
            {
                if (child.gameObject.activeInHierarchy)
                {
                    SearchRecursive(child.gameObject);
                }
            }
        }
        else
        {
            foreach (Transform child in obj.transform)
            {
                SearchRecursive(child.gameObject);
            }
        }
    }

    private void DrawSearchResults()
    {
        if (foundObjects.Count == 0)
        {
            GUILayout.Label("No results to display.");
            return;
        }

        // Begin scroll view
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(300));

        foreach (GameObject obj in foundObjects)
        {
            if (GUILayout.Button(GetFullPath(obj), EditorStyles.label))
            {
                Selection.activeGameObject = obj;
                EditorGUIUtility.PingObject(obj);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private string GetFullPath(GameObject obj)
    {
        string path = "/" + obj.name;
        while (obj.transform.parent != null)
        {
            obj = obj.transform.parent.gameObject;
            path = "/" + obj.name + path;
        }
        return path;
    }

    private void CopyResultsToClipboard()
    {
        if (foundObjects.Count == 0)
            return;

        string clipboardText = string.Join("\n", foundObjects.Select(obj => GetFullPath(obj)));
        EditorGUIUtility.systemCopyBuffer = clipboardText;
        EditorUtility.DisplayDialog("Copied", "Search results copied to clipboard.", "OK");
    }

    private void ExportResultsToTextFile()
    {
        if (foundObjects.Count == 0)
            return;

        string path = EditorUtility.SaveFilePanel("Export Search Results", "", "ComponentSearchResults.txt", "txt");
        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            System.IO.File.WriteAllLines(path, foundObjects.Select(obj => GetFullPath(obj)));
            EditorUtility.DisplayDialog("Export Successful", $"Results exported to:\n{path}", "OK");
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("Export Failed", $"An error occurred:\n{e.Message}", "OK");
        }
    }

    private void DrawProgressBar()
    {
        // Display a custom progress bar within the window
        GUILayout.BeginHorizontal();
        GUILayout.Label("Searching...", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        GUILayout.Label($"{Mathf.RoundToInt(searchProgress * 100)}%");
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        Rect rect = GUILayoutUtility.GetRect(18, 18, "TextField");
        EditorGUI.ProgressBar(rect, searchProgress, "Searching...");
        GUILayout.EndHorizontal();
    }
}

