using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NocturneAccess.Core
{
    /// <summary>Small helpers for describing Unity UI objects in logs.</summary>
    internal static class UiUtil
    {
        private const int MaxDepth = 16;
        private const int MaxLoggedChars = 600;

        /// <summary>Whether a UI component is shown (its object active in the hierarchy). False for null or destroyed objects.</summary>
        internal static bool IsVisible(Component component)
        {
            try
            {
                return component != null && component.gameObject.activeInHierarchy;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Whether a game object is shown (active in the hierarchy). False for null or destroyed objects.</summary>
        internal static bool IsVisible(GameObject obj)
        {
            try
            {
                return obj != null && obj.activeInHierarchy;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>The cleaned text of the TextMeshPro component on a shown object, else "" (several readers, 0.0.67).</summary>
        internal static string TextOf(Transform t)
        {
            if (t == null || !t.gameObject.activeInHierarchy)
                return "";
            Il2CppTMPro.TMP_Text text = t.GetComponent<Il2CppTMPro.TMP_Text>();
            return text == null ? "" : TextUtil.Clean(text.text);
        }

        /// <summary>The nearest ancestor named name (the transform itself included), at most 12 levels up, or null.</summary>
        internal static Transform FindAncestor(Transform t, string name)
        {
            for (int depth = 0; t != null && depth < 12; depth++, t = t.parent)
            {
                if (t.name == name)
                    return t;
            }
            return null;
        }

        /// <summary>Hierarchy path such as "Canvas/mesWindow/mes_textTM", root first.</summary>
        internal static string PathOf(Transform transform)
        {
            if (transform == null)
                return "(null)";

            var parts = new List<string>();
            Transform t = transform;
            for (int depth = 0; t != null && depth < MaxDepth; depth++)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            if (t != null)
                parts.Add("...");

            parts.Reverse();
            return string.Join("/", parts);
        }

        /// <summary>Keeps a logged string on one line: escapes line breaks and tabs, truncates very long text.</summary>
        internal static string Escape(string text)
        {
            if (text == null)
                return "(null)";

            var sb = new StringBuilder(text.Length + 8);
            foreach (char c in text)
            {
                switch (c)
                {
                    case '\n': sb.Append("\\n"); break;
                    case '\r': break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }

            if (sb.Length > MaxLoggedChars)
            {
                sb.Length = MaxLoggedChars;
                sb.Append("...(truncated)");
            }
            return sb.ToString();
        }
    }
}
