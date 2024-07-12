using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Reflection;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

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

	[HarmonyPatch(typeof(ScWndSettingsTabMain), "SetUpTabWindow")]
	class HarmonyPatch_ScWndSettingsTabMain_SetUpTabWindow {

		private static void Postfix(ref int ___langaugeCount) {
			try {
				___langaugeCount = CommunityLocalizer.Instance.Languages.Length;
			} catch (Exception e) {
				logger.LogError("** HarmonyPatch_ScWndSettingsTabMain_SetUpTabWindow.Postfix ERROR - " + e);
			}
		}
	}

	[HarmonyPatch(typeof(ScWndSettingsTabMain), "UpdateLanguageText")]
	class HarmonyPatch_ScWndSettingsTabMain_UpdateLanguageText {

		private static bool Prefix(TextMeshProUGUI ___languageText, int ___languageLevel) {
			try {
				// enum ancestors of parent of ___languageText to find "Text_D_Language"
				___languageText.text = CommunityLocalizer.Instance.Languages[___languageLevel].name;
				return false;
			} catch (Exception e) {
				logger.LogError("** HarmonyPatch_ScWndSettingsTabMain_UpdateLanguageText.Prefix ERROR - " + e);
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(ScPlayerPrefsManager), "GetLanguage")]
	class HarmonyPatch_ScPlayerPrefsManager_GetLanguage {

		private static bool Prefix(ref int __result) {
			try {
				__result = 0;
				return false;
			} catch (Exception e) {
				logger.LogError("** HarmonyPatch_ScPlayerPrefsManager_GetLanguage.Prefix ERROR - " + e);
			}
			return true;
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
		private ScLocalizationData[] m_languages = null;
		public ScLocalizationData[] Languages {
			get {
				return this.m_languages;
			}
		}

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
				Dictionary<string, ScLocalizationData> new_localizations = new Dictionary<string, ScLocalizationData>();
				foreach (ScLocalizationData existing_localization in existing_localizations) {
					new_localizations[existing_localization.name] = existing_localization;
				}
				Dictionary<string, FieldInfo> fields = new Dictionary<string, FieldInfo>();
				foreach (FieldInfo field in existing_localizations[0].GetType().GetFields(ReflectionUtils.BINDING_FLAGS_ALL)) {
					if (!field.Name.EndsWith("JSON")) {
						continue;
					}
					string key = field.Name.Substring(0, field.Name.Length - 4).ToLower();
					if (key[key.Length - 1] == 's') {
						key = key.Substring(0, key.Length - 1);
					}
					fields[key] = field;
				}
				ScLocalizationData localization;
				int counter = 0;
				List<TextAsset> npc_texts = null;
				foreach (string sub_dir in Directory.GetDirectories(root_dir)) {
					try {
						localization = GameObject.Instantiate<ScLocalizationData>(existing_localizations[0]);
						localization.name = Path.GetFileName(sub_dir.Replace("_", " "));
						logger.LogInfo($"Loading '{localization.name}'.");
						npc_texts = new List<TextAsset>();
						foreach (string file in Directory.GetFiles(sub_dir)) {
							string file_name_with_extension = Path.GetFileName(file);
							string file_name = Path.GetFileNameWithoutExtension(file).ToLower();
							if (Path.GetExtension(file).ToLower() != ".txt" || !file_name.Contains("_")) {
								logger.LogInfo($"--> skipping unknown file, '{file_name_with_extension}'.");
								continue;
							}
							file_name = file_name.Split(new char[] {'_'})[1];
							if (file_name[file_name.Length - 1] == 's') {
								file_name = file_name.Substring(0, file_name.Length - 1);
							}
							if (fields.ContainsKey(file_name)) {
								logger.LogInfo($"--> reading '{file_name_with_extension}' into localization.{fields[file_name].Name} field.");
								fields[file_name].SetValue(localization, new TextAsset(File.ReadAllText(file)));
							} else {
								string data = File.ReadAllText(file);
								if (!data.Contains("npcSaveID")) {
									logger.LogInfo($"--> skipping unknown file, '{file_name_with_extension}'.");
									continue;
								}
								logger.LogInfo($"--> reading '{file_name_with_extension}' into localization.npcsJSON[] field.");
								npc_texts.Add(new TextAsset(data));
							}
						}
						ReflectionUtils.set_field_value(localization, "npcsJSON", npc_texts.ToArray());
						if (new_localizations.ContainsKey(localization.name)) {
							logger.LogInfo($"Success - overriding existing '{localization.name}' localization in manager.");
						} else {
							logger.LogInfo($"Success - adding '{localization.name}' localization to manager.");
						}
						new_localizations[localization.name] = localization;
						counter++;
					} catch (Exception e) {
						logger.LogWarning($"** CommunityLocalizer.initialize WARNING - unable to load '{sub_dir}' localization.  Exception: " + e);
					}
				}
				logger.LogInfo($"Found {counter} total language localizations.");
				this.m_languages = new_localizations.Values.ToList().ToArray();
				ReflectionUtils.set_field_value(this.m_manager, "supportedLanguagesData", this.m_languages);
				this.m_is_initialized = true;
			} catch (Exception e) {
				logger.LogError("** CommunityLocalizer.reload ERROR - " + e);
			}
		}
	}
}