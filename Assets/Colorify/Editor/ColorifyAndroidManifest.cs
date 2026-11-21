#if UNITY_ANDROID
// Solo con la plataforma Android activa (el interfaz es del módulo de Android de Unity)
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEngine;

namespace Colorify.EditorTools
{
    // Builds de Android: con la orientación fija en vertical, Google Play deduce que android.hardware.screen.portrait
    // es obligatoria (Unity no la declara). Google Play avisa de que eso deja fuera tablets, Chromebooks, Google Play Games en PC y
    // Android XR. El juego funciona igual en esos dispositivos (se ve en vertical), así que se marca como opcional
    // (android:required="false") de forma explícita en el manifiesto que genera Unity, antes de que Gradle compile.
    public class ColorifyAndroidManifest : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;

        private const string Feature = "android.hardware.screen.portrait";

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // path = carpeta unityLibrary; el launcher está al lado
            string[] manifests =
            {
                Path.Combine(path, "src", "main", "AndroidManifest.xml"),
                Path.Combine(path, "..", "launcher", "src", "main", "AndroidManifest.xml"),
            };
            foreach (var file in manifests)
            {
                if (!File.Exists(file)) continue;
                string xml = File.ReadAllText(file);
                var tag = new Regex("<uses-feature[^>]*android:name=\"" + Regex.Escape(Feature) + "\"[^>]*/>");
                string fixedXml;
                if (tag.IsMatch(xml))
                    fixedXml = tag.Replace(xml, m =>
                    {
                        string t = Regex.Replace(m.Value, "\\s*android:required=\"[^\"]*\"", "");
                        return t.Replace("/>", " android:required=\"false\" />");
                    });
                else if (xml.Contains("android:screenOrientation=\"portrait\"") && xml.Contains("<application"))
                    // Unity no la declara, pero Play la deduce de la orientación vertical: hay que declararla opcional
                    fixedXml = xml.Replace("<application", "<uses-feature android:name=\"" + Feature + "\" android:required=\"false\" />\n  <application");
                else continue;
                if (fixedXml == xml) continue;
                File.WriteAllText(file, fixedXml);
                Debug.Log($"[AndroidManifest] {Feature} marcada como opcional en {file}");
            }
        }
    }
}
#endif
