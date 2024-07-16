using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Reflection;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using Newtonsoft.Json;

[BepInPlugin("devopsdinosaur.lkg.community_localization", "Community Localization", "0.0.2")]
public class CommunityLocalizationPlugin : BaseUnityPlugin {
	
	private const string MISSING_KEY_STRING = "<< MISSING STRING >>";
	
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

		private static bool Prefix() {
			try {
				CommunityLocalizer.Instance.initialize();
				return true;
			} catch (Exception e) {
				logger.LogError("** HarmonyPatch_ScLocalizationManager_SetUpManager.Prefix ERROR - " + e);
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(ScWndSettingsTabMain), "SetUpTabWindow")]
	class HarmonyPatch_ScWndSettingsTabMain_SetUpTabWindow {

		private static void Postfix(ref int ___langaugeCount, ref TextMeshProUGUI ___languageText) {
			try {
				TextMeshProUGUI new_language_text = null;
				bool enum_descendants_callback(Transform transform) {
					if (transform.name == "Text_D_Language") {
						new_language_text = transform.GetComponent<TextMeshProUGUI>();
						return false;
					}
					return true;
				}
				UnityUtils.enum_descendants(___languageText.transform.parent, enum_descendants_callback);
				if (new_language_text != null) {
					___languageText = new_language_text;
				}
				___langaugeCount = CommunityLocalizer.Instance.Languages.Length;
			} catch (Exception e) {
				logger.LogError("** HarmonyPatch_ScWndSettingsTabMain_SetUpTabWindow.Postfix ERROR - " + e);
			}
		}
	}

	[HarmonyPatch(typeof(ScWndSettingsTabMain), "UpdateLanguageText")]
	class HarmonyPatch_ScWndSettingsTabMain_UpdateLanguageText {

		private static bool Prefix(TextMeshProUGUI ___languageText, int ___languageLevel, int ___langaugeCount) {
			try {
				// debug_log($"ScWndSettingsTabMain.UpdateLanguageText(languageLevel: {___languageLevel})");
				if (___languageLevel < 0 || ___languageLevel > ___langaugeCount) {
					ScLocalizationData[] languages = CommunityLocalizer.Instance.Languages;
					int index;
					for (index = 0; index < ___langaugeCount; index++) {
						// debug_log($"languages[{index}] = language: {languages[index].GetLanguage()}, name: {languages[index].name}");
						if (languages[index].GetLanguage() == (Language) ___languageLevel) {
							___languageLevel = index;
							break;
						}
					}
					if (index == ___langaugeCount) {
						___languageLevel = 0;
					}
				}
				___languageText.text = CommunityLocalizer.Instance.Languages[___languageLevel].name;
				return false;
			} catch (Exception e) {
				logger.LogError("** HarmonyPatch_ScWndSettingsTabMain_UpdateLanguageText.Prefix ERROR - " + e);
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(ScWndSettingsTabMain), "WaitConfirmChangeLanguage")]
	class HarmonyPatch_ScWndSettingsTabMain_WaitConfirmChangeLanguage {

		private static bool Prefix(
			int optionSelected, 
			ScWndSettingsTabMain __instance, 
			ref bool ___subscribeOptions,
			TextMeshProUGUI ___languageText,
			ref bool ___closeAfterOverlayCloses
		) {
			try {
				ScUIManager.Instance.OptionSelectedListeners -= __instance.WaitConfirmChangeLanguage;
				___subscribeOptions = false;
				if (optionSelected == 2) {
					ScPlayerPrefsManager.SetLanguage((Language) ___languageText.text.GetHashCode());
					___closeAfterOverlayCloses = true;
					ScGameManager.Instance.GetSceneManager().ReturnToMainMenu();
				}
				return false;
			} catch (Exception e) {
				logger.LogError("** HarmonyPatch_ScWndSettingsTabMain_WaitConfirmChangeLanguage.Prefix ERROR - " + e);
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(ScLocalizationManager), "GetMatchingLocalizationData")]
	class HarmonyPatch_ScLocalizationManager_GetMatchingLocalizationData {

		private static bool Prefix(ref ScLocalizationData[] ___supportedLanguagesData) {
			try {
				// debug_log($"ScLocalizationManager.GetMatchingLocalizationData(language: {___language})");
				___supportedLanguagesData = CommunityLocalizer.Instance.Languages;
				return true;
			} catch (Exception e) {
				logger.LogError("** HarmonyPatch_ScLocalizationManager_GetMatchingLocalizationData.Prefix ERROR - " + e);
			}
			return true;
		}
	}
	
	[HarmonyPatch(typeof(ScWndMainMenu), "BeforeOpening")]
	class HarmonyPatch_ScWndMainMenu_BeforeOpening {

		private static bool Prefix(ref GameObject ___translatedGameTitle) {
			try {
				// ScWndMainMenu.translatedGameTitle seems to currently be uninitialized (null).
				// Create an empty GameObject to allow translatedGameTitle.SetActive() to work without crashing.
				GameObject obj = GameObject.Instantiate<GameObject>(Resources.FindObjectsOfTypeAll<GameObject>()[0]);
				foreach (Component component in obj.GetComponents<Component>()) {
					GameObject.Destroy(component);
				}
				___translatedGameTitle = obj;
				return true;
			} catch (Exception e) {
				logger.LogError("** HarmonyPatch_ScWndMainMenu_BeforeOpening.Prefix ERROR - " + e);
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(ScLocalizationManager), "GetUIText")]
	class HarmonyPatch_ScLocalizationManager_GetUIText {

		private static void Postfix(string uiTextKey, ref ScUIText __result) {
			if (__result == null) {
				__result = new ScUIText(uiTextKey, MISSING_KEY_STRING);
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
				int required_npc_count = ((TextAsset[]) ReflectionUtils.get_field_value(existing_localizations[0], "npcsJSON")).Length;
				Dictionary<string, ScLocalizationData> new_localizations = new Dictionary<string, ScLocalizationData>();
				foreach (ScLocalizationData existing_localization in existing_localizations) {
					new_localizations[existing_localization.name] = existing_localization;
				}
				Dictionary<string, FieldInfo> fields = new Dictionary<string, FieldInfo>();
				Dictionary<string, bool> loaded_fields = new Dictionary<string, bool>();
				foreach (FieldInfo field in existing_localizations[0].GetType().GetFields(ReflectionUtils.BINDING_FLAGS_ALL)) {
					if (!field.Name.EndsWith("JSON")) {
						continue;
					}
					string key = field.Name.Substring(0, field.Name.Length - 4).ToLower();
					if (key[key.Length - 1] == 's') {
						key = key.Substring(0, key.Length - 1);
					}
					if (key != "npc") {
						fields[key] = field;
						loaded_fields[key] = false;
					}
				}
				ScLocalizationData localization;
				List<TextAsset> npc_texts = null;
				List<string> npc_names = null;
				Encoding encoding = Encoding.GetEncoding("iso-8859-1");
				foreach (string sub_dir in Directory.GetDirectories(root_dir)) {
					try {
						localization = GameObject.Instantiate<ScLocalizationData>(existing_localizations[0]);
						localization.name = Path.GetFileName(sub_dir.Replace("_", " "));
						ReflectionUtils.set_field_value(localization, "language", (localization.name == "English" ? 0 : localization.name.GetHashCode()));
						logger.LogInfo($"Loading '{localization.name}' (language id [hash code]: {localization.GetLanguage()}).");
						npc_texts = new List<TextAsset>();
						npc_names = new List<string>();
						foreach (string key in fields.Keys) {
							loaded_fields[key] = false;
						}
						foreach (string file in Directory.GetFiles(sub_dir)) {
							string file_name_with_extension = Path.GetFileName(file);
							string file_name = Path.GetFileNameWithoutExtension(file).ToLower();
							if (Path.GetExtension(file).ToLower() != ".txt" || !file_name.Contains("_")) {
								// debug_log($"--> skipping unknown file, '{file_name_with_extension}'.");
								continue;
							}
							file_name = file_name.Split(new char[] {'_'})[1];
							if (file_name[file_name.Length - 1] == 's') {
								file_name = file_name.Substring(0, file_name.Length - 1);
							}
							if (fields.ContainsKey(file_name)) {
								// debug_log($"--> reading '{file_name_with_extension}' into localization.{fields[file_name].Name} field.");
								fields[file_name].SetValue(localization, new TextAsset(File.ReadAllText(file, encoding)));
								loaded_fields[file_name] = true;
							} else {
								string data = File.ReadAllText(file, encoding);
								if (!data.Contains("npcSaveID")) {
									// debug_log($"--> skipping unknown file, '{file_name_with_extension}'.");
									continue;
								}
								// debug_log($"--> reading '{file_name_with_extension}' into localization.npcsJSON[] field.");
								npc_texts.Add(new TextAsset(data));
								npc_names.Add(file_name_with_extension);
							}
						}
						logger.LogInfo("Checking fields and NPC count.");
						bool has_all_fields = true;
						foreach (string key in fields.Keys) {
							if (!loaded_fields[key]) {
								logger.LogError($"** Field ERROR - '{key}' not present.");
								has_all_fields = false;
								break;
							}
						}
						if (!has_all_fields) {
							continue;
						}
						if (npc_names.Count != required_npc_count) {
							logger.LogError($"** NPC ERROR - one or more missing NPC files (required: {required_npc_count}, actual: {npc_names.Count}).");
							continue;
						}
						ReflectionUtils.set_field_value(localization, "npcsJSON", npc_texts.ToArray());
						logger.LogInfo("Validating localization data.");
						if (!this.test_localization(localization, npc_names.ToArray())) {
							continue;
						}
						if (new_localizations.ContainsKey(localization.name)) {
							logger.LogInfo($"Success - overriding existing '{localization.name}' localization in manager.");
						} else {
							logger.LogInfo($"Success - adding '{localization.name}' localization to manager.");
						}
						new_localizations[localization.name] = localization;
					} catch (Exception e) {
						logger.LogWarning($"** CommunityLocalizer.initialize WARNING - unable to load '{sub_dir}' localization.  Exception: " + e);
					}
				}
				this.m_languages = new_localizations.Values.ToList().ToArray();
				logger.LogInfo($"Total language localizations (including non-overriden existing): {this.m_languages.Length}.");
				ReflectionUtils.set_field_value(this.m_manager, "supportedLanguagesData", this.m_languages);
				this.m_is_initialized = true;
			} catch (Exception e) {
				logger.LogError("** CommunityLocalizer.reload ERROR - " + e);
			}
		}

		private bool test_localization(ScLocalizationData localization, string[] npc_names) {
			void deserialize<T>(string name, string data) {
				try {
					JsonConvert.DeserializeObject<T>(data);
				} catch (Newtonsoft.Json.JsonException e) {
					logger.LogError($"** CommunityLocalizer.test_localization ERROR - JSON syntax error in '{name}' data.  Exception: " + e);
					throw e;
				}
			}
			try {
				deserialize<List<ScGeneralText>>("General", localization.GetGeneralText().ToString());
				deserialize<List<ScItemText>>("Items", localization.GetItemText().ToString());
				deserialize<List<ScCinematicText>>("Cine", localization.GetCineText().ToString());
				deserialize<List<ScQuestText>>("Quests", localization.GetQuestText().ToString());
				deserialize<List<ScEmailText>>("Emails", localization.GetEmailText().ToString());
				deserialize<List<ScEventText>>("Events", localization.GetEventText().ToString());
				TextAsset[] npc_texts = localization.GetNPCText();
				for (int index = 0; index < npc_texts.Length; index++) {
					deserialize<List<ScNPCText>>(npc_names[index], npc_texts[index].ToString());
				}
				deserialize<List<ScLocationText>>("Locations", localization.GetLocationsText().ToString());
				deserialize<List<ScUpgradeText>>("Upgrades", localization.GetUpgradesText().ToString());
				deserialize<List<ScUIText>>("UI", localization.GetUIText().ToString());
				deserialize<List<ScCollectionText>>("Collections", localization.GetCollectionText().ToString());
				deserialize<List<ScAnimalText>>("Animals", localization.GetAnimalText().ToString());
				deserialize<List<ScAchievementText>>("Achievements", localization.GetAchievementText().ToString());
				deserialize<List<ScLibraryText>>("Library", localization.GetLibraryText().ToString());
				return true;
			} catch {
			}
			return false;
		}
	}
}