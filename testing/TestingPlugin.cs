﻿using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

[BepInPlugin("devopsdinosaur.lkg.testing", "Testing", "0.0.1")]
public class TestingPlugin : BaseUnityPlugin {

	private Harmony m_harmony = new Harmony("devopsdinosaur.lkg.testing");
	public static ManualLogSource logger;
	private static ConfigEntry<bool> m_enabled;
	
	private void Awake() {
		logger = this.Logger;
		try {
			m_enabled = this.Config.Bind<bool>("General", "Enabled", true, "Set to false to disable this mod.");
			if (m_enabled.Value) {
				this.m_harmony.PatchAll();
			}

            SceneManager.sceneLoaded += on_scene_loaded;

			logger.LogInfo("devopsdinosaur.lkg.testing v0.0.1" + (m_enabled.Value ? "" : " [inactive; disabled in config]") + " loaded.");
		} catch (Exception e) {
			logger.LogError("** Awake FATAL - " + e);
		}
	}

    public static bool list_descendants(Transform parent, Func<Transform, bool> callback, int indent) {
		Transform child;
		string indent_string = "";
		for (int counter = 0; counter < indent; counter++) {
			indent_string += " => ";
		}
		for (int index = 0; index < parent.childCount; index++) {
			child = parent.GetChild(index);
			logger.LogInfo(indent_string + child.gameObject.name);
			if (callback != null) {
				if (callback(child) == false) {
					return false;
				}
			}
			list_descendants(child, callback, indent + 1);
		}
		return true;
	}

	public static bool enum_descendants(Transform parent, Func<Transform, bool> callback) {
		Transform child;
		for (int index = 0; index < parent.childCount; index++) {
			child = parent.GetChild(index);
			if (callback != null) {
				if (callback(child) == false) {
					return false;
				}
			}
			enum_descendants(child, callback);
		}
		return true;
	}

	public static void list_component_types(Transform obj) {
		foreach (Component component in obj.GetComponents<Component>()) {
			logger.LogInfo(component.GetType().ToString());
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

		}
	}

	class TextureManager {
		
		
		private Dictionary<string, CustomTexture> m_textures = new Dictionary<string, CustomTexture>();


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

	/*
	[HarmonyPatch(typeof(), "")]
	class HarmonyPatch_ {

		private static bool Prefix() {
			try {

				return false;
			} catch (Exception e) {
				logger.LogError("** XXXXX.Prefix ERROR - " + e);
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(), "")]
	class HarmonyPatch_ {

		private static void Postfix() {
			try {

				
			} catch (Exception e) {
				logger.LogError("** XXXXX.Postfix ERROR - " + e);
			}
		}
	}
	*/
}