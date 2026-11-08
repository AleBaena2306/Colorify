using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using KovusPride.Publishing;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

// Kovu's Pride · Publicación en portales web. Cada juego declara sus menús (p. ej. COLORIFY > Build > CrazyGames)
// y llama a KovusPridePublish.Build. Una build por plataforma, sin tocar nada a mano:
//   1. Comprueba que los scripts están compilados y que la plataforma es Web (si no, ofrece cambiarla).
//   2. Compila con la plantilla KovusPridePortal y el símbolo KP_STORE_<PLATAFORMA> (solo para esa build; no cambia
//      los ajustes del proyecto ni obliga a recompilar el Editor).
//   3. Añade el SDK de la plataforma al index.html y copia kp-platform.js (y la configuración de Playgama).
//   4. Comprueba los requisitos de la plataforma (tamaño, número de archivos, descarga inicial, rutas relativas).
//      Si algo incumple un requisito obligatorio, no genera el zip.
//   5. Genera el zip en <proyecto>/Builds/Publish/<plataforma>/ y apunta el resultado en Publishing/history.md.
//   6. Sube o prepara la subida: itch.io con butler (si está instalado); Playgama la sube Claude por MCP; en
//      CrazyGames y GameDistribution abre la página de subida y la carpeta del zip.
// La configuración del juego está en <proyecto>/Publishing/publish.json (se crea con valores por defecto).
// Los rechazos de cada portal se apuntan en Publishing/rejections.md para ir mejorando estas comprobaciones.
public static class KovusPridePublish
{
    [Serializable]
    public class Settings
    {
        public string gameName = "";
        public string gameDistributionGameId = "";   // «Game ID» del panel de GameDistribution (pestaña Upload)
        public string itchTarget = "";               // usuario/juego:html5, p. ej. alebaena/colorify:html5
        public string butlerPath = "";               // vacío = buscar «butler» en el PATH
        public float interstitialMinSeconds = 180f;
    }

    private class Profile
    {
        public Store store;
        public string name, define, folder, uploadUrl, notes;
        public string head;                          // lo que se añade al <head> del index.html
        public WebGLCompressionFormat compression = WebGLCompressionFormat.Brotli;
        public bool decompressionFallback = true;    // funciona aunque el servidor no envíe Content-Encoding
        public long maxTotalBytes = 250L << 20, maxInitialBytes = 50L << 20, warnInitialBytes = 20L << 20;
        public int maxFiles = 1500;
    }

    private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    private static string PublishingDir => Path.Combine(ProjectRoot, "Publishing");
    private static string SettingsPath => Path.Combine(PublishingDir, "publish.json");
    private const string TemplateName = "PROJECT:KovusPridePortal";
    private static string AdapterPath => Path.Combine(Application.dataPath, "KovusPride", "Publishing", "Templates~", "kp-platform.js");

    private static Profile GetProfile(Store store, Settings s)
    {
        switch (store)
        {
            case Store.CrazyGames:
                return new Profile
                {
                    store = store, name = "CrazyGames", define = "KP_STORE_CRAZYGAMES", folder = "crazygames",
                    uploadUrl = "https://developer.crazygames.com/",
                    head = "<script src=\"https://sdk.crazygames.com/crazygames-sdk-v3.js\"></script>\n  <script>window.KP_STORE = 'crazygames';</script>",
                    notes = "Basic Launch: sin anuncios (el SDK los desactiva) y sin inicio de sesión externo. Sube todos los archivos de la carpeta (o el zip).",
                };
            case Store.Playgama:
                return new Profile
                {
                    store = store, name = "Playgama", define = "KP_STORE_PLAYGAMA", folder = "playgama",
                    uploadUrl = "https://developer.playgama.com/",
                    head = "<script src=\"https://bridge.playgama.com/v1/stable/playgama-bridge.js\"></script>\n  <script>window.KP_STORE = 'playgama';</script>",
                    notes = "Claude puede subir el zip con el servidor MCP de Playgama (start_archive_upload → confirm_archive_upload).",
                };
            case Store.GameDistribution:
                string id = (s.gameDistributionGameId ?? "").Trim().Replace("'", "");
                return new Profile
                {
                    store = store, name = "GameDistribution", define = "KP_STORE_GAMEDISTRIBUTION", folder = "gamedistribution",
                    uploadUrl = "https://developer.gamedistribution.com/",
                    head = "<script>\n    window.KP_STORE = 'gamedistribution';\n    window.__kpGdQueue = [];\n" +
                           "    window['GD_OPTIONS'] = { gameId: '" + id + "', onEvent: function (ev) {\n" +
                           "      if (window.__kpGdEvent) window.__kpGdEvent(ev); else window.__kpGdQueue.push(ev); } };\n" +
                           "  </script>\n  <script src=\"https://html5.api.gamedistribution.com/main.min.js\"></script>",
                    notes = "Los anuncios se prueban desde la pestaña Upload del panel de GameDistribution (iframe de prueba).",
                };
            default:
                return new Profile
                {
                    store = Store.Itch, name = "itch.io", define = "KP_STORE_ITCH", folder = "itch",
                    uploadUrl = "https://itch.io/game/new",
                    head = "<script>window.KP_STORE = 'itch';</script>",
                    maxFiles = 1000, maxTotalBytes = 500L << 20, maxInitialBytes = 500L << 20, warnInitialBytes = 50L << 20,
                    notes = "En la página del juego en itch.io: Kind of project = HTML y marcar el canal como «This file will be played in the browser» (solo la primera vez).",
                };
        }
    }

    // ---------------- Menú ----------------

    public static void BuildAll(string gameName)
    {
        foreach (Store s in new[] { Store.CrazyGames, Store.Playgama, Store.GameDistribution, Store.Itch })
            if (!Build(s, gameName, askAtEnd: false)) return;
        EditorUtility.RevealInFinder(Path.Combine(ProjectRoot, "Builds", "Publish"));
    }

    public static bool Build(Store store, string gameName, bool askAtEnd = true)
    {
        Settings settings = LoadSettings(gameName);
        Profile p = GetProfile(store, settings);
        string title = $"{gameName} · Build {p.name}";

        if (!ScriptsUpToDate(title)) return false;
        if (!EnsureWebPlatform(title)) return false;
        if (!File.Exists(AdapterPath)) { Fail(title, "Falta Assets/KovusPride/Publishing/Templates~/kp-platform.js."); return false; }
        if (store == Store.GameDistribution && string.IsNullOrWhiteSpace(settings.gameDistributionGameId))
        {
            Fail(title, "Falta el Game ID de GameDistribution.\n\nCópialo del panel de GameDistribution (pestaña Upload) en Publishing/publish.json → gameDistributionGameId.");
            EditorUtility.RevealInFinder(SettingsPath);
            return false;
        }

        string version = PlayerSettings.bundleVersion;
        string outDir = Path.Combine(ProjectRoot, "Builds", "Publish", p.folder, "web");
        string zipPath = Path.Combine(ProjectRoot, "Builds", "Publish", p.folder, $"{Slug(gameName)}-{p.folder}-{version}.zip");

        var snapshot = new SettingsSnapshot();
        BuildReport report;
        try
        {
            PlayerSettings.WebGL.template = TemplateName;
            PlayerSettings.WebGL.compressionFormat = p.compression;
            PlayerSettings.WebGL.decompressionFallback = p.decompressionFallback;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);
            EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.DXT;

            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(sc => sc.enabled).Select(sc => sc.path).ToArray(),
                locationPathName = outDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
                extraScriptingDefines = new[] { p.define },
            };
            Debug.Log($"[Publish] Compilando {gameName} {version} para {p.name} en {outDir}…");
            report = BuildPipeline.BuildPlayer(options);
        }
        finally { snapshot.Restore(); }

        if (report.summary.result != BuildResult.Succeeded)
        {
            Log(p, version, "ERROR DE COMPILACIÓN", 0, 0, 0, $"{report.summary.totalErrors} errores en la consola");
            Fail(title, $"La compilación ha fallado ({report.summary.totalErrors} errores). Revisa la consola.");
            return false;
        }

        // Plantilla: SDK de la plataforma, adaptador y configuración
        string index = Path.Combine(outDir, "index.html");
        string html = File.ReadAllText(index);
        if (!html.Contains("<!-- KP_STORE_HEAD -->")) { Fail(title, "El index.html no viene de la plantilla KovusPridePortal."); return false; }
        File.WriteAllText(index, html.Replace("<!-- KP_STORE_HEAD -->", p.head), new UTF8Encoding(false));
        File.Copy(AdapterPath, Path.Combine(outDir, "kp-platform.js"), true);
        if (store == Store.Playgama)
            File.WriteAllText(Path.Combine(outDir, "playgama-bridge-config.json"),
                "{\n  \"advertisement\": {\n    \"interstitial\": { \"minimumDelay\": " + Mathf.RoundToInt(settings.interstitialMinSeconds) + " }\n  }\n}\n",
                new UTF8Encoding(false));

        // Comprobaciones
        var problems = new List<string>();
        var warnings = new List<string>();
        var files = Directory.GetFiles(outDir, "*", SearchOption.AllDirectories);
        long total = files.Sum(f => new FileInfo(f).Length);
        long initial = files.Where(f => f.Replace('\\', '/').Contains("/Build/")).Sum(f => new FileInfo(f).Length);
        if (files.Length > p.maxFiles) problems.Add($"{files.Length} archivos (máximo {p.maxFiles}).");
        if (total > p.maxTotalBytes) problems.Add($"Tamaño total {Mb(total)} (máximo {Mb(p.maxTotalBytes)}).");
        if (initial > p.maxInitialBytes) problems.Add($"Descarga inicial {Mb(initial)} (máximo {Mb(p.maxInitialBytes)}).");
        else if (initial > p.warnInitialBytes) warnings.Add($"Descarga inicial {Mb(initial)}: por encima de {Mb(p.warnInitialBytes)} (en CrazyGames, sin portada móvil).");
        string final = File.ReadAllText(index);
        foreach (var src in new[] { "src=\"/", "href=\"/", "url(/" })
            if (final.Contains(src)) problems.Add($"Ruta absoluta en index.html ({src}…): deben ser relativas.");
        if (!Directory.Exists(Path.Combine(outDir, "Build"))) problems.Add("No hay carpeta Build/.");

        if (problems.Count > 0)
        {
            Log(p, version, "NO CUMPLE", total, initial, files.Length, string.Join(" ", problems));
            Fail(title, "La build no cumple los requisitos de " + p.name + ":\n\n· " + string.Join("\n· ", problems) +
                        "\n\nNo se ha generado el zip.");
            return false;
        }

        if (File.Exists(zipPath)) File.Delete(zipPath);
        Zip(outDir, zipPath);
        Log(p, version, "LISTA", total, initial, files.Length, string.Join(" ", warnings));
        Debug.Log($"[Publish] {p.name} lista: {zipPath} · {Mb(total)} · descarga inicial {Mb(initial)} · {files.Length} archivos." +
                  (warnings.Count > 0 ? "\n· " + string.Join("\n· ", warnings) : ""));

        Upload(p, settings, outDir, zipPath, version, askAtEnd);
        return true;
    }

    // ---------------- Subida ----------------

    private static void Upload(Profile p, Settings s, string outDir, string zipPath, string version, bool ask)
    {
        if (p.store == Store.Itch && !string.IsNullOrWhiteSpace(s.itchTarget))
        {
            string butler = FindButler(s.butlerPath);
            if (butler != null)
            {
                if (!ask || EditorUtility.DisplayDialog("itch.io", $"¿Subir la versión {version} a {s.itchTarget} con butler?", "Subir", "Ahora no"))
                {
                    int code = Run(butler, $"push \"{outDir}\" {s.itchTarget} --userversion {version}", out string output);
                    Debug.Log("[Publish] butler: " + output);
                    if (code == 0) { Log(p, version, "SUBIDA a itch.io", 0, 0, 0, s.itchTarget); return; }
                    Debug.LogError("[Publish] butler ha fallado. Si es la primera vez, ejecuta «butler login» en una terminal.");
                }
                EditorUtility.RevealInFinder(zipPath);
                return;
            }
            Debug.LogWarning("[Publish] No encuentro butler. Instálalo desde https://itchio.itch.io/butler y ejecuta «butler login» una vez.");
        }

        EditorUtility.RevealInFinder(zipPath);
        if (ask && EditorUtility.DisplayDialog($"{p.name} · lista para subir",
                $"Zip preparado:\n{zipPath}\n\n{p.notes}\n\n¿Abrir el portal de {p.name}?", "Abrir", "Cerrar"))
            Application.OpenURL(p.uploadUrl);
    }

    private static string FindButler(string configured)
    {
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
        try { return Run("butler", "--version", out _) == 0 ? "butler" : null; }
        catch (Exception) { return null; }
    }

    private static int Run(string exe, string args, out string output)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            WorkingDirectory = ProjectRoot,
        };
        using (var proc = Process.Start(psi))
        {
            string o = proc.StandardOutput.ReadToEnd(), e = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            output = (o + "\n" + e).Trim();
            return proc.ExitCode;
        }
    }

    // ---------------- Utilidades ----------------

    private static Settings LoadSettings(string gameName)
    {
        Directory.CreateDirectory(PublishingDir);
        Settings s = null;
        if (File.Exists(SettingsPath))
            try { s = JsonUtility.FromJson<Settings>(File.ReadAllText(SettingsPath)); } catch (Exception) { }
        if (s == null)
        {
            s = new Settings { gameName = gameName };
            File.WriteAllText(SettingsPath, JsonUtility.ToJson(s, true), new UTF8Encoding(false));
            Debug.Log("[Publish] Creado " + SettingsPath + " (rellena el Game ID de GameDistribution y el destino de itch.io).");
        }
        if (string.IsNullOrEmpty(s.gameName)) s.gameName = gameName;
        string rejections = Path.Combine(PublishingDir, "rejections.md");
        if (!File.Exists(rejections))
            File.WriteAllText(rejections,
                "# Rechazos y avisos de los portales\n\n" +
                "Una línea por rechazo o aviso de QA: fecha · plataforma · versión · qué dijeron · qué se cambió en el sistema.\n" +
                "Cada caso nuevo se convierte en una comprobación de KovusPridePublish para que no se repita.\n\n", new UTF8Encoding(false));
        return s;
    }

    private static void Log(Profile p, string version, string result, long total, long initial, int files, string detail)
    {
        string path = Path.Combine(PublishingDir, "history.md");
        if (!File.Exists(path)) File.WriteAllText(path, "# Historial de builds para portales\n\n", new UTF8Encoding(false));
        string line = $"- {DateTime.Now:yyyy-MM-dd HH:mm} · {p.name} · {version} · {result}" +
                      (total > 0 ? $" · {Mb(total)}, inicial {Mb(initial)}, {files} archivos" : "") +
                      (string.IsNullOrEmpty(detail) ? "" : " · " + detail) + "\n";
        File.AppendAllText(path, line, new UTF8Encoding(false));
    }

    // Zip con rutas «/» (algunos portales no aceptan las «\» de Windows) y el index.html en la raíz.
    // Escritor propio sin compresión (método «store»): los archivos grandes de Unity ya van comprimidos, y así no
    // depende de System.IO.Compression, que según la configuración del Editor no está disponible.
    private static void Zip(string dir, string zipPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(zipPath));
        var central = new MemoryStream();
        int count = 0;
        using (var fs = new FileStream(zipPath, FileMode.Create))
        using (var w = new BinaryWriter(fs))
        {
            foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
            {
                string name = f.Substring(dir.Length).TrimStart('\\', '/').Replace('\\', '/');
                byte[] nameBytes = Encoding.UTF8.GetBytes(name);
                byte[] data = File.ReadAllBytes(f);
                uint crc = Crc32(data);
                uint offset = (uint)fs.Position;
                DateTime t = File.GetLastWriteTime(f);
                ushort dosTime = (ushort)((t.Hour << 11) | (t.Minute << 5) | (t.Second / 2));
                ushort dosDate = (ushort)(((Math.Max(t.Year, 1980) - 1980) << 9) | (t.Month << 5) | t.Day);

                w.Write(0x04034b50u); w.Write((ushort)20); w.Write((ushort)0x0800); w.Write((ushort)0);
                w.Write(dosTime); w.Write(dosDate); w.Write(crc); w.Write((uint)data.Length); w.Write((uint)data.Length);
                w.Write((ushort)nameBytes.Length); w.Write((ushort)0); w.Write(nameBytes); w.Write(data);

                var c = new BinaryWriter(central);
                c.Write(0x02014b50u); c.Write((ushort)20); c.Write((ushort)20); c.Write((ushort)0x0800); c.Write((ushort)0);
                c.Write(dosTime); c.Write(dosDate); c.Write(crc); c.Write((uint)data.Length); c.Write((uint)data.Length);
                c.Write((ushort)nameBytes.Length); c.Write((ushort)0); c.Write((ushort)0); c.Write((ushort)0); c.Write((ushort)0);
                c.Write(0u); c.Write(offset); c.Write(nameBytes);
                count++;
            }
            uint centralOffset = (uint)fs.Position;
            central.Position = 0;
            central.CopyTo(fs);
            w.Write(0x06054b50u); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)count); w.Write((ushort)count);
            w.Write((uint)central.Length); w.Write(centralOffset); w.Write((ushort)0);
        }
    }

    private static uint[] crcTable;
    private static uint Crc32(byte[] data)
    {
        if (crcTable == null)
        {
            crcTable = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                crcTable[i] = c;
            }
        }
        uint crc = 0xFFFFFFFFu;
        foreach (byte b in data) crc = crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }

    private static bool ScriptsUpToDate(string title)
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (EditorApplication.isCompiling)
        {
            EditorUtility.DisplayDialog(title, "Unity está recompilando los scripts que han cambiado.\n\nCuando termine, vuelve a lanzar la build.", "OK");
            return false;
        }
        if (EditorUtility.scriptCompilationFailed)
        {
            EditorUtility.DisplayDialog(title, "Hay errores de compilación en la consola. Corrígelos antes de compilar.", "OK");
            return false;
        }
        return true;
    }

    private static bool EnsureWebPlatform(string title)
    {
        if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL) return true;
        if (!EditorUtility.DisplayDialog(title, "Hay que cambiar la plataforma a Web (reimporta los assets y tarda un poco).", "Cambiar", "Cancelar"))
            return false;
        return EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
    }

    private static void Fail(string title, string message)
    {
        Debug.LogError("[Publish] " + message);
        EditorUtility.DisplayDialog(title, message, "OK");
    }

    private static string Mb(long bytes) => (bytes / (1024f * 1024f)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " MB";

    private static string Slug(string s) => new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    // Ajustes del proyecto que se cambian para la build y se dejan como estaban al acabar
    private class SettingsSnapshot
    {
        private readonly string template = PlayerSettings.WebGL.template;
        private readonly WebGLCompressionFormat compression = PlayerSettings.WebGL.compressionFormat;
        private readonly bool fallback = PlayerSettings.WebGL.decompressionFallback;
        private readonly bool caching = PlayerSettings.WebGL.dataCaching;
        private readonly bool hashes = PlayerSettings.WebGL.nameFilesAsHashes;
        private readonly WebGLTextureSubtarget textures = EditorUserBuildSettings.webGLBuildSubtarget;
        private readonly Il2CppCodeGeneration codegen = PlayerSettings.GetIl2CppCodeGeneration(NamedBuildTarget.WebGL);

        public void Restore()
        {
            PlayerSettings.WebGL.template = template;
            PlayerSettings.WebGL.compressionFormat = compression;
            PlayerSettings.WebGL.decompressionFallback = fallback;
            PlayerSettings.WebGL.dataCaching = caching;
            PlayerSettings.WebGL.nameFilesAsHashes = hashes;
            EditorUserBuildSettings.webGLBuildSubtarget = textures;
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, codegen);
        }
    }
}
