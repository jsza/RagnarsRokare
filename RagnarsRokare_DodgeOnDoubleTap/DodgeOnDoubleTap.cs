﻿using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using UnityEngine;

namespace RagnarsRokare_DodgeOnDoubleTap
{
	[BepInPlugin("RagnarsRokare.DodgeOnDoubleTap", "RagnarsRökare DodgeOnDoubleTapMod", "1.0.0")]
	[BepInProcess("valheim.exe")]
    public class DodgeOnDoubleTap : BaseUnityPlugin
    {
        private readonly Harmony harmony = new Harmony("RagnarsRokare.DodgeOnDoubleTap");
		public static ConfigEntry<int> DodgeTapPressMin;
		public static ConfigEntry<int> DodgeTapReleaseMin;
		public static ConfigEntry<int> DodgeDoubleTapDelay;

		void Awake()
        {
            harmony.PatchAll();
			DodgeTapPressMin = Config.Bind("General", "DodgeTapPressMin", 100);
			DodgeTapReleaseMin = Config.Bind("General", "DodgeTapReleaseMin", 100);
			DodgeDoubleTapDelay = Config.Bind("General", "DodgeDoubleTapDelay", 500);
		}

		public enum DodgeDirection { None, Forward, Backwards, Left, Right };
		public static DodgeDirection DodgeDir { get; set; } = DodgeDirection.None;

		[HarmonyPatch(typeof(Player), "SetControls")]
		class SetControls_Patch
		{
			static bool Prefix(ref float ___m_queuedDodgeTimer, ref Vector3 ___m_queuedDodgeDir, Vector3 ___m_lookDir)
            {
				if (DodgeDir == DodgeDirection.None)
                {
					return true;
                }

				___m_queuedDodgeTimer = 0.5f;

				var dodgeDir = ___m_lookDir;
				dodgeDir.y = 0f;
				dodgeDir.Normalize();

				if (DodgeDir == DodgeDirection.Backwards)  dodgeDir = -dodgeDir;
				else if (DodgeDir == DodgeDirection.Left) dodgeDir = Quaternion.AngleAxis(-90, Vector3.up) * dodgeDir;
				else if (DodgeDir == DodgeDirection.Right) dodgeDir = Quaternion.AngleAxis(90, Vector3.up) * dodgeDir;

				___m_queuedDodgeDir = dodgeDir;
				DodgeDir = DodgeDirection.None;
				return false;
			}
		}

		[HarmonyPatch(typeof(PlayerController), "FixedUpdate")]
        class FixedUpdate_Patch
        {
			private static DateTime? m_forwardLastTapRegistered = DateTime.Now;
			private static DateTime? m_backwardLastTapRegistered = DateTime.Now;
			private static DateTime? m_leftLastTapRegistered = DateTime.Now;
			private static DateTime? m_rightLastTapRegistered = DateTime.Now;

			private static DateTime m_forwardLastTapCheck = DateTime.Now;
			private static DateTime m_backwardLastTapCheck = DateTime.Now;
			private static DateTime m_leftLastTapCheck = DateTime.Now;
			private static DateTime m_rightLastTapCheck = DateTime.Now;

			private static float m_forwardPressTimer = 0;
			private static float m_backwardPressTimer = 0;
			private static float m_leftPressTimer = 0;
			private static float m_rightPressTimer = 0;

			private static float m_forwardReleaseTimer = 0;
			private static float m_backwardReleaseTimer = 0;
			private static float m_leftReleaseTimer = 0;
			private static float m_rightReleaseTimer = 0;

			private static bool m_forwardTapPressed = false;
			private static bool m_backwardTapPressed = false;
			private static bool m_leftTapPressed = false;
			private static bool m_rightTapPressed = false;

			static bool Prefix(ZNetView ___m_nview, ref Player ___m_character, ref bool ___m_attackWasPressed, ref bool ___m_secondAttackWasPressed, ref bool ___m_blockWasPressed, ref bool ___m_lastJump, ref bool ___m_lastCrouch)
            {
				if ((bool)___m_nview && !___m_nview.IsOwner())
				{
					return false;
				}
				if (!TakeInput())
				{
					___m_character.SetControls(Vector3.zero, attack: false, attackHold: false, secondaryAttack: false, block: false, blockHold: false, jump: false, crouch: false, run: false, autoRun: false);
					return false;
				}
				bool flag = InInventoryEtc();
				Vector3 zero = Vector3.zero;
				bool run = ZInput.GetButton("Run") || ZInput.GetButton("JoyRun");
				if (ZInput.GetButton("Forward"))
				{
					DetectTap(true, (float)(DateTime.Now - m_forwardLastTapCheck).TotalMilliseconds, DodgeTapPressMin.Value, true, ref m_forwardPressTimer, ref m_forwardReleaseTimer, ref m_forwardTapPressed);
					m_forwardLastTapCheck = DateTime.Now;
					zero.z += 1f;
				}
				else
                {
                    bool isTap = DetectTap(false, (float)(DateTime.Now - m_forwardLastTapCheck).TotalMilliseconds, DodgeTapReleaseMin.Value, true, ref m_forwardPressTimer, ref m_forwardReleaseTimer, ref m_forwardTapPressed);
                    CheckForDoubleTapDodge(isTap, ref m_forwardLastTapCheck, ref m_forwardPressTimer, ref m_forwardReleaseTimer, ref m_forwardLastTapRegistered, DodgeDirection.Forward);
                }
                if (ZInput.GetButton("Backward"))
				{
					DetectTap(true, (float)(DateTime.Now - m_backwardLastTapCheck).TotalMilliseconds, DodgeTapPressMin.Value, true, ref m_backwardPressTimer, ref m_backwardReleaseTimer, ref m_backwardTapPressed);
					m_backwardLastTapCheck = DateTime.Now;
					zero.z -= 1f;
				}
				else
				{
					bool isTap = DetectTap(false, (float)(DateTime.Now - m_backwardLastTapCheck).TotalMilliseconds, DodgeTapReleaseMin.Value, true, ref m_backwardPressTimer, ref m_backwardReleaseTimer, ref m_backwardTapPressed);
					CheckForDoubleTapDodge(isTap, ref m_backwardLastTapCheck, ref m_backwardPressTimer, ref m_backwardReleaseTimer, ref m_backwardLastTapRegistered, DodgeDirection.Backwards);
				}
				if (ZInput.GetButton("Left"))
				{
					DetectTap(true, (float)(DateTime.Now - m_leftLastTapCheck).TotalMilliseconds, DodgeTapPressMin.Value, true, ref m_leftPressTimer, ref m_leftReleaseTimer, ref m_leftTapPressed);
					m_leftLastTapCheck = DateTime.Now;
					zero.x -= 1f;
				}
				else
				{
					bool isTap = DetectTap(false, (float)(DateTime.Now - m_leftLastTapCheck).TotalMilliseconds, DodgeTapReleaseMin.Value, true, ref m_leftPressTimer, ref m_leftReleaseTimer, ref m_leftTapPressed);
					CheckForDoubleTapDodge(isTap, ref m_leftLastTapCheck, ref m_leftPressTimer, ref m_leftReleaseTimer, ref m_leftLastTapRegistered, DodgeDirection.Left);
				}
				if (ZInput.GetButton("Right"))
				{
					DetectTap(true, (float)(DateTime.Now - m_rightLastTapCheck).TotalMilliseconds, DodgeTapPressMin.Value, true, ref m_rightPressTimer, ref m_rightReleaseTimer, ref m_rightTapPressed);
					m_rightLastTapCheck = DateTime.Now;
					zero.x += 1f;
				}
				else
				{
					bool isTap = DetectTap(false, (float)(DateTime.Now - m_rightLastTapCheck).TotalMilliseconds, DodgeTapReleaseMin.Value, true, ref m_rightPressTimer, ref m_rightReleaseTimer, ref m_rightTapPressed);
					CheckForDoubleTapDodge(isTap, ref m_rightLastTapCheck, ref m_rightPressTimer, ref m_rightReleaseTimer, ref m_rightLastTapRegistered, DodgeDirection.Right);
				}
				zero.x += ZInput.GetJoyLeftStickX();
				zero.z += 0f - ZInput.GetJoyLeftStickY();
				if (zero.magnitude > 1f)
				{
					zero.Normalize();
				}
				bool flag2 = (ZInput.GetButton("Attack") || ZInput.GetButton("JoyAttack")) && !flag;
				bool attackHold = flag2;
				bool attack = flag2 && !___m_attackWasPressed;
				___m_attackWasPressed = flag2;
				bool flag3 = (ZInput.GetButton("SecondAttack") || ZInput.GetButton("JoySecondAttack")) && !flag;
				bool secondaryAttack = flag3 && !___m_secondAttackWasPressed;
				___m_secondAttackWasPressed = flag3;
				bool flag4 = (ZInput.GetButton("Block") || ZInput.GetButton("JoyBlock")) && !flag;
				bool blockHold = flag4;
				bool block = flag4 && !___m_blockWasPressed;
				___m_blockWasPressed = flag4;
				bool button = ZInput.GetButton("Jump");
				bool jump = (button && !___m_lastJump) || ZInput.GetButtonDown("JoyJump");
				___m_lastJump = button;
				bool flag5 = InventoryGui.IsVisible();
				bool flag6 = (ZInput.GetButton("Crouch") || ZInput.GetButton("JoyCrouch")) && !flag5;
				bool crouch = flag6 && ___m_lastCrouch;
				___m_lastCrouch = flag6;
				bool button2 = ZInput.GetButton("AutoRun");
				___m_character.SetControls(zero, attack, attackHold, secondaryAttack, block, blockHold, jump, crouch, run, button2);

				return false;
			}

            private static void CheckForDoubleTapDodge(bool isTap, ref DateTime lastTapCheck, ref float pressTimer, ref float releaseTimer, ref DateTime? lastTapRegistered, DodgeDirection dodgeDirection)
            {
                lastTapCheck = DateTime.Now;
                if (isTap)
                {
                    pressTimer = 0;
                    releaseTimer = 0;
                    if (lastTapRegistered == null)
                    {
						lastTapRegistered = DateTime.Now;
                    }
                    else
                    {
                        var milliesSinceLastTap = (DateTime.Now - lastTapRegistered)?.TotalMilliseconds ?? 0f;
                        if (milliesSinceLastTap < DodgeDoubleTapDelay.Value)
                        {
                            DodgeDir = dodgeDirection;
                        }
                        lastTapRegistered = null;
                    }
                }
            }

            private static bool DetectTap(bool pressed, float dt, float minPressTime, bool run, ref float pressTimer, ref float releasedTimer, ref bool tapPressed)
			{
				bool result = false;
				if (pressed)
				{
					if ((releasedTimer > 0f && releasedTimer < minPressTime) & tapPressed)
					{
						tapPressed = false;
						result = true;
					}
					pressTimer += dt;
					releasedTimer = 0f;
				}
				else
				{
					if (pressTimer > 0f)
					{
						tapPressed = pressTimer < minPressTime;
						if (run & tapPressed)
						{
							tapPressed = false;
							result = true;
						}
					}
					releasedTimer += dt;
					pressTimer = 0f;
				}
				return result;
			}

			private static bool TakeInput()
			{
				if (GameCamera.InFreeFly())
				{
					return false;
				}
				if ((!Chat.instance || !Chat.instance.HasFocus()) && !Menu.IsVisible() && !Console.IsVisible() && !TextInput.IsVisible() && !Minimap.InTextInput() && (!ZInput.IsGamepadActive() || !Minimap.IsOpen()) && (!ZInput.IsGamepadActive() || !InventoryGui.IsVisible()) && (!ZInput.IsGamepadActive() || !StoreGui.IsVisible()))
				{
					if (ZInput.IsGamepadActive())
					{
						return !Hud.IsPieceSelectionVisible();
					}
					return true;
				}
				return false;
			}

			private static bool InInventoryEtc()
			{
				if (!InventoryGui.IsVisible() && !Minimap.IsOpen() && !StoreGui.IsVisible())
				{
					return Hud.IsPieceSelectionVisible();
				}
				return true;
			}
		}
	}
}