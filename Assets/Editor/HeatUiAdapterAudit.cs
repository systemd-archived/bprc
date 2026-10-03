using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class HeatUiAdapterAudit
{
    public static void Run()
    {
        GameObject root = new GameObject("Heat adapter audit", typeof(RectTransform));
        try
        {
            HeatUiLegacyPanelAdapter adapter = root.AddComponent<HeatUiLegacyPanelAdapter>();
            adapter.Initialize(root.transform);
            GameObject original = new GameObject("Original button", typeof(RectTransform), typeof(UnityEngine.UI.Button));
            original.transform.SetParent(root.transform, false);
            RectTransform originalRect = original.GetComponent<RectTransform>();
            originalRect.anchoredPosition = new Vector2(125, -72);
            originalRect.sizeDelta = new Vector2(230, 55);
            UnityEngine.UI.Button button = original.GetComponent<UnityEngine.UI.Button>();
            int clicks = 0;
            button.onClick.AddListener(() => clicks++);
            button.interactable = false;
            Invoke(adapter, "StyleButtons");
            var replacement = root.GetComponentInChildren<Michsky.UI.Heat.ButtonManager>(true);
            Require(replacement != null, "Button replaced");
            RectTransform replacementRect = replacement.GetComponent<RectTransform>();
            Require(replacementRect.anchoredPosition == originalRect.anchoredPosition &&
                replacementRect.sizeDelta == originalRect.sizeDelta, "Button geometry preserved");
            Require(!replacement.isInteractable, "Disabled button state preserved");
            replacement.InvokeOnClick();
            Require(clicks == 1, "HEAT button event forwards exactly once");

            GameObject inputObject = new GameObject("Original input", typeof(RectTransform), typeof(InputField));
            inputObject.transform.SetParent(root.transform, false);
            InputField input = inputObject.GetComponent<InputField>();
            input.contentType = InputField.ContentType.IntegerNumber;
            input.characterLimit = 8;
            input.readOnly = true;
            input.interactable = false;
            Invoke(adapter, "StyleFreeControls");
            TMP_InputField heatInput = root.GetComponentInChildren<TMP_InputField>(true);
            Require(heatInput != null, "Input replaced");
            Require(heatInput.contentType == TMP_InputField.ContentType.IntegerNumber &&
                heatInput.characterValidation == TMP_InputField.CharacterValidation.Integer &&
                heatInput.characterLimit == 8 && heatInput.readOnly && !heatInput.interactable,
                "Input validation, length, read-only and disabled state preserved");
            Directory.CreateDirectory("Builds/QA");
            File.WriteAllText("Builds/QA/heat-ui-adapter.txt", "PASS: 6 adapter regression checks.\n");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void Invoke(HeatUiLegacyPanelAdapter adapter, string method)
    {
        typeof(HeatUiLegacyPanelAdapter).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(adapter, null);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }
}
