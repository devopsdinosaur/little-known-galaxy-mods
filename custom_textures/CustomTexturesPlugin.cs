using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Reflection;

[BepInPlugin("devopsdinosaur.lkg.custom_textures", "Custom Textures", "0.0.1")]
public class CustomTexturesPlugin : BaseUnityPlugin {

	private Harmony m_harmony = new Harmony("devopsdinosaur.lkg.custom_textures");
	public static ManualLogSource logger;
	private static ConfigEntry<bool> m_enabled;
	private static ConfigEntry<string> m_subdir;
	
	private void Awake() {
		logger = this.Logger;
		try {
			m_enabled = this.Config.Bind<bool>("General", "Enabled", true, "Set to false to disable this mod.");
			m_subdir = this.Config.Bind<string>("General", "Texture Subfolder", "custom_textures", "Subfolder under this plugin's parent folder (i.e. <game>/BepInEx/plugins) which in turn contains a set of mod-specific texture directories.");
			TextureManager.Instance.get_texture_paths(m_subdir.Value);
			this.m_harmony.PatchAll();
			logger.LogInfo("devopsdinosaur.lkg.custom_textures v0.0.1 loaded.");
		} catch (Exception e) {
			logger.LogError("** Awake FATAL - " + e);
		}
	}

	class CustomTexture {
		private string m_name;
		private string m_path;
		private Texture2D m_texture;

		public CustomTexture(string name, string path) {
			this.m_name = name;
			this.m_path = path;
			this.m_texture = null;
		}

		public Texture2D get_texture() {
			return null;
		}
	}

	class TextureManager {
		private static TextureManager m_instance = null;
		public static TextureManager Instance {
			get {
				if (m_instance == null) {
					m_instance = new TextureManager();
				}
				return m_instance;
			}
		}
		private Dictionary<string, CustomTexture> m_textures = new Dictionary<string, CustomTexture>();

		public void get_texture_paths(string root_dir) {
			try {

			} catch (Exception e) {

			}
		}
	}

	private static Dictionary<string, Sprite> m_replaced_sprites = new Dictionary<string, Sprite>();


	private static void debug_log(string text) {
		logger.LogInfo(text);
	}

	private static void on_scene_loaded(Scene scene, LoadSceneMode mode) {
		try {
			List<int> checked_hashes = new List<int>();
			foreach (Component component in Resources.FindObjectsOfTypeAll<Component>()) {
				if (component is Renderer renderer) {

				} else if (component is MonoBehaviour) {
					replace_sprites_in_object(component, checked_hashes);
				}
			}
		} catch (Exception e) {
			logger.LogError("** on_scene_loaded ERROR - " + e);
		}
	}

	private static void replace_sprites_in_object(object obj, List<int> checked_hashes) {
		try {
			if (checked_hashes.Contains(obj.GetHashCode())) {
				return;
			}
			checked_hashes.Add(obj.GetHashCode());
			foreach (FieldInfo field in AccessTools.GetDeclaredFields(obj.GetType())) {
				if (field.FieldType == typeof(Sprite)) {
					debug_log($"Replacing sprite {obj.GetType().Name}.{field.Name}");
					field.SetValue(obj, replace_sprite((Sprite) field.GetValue(obj)));
				}
			}
		} catch (Exception e) {
			logger.LogError("** replace_sprites_in_object ERROR - " + e);
		}
	}

	private static string get_sprite_key(string sprite_name, string texture_name) {
		return sprite_name + "_" + texture_name;
	}

	private static Sprite replace_sprite(Sprite sprite) {
		try {
			if (sprite == null) {
				return null;
			}
			string texture_name = sprite.texture?.name;
			if (string.IsNullOrEmpty(texture_name)) {
				return sprite;
			}
			debug_log($"replace_sprite(sprite.name: {sprite.name}, sprite.texture.name: {texture_name})");
			string key = get_sprite_key(sprite.name, texture_name);
			if (m_replaced_sprites.TryGetValue(key, out Sprite new_sprite)) {
				debug_log($"sprite already replaced");
				return new_sprite;
			}

		} catch (Exception e) {
			logger.LogError("** replace_sprite ERROR - " + e);
		}
		return sprite;
	}
}