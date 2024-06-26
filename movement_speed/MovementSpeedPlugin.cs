using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using UnityEngine;

[BepInPlugin("devopsdinosaur.lkg.movement_speed", "Movement Speed", "0.0.1")]
public class MovementSpeedPlugin : BaseUnityPlugin {

	private Harmony m_harmony = new Harmony("devopsdinosaur.lkg.movement_speed");
	public static ManualLogSource logger;
	private static ConfigEntry<bool> m_enabled;
	private static ConfigEntry<float> m_movement_speed_multiplier;

	private void Awake() {
		logger = this.Logger;
		try {
			m_enabled = this.Config.Bind<bool>("General", "Enabled", true, "Set to false to disable this mod.");
			m_movement_speed_multiplier = this.Config.Bind<float>("General", "Movement Speed Multiplier", 2f, "Multiplier applied to player movement speed (float, default 2f; 1 == game default speed (i.e. slow), > 1 == faster, < 1 == slower)");
			this.m_harmony.PatchAll();
			logger.LogInfo("devopsdinosaur.lkg.movement_speed v0.0.1 loaded.");
		} catch (Exception e) {
			logger.LogError("** Awake FATAL - " + e);
		}
	}

	[HarmonyPatch(typeof(ScCharacter), "Move")]
	class HarmonyPatch_ScCharacter_Move {

		private static bool Prefix(Vector2 nextPos, ScCharacter __instance, Transform ___thisTransform, float ___speed, Rigidbody2D ___rb) {
			try {
				if (!m_enabled.Value || !(__instance is ScPlayerController)) {
					return true;
				}
				Vector3 normalized = new Vector3(nextPos.x, nextPos.y, 0f).normalized;
				Vector3 vector = new Vector3(___thisTransform.position.x + normalized.x * Time.deltaTime * ___speed * m_movement_speed_multiplier.Value, ___thisTransform.position.y + normalized.y * Time.deltaTime * ___speed * m_movement_speed_multiplier.Value, __instance.transform.position.z);
				___rb.MovePosition(vector);
				return false;
			} catch (Exception e) {
				logger.LogError("** HarmonyPatch_ScCharacter_Move.Prefix ERROR - " + e);
			}
			return true;
		}
	}
}