using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Reflection;
using System.IO;

[BepInPlugin("devopsdinosaur.lkg.community_localization", "Community Localization", "0.0.1")]
public class CommunityLocalizationPlugin : BaseUnityPlugin {
	private Harmony m_harmony = new Harmony("devopsdinosaur.lkg.community_localization");
	public static ManualLogSource logger;
	private static ConfigEntry<bool> m_enabled;
	private static ConfigEntry<string> m_subdir;

	private void Awake() {
		logger = this.Logger;
		try {
			m_enabled = this.Config.Bind<bool>("General", "Enabled", true, "Set to false to disable this mod.");
			m_subdir = this.Config.Bind<string>("General", "Localization Subfolder", "community_localization", "Subfolder under this plugin's parent folder (i.e. <game>/BepInEx/plugins) which in turn contains a set of language directories (i.e. English, Spanish, etc).");
			CommunityLocalizer.initialize();
			this.m_harmony.PatchAll();
			logger.LogInfo("devopsdinosaur.lkg.community_localization v0.0.1 loaded.");
		} catch (Exception e) {
			logger.LogError("** Awake FATAL - " + e);
		}
	}

	private static void debug_log(string text) {
		logger.LogInfo(text);
	}

	class CommunityLocalizer {
		private static CommunityLocalizer m_instance = null;
		public static CommunityLocalizer Instance {
			get {
				if (m_instance == null) {
					m_instance = new CommunityLocalizer();
				}
				return m_instance;
			}
		}

		public static void initialize() {
			string root_dir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), m_subdir.Value);
			logger.LogInfo($"Assembling localization information from directory, '{root_dir}'.");
			if (!Directory.Exists(root_dir)) {
				logger.LogInfo("Directory does not exist; creating.");
				Directory.CreateDirectory(root_dir);
			}
		}
	}
}