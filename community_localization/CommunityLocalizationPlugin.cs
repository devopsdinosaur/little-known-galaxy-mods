using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

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
			this.m_harmony.PatchAll();
			logger.LogInfo("devopsdinosaur.lkg.community_localization v0.0.1 loaded.");
		} catch (Exception e) {
			logger.LogError("** Awake FATAL - " + e);
		}
	}

	private static void debug_log(string text) {
		logger.LogInfo(text);
	}

	[HarmonyPatch(typeof(ScLocalizationManager), "SetUpManager")]
	class HarmonyPatch_ScLocalizationManager_SetUpManager {

		private static void Postfix() {
			try {
				CommunityLocalizer.Instance.initialize();
			} catch (Exception e) {
				logger.LogError("** HarmonyPatch_ScLocalizationManager_SetUpManager.Postfix ERROR - " + e);
			}
		}
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
		private bool m_is_initialized = false;
		private ScLocalizationManager m_manager = null;
		
		public void initialize() {
			if (m_enabled.Value && !this.m_is_initialized) {
				this.reload();
			}
		}

		public void reload() {
			try {
				if (!m_enabled.Value) {
					return;
				}
				this.m_is_initialized = false;
				string root_dir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), m_subdir.Value);
				logger.LogInfo($"Assembling localization information from directory, '{root_dir}'.");
				if (!Directory.Exists(root_dir)) {
					logger.LogInfo("Directory does not exist; creating.");
					Directory.CreateDirectory(root_dir);
				}
				try {
					this.m_manager = Resources.FindObjectsOfTypeAll<ScLocalizationManager>()[0];
				} catch {
					logger.LogError("** CommunityLocalizer.initialize ERROR - unable to locate Localization Manager singleton.");
					return;
				}
				ScLocalizationData[] existing_localizations = (ScLocalizationData[]) ReflectionUtils.get_field_value(this.m_manager, "supportedLanguagesData");
				List<ScLocalizationData> new_localizations = new List<ScLocalizationData>();
				ScLocalizationData localization;
				int counter = 0;
				foreach (string sub_dir in Directory.GetDirectories(root_dir)) {
					try {
						localization = GameObject.Instantiate<ScLocalizationData>(existing_localizations[0]);
						localization.name = sub_dir.Replace("_", " ");

						counter++;
					} catch (Exception e) {
						logger.LogWarning($"** CommunityLocalizer.initialize WARNING - unable to load '{sub_dir}' localization.  Exception: " + e);
					}
				}
				logger.LogInfo($"Found {counter} total language localizations.");
				this.m_is_initialized = true;
			} catch (Exception e) {
				logger.LogError("** CommunityLocalizer.reload ERROR - " + e);
			}
		}
	}
}