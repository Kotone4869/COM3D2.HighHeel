using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine.SceneManagement;

namespace COM3D2.HighHeel;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.Kotone4869.com3d2.highheel";
    public const string PluginName = "COM3D2.HighHeel";
    public const string PluginVersion = "1.0.0";
    public const string PluginString = PluginName + " " + PluginVersion;

    public readonly PluginConfig Configuration;
    public new readonly ManualLogSource Logger;
    public Core.ShoeConfig EditModeConfig = new();

    private const string ConfigName = "Configuration.cfg";

    private static readonly string ConfigPath = Path.Combine(Paths.ConfigPath, PluginName);
    private static readonly string ShoeConfigPath = Path.Combine(ConfigPath, "Configurations");

    private readonly UI.MainWindow mainWindow;
    private readonly Harmony harmony;

    public Plugin()
    {
        try
        {
            harmony = Harmony.CreateAndPatchAll(typeof(Core.Hooks));
        }
        catch (Exception e)
        {
            base.Logger.LogError($"Unable to inject core because: {e.Message}");
            base.Logger.LogError(e.StackTrace);

            DestroyImmediate(this);

            return;
        }

        Instance = this;
        Configuration = new(new(Path.Combine(ConfigPath, ConfigName), false, Info.Metadata));
        Logger = base.Logger;

        mainWindow = new();
        mainWindow.ReloadEvent += (_, _) =>
            ShoeDatabase = LoadShoeDatabase();

        mainWindow.ExportEvent += (_, args) =>
            ExportConfiguration(EditModeConfig, args.Text);

        mainWindow.ImportEvent += (_, args) =>
        {
            ImportConfigsAndUpdate(args.Text);
        };

        SceneManager.sceneLoaded += (_, _) =>
            IsDance = FindObjectOfType<DanceMain>() != null;

        ShoeDatabase = LoadShoeDatabase();

        ImportConfigsAndUpdate(string.Empty);
    }

    public static bool IsDance { get; private set; }

    public static Plugin? Instance { get; private set; }

    public Dictionary<string, Core.ShoeConfig> ShoeDatabase { get; private set; }

    public bool EditMode { get; set; }

    public void ImportConfigsAndUpdate(string configName)
    {
        ImportConfiguration(ref EditModeConfig, configName);
        mainWindow.UpdateEditModeValues();
    }

    private static Dictionary<string, Core.ShoeConfig> LoadShoeDatabase()
    {
        var database = new Dictionary<string, Core.ShoeConfig>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(ShoeConfigPath))
            Directory.CreateDirectory(ShoeConfigPath);

        var shoeConfigs = Directory.GetFiles(ShoeConfigPath, "hhmod_*.json", SearchOption.AllDirectories);

        foreach (var configPath in shoeConfigs)
        {
            try
            {
                var key = Path.GetFileNameWithoutExtension(configPath);

                if (database.ContainsKey(key))
                {
                    Instance!.Logger.LogWarning($"Duplicate configuration filename found: {configPath}. Skipping");
                    continue;
                }

                var configJson = File.ReadAllText(configPath);
                database[key] = JsonConvert.DeserializeObject<Core.ShoeConfig>(configJson);
            }
            catch (Exception e)
            {
                var errorVerb = e is IOException ? "load" : "parse";
                Instance!.Logger.LogWarning($"Could not {errorVerb} '{configPath}' because: {e.Message}");
            }
        }

        return database;
    }

    private static void ExportConfiguration(Core.ShoeConfig config, string filename)
    {
        if (!Directory.Exists(ShoeConfigPath))
            Directory.CreateDirectory(ShoeConfigPath);

        var fullPath = CreateConfigFullPath(filename);

        var jsonText = JsonConvert.SerializeObject(config, Formatting.Indented);

        File.WriteAllText(fullPath, jsonText);
    }

    private static void ImportConfiguration(ref Core.ShoeConfig config, string filename)
    {
        var fullPath = CreateConfigFullPath(filename);

        if (!File.Exists(fullPath))
            return;

        var jsonText = File.ReadAllText(fullPath);

        config = JsonConvert.DeserializeObject<Core.ShoeConfig>(jsonText);
    }

    private static string CreateConfigFullPath(string filename)
    {
        var sanitizedFilename = SanitizeFilename(filename.ToLowerInvariant());

        if (string.IsNullOrEmpty(sanitizedFilename))
            sanitizedFilename = "hhmod_configuration";
        else if (!sanitizedFilename.StartsWith("hhmod_"))
            sanitizedFilename = "hhmod_" + sanitizedFilename;

        sanitizedFilename += ".json";

        return Path.Combine(ShoeConfigPath, sanitizedFilename);

        static string SanitizeFilename(string path)
        {
            var invalid = Path.GetInvalidFileNameChars();
            path = path.Trim();

            return string.Join("_", path.Split(invalid)).Replace(".", string.Empty).Trim('_');
        }
    }

    private void OnDestroy() =>
        harmony?.UnpatchSelf();

    private void Update()
    {
        if (Configuration.UIShortcut.Value.IsUp())
            mainWindow.Visible = !mainWindow.Visible;

        mainWindow.Update();
    }

    private void OnGUI() =>
        mainWindow.Draw();
}
