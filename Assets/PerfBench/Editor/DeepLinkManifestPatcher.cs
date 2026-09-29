using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace PerfBench.EditorTools
{
    /// <summary>
    /// Adiciona o intent-filter do esquema perfbench:// na Activity de entrada do app
    /// durante o build. Funciona tanto com Activity quanto com GameActivity, sem manter
    /// um AndroidManifest.xml customizado no projeto.
    /// </summary>
    public class DeepLinkManifestPatcher : IPostGenerateGradleAndroidProject
    {
        const string Scheme = "perfbench";
        const string AndroidNs = "http://schemas.android.com/apk/res/android";

        public int callbackOrder => 999;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string[] candidates =
            {
                Path.Combine(path, "src", "main", "AndroidManifest.xml"),
                Path.Combine(path, "..", "launcher", "src", "main", "AndroidManifest.xml")
            };
            foreach (var manifest in candidates)
                if (File.Exists(manifest) && TryPatch(manifest)) return;

            Debug.LogWarning("[PerfBench] Activity de entrada não encontrada; deeplink perfbench:// não configurado.");
        }

        static bool TryPatch(string manifestPath)
        {
            var doc = new XmlDocument();
            doc.Load(manifestPath);
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("android", AndroidNs);

            var activities = doc.SelectNodes("/manifest/application/activity", ns);
            if (activities == null) return false;
            foreach (XmlElement act in activities)
            {
                if (act.SelectSingleNode("intent-filter/category[@android:name='android.intent.category.LAUNCHER']", ns) == null)
                    continue;
                if (act.SelectSingleNode($"intent-filter/data[@android:scheme='{Scheme}']", ns) != null)
                    return true;

                var filter = doc.CreateElement("intent-filter");
                filter.AppendChild(El(doc, "action", "android.intent.action.VIEW"));
                filter.AppendChild(El(doc, "category", "android.intent.category.DEFAULT"));
                filter.AppendChild(El(doc, "category", "android.intent.category.BROWSABLE"));
                var data = doc.CreateElement("data");
                data.SetAttribute("scheme", AndroidNs, Scheme);
                filter.AppendChild(data);
                act.AppendChild(filter);
                doc.Save(manifestPath);
                Debug.Log($"[PerfBench] Deeplink {Scheme}:// adicionado em {manifestPath}");
                return true;
            }
            return false;
        }

        static XmlElement El(XmlDocument doc, string tag, string name)
        {
            var e = doc.CreateElement(tag);
            e.SetAttribute("name", AndroidNs, name);
            return e;
        }
    }
}
